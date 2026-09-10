using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using PetWork.Security;

namespace PetWork.Services;

public interface IEmailVerificationSender
{
    Task SendAsync(string email, string username, string code, string challengeToken, CancellationToken cancellationToken);
}

public sealed class EmailVerificationService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const int MaximumAttempts = 5;
    private readonly IEmailVerificationSender _sender;
    private readonly ConcurrentDictionary<string, ChallengeState> _challenges = new();

    public EmailVerificationService(IEmailVerificationSender sender) => _sender = sender;

    public async Task<EmailVerificationChallenge> CreateAsync(int userId, string email, string username, bool rememberMe, CancellationToken cancellationToken)
    {
        CleanupExpired();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.Add(Lifetime);
        _challenges[Hash(token)] = new(userId, rememberMe, Hash(code), expiresAt);
        try { await _sender.SendAsync(email, username, code, token, cancellationToken); }
        catch { _challenges.TryRemove(Hash(token), out _); throw; }
        return new(token, expiresAt, SensitiveDataMasker.Mask(email));
    }

    public EmailVerificationResult Verify(string token, string code)
    {
        var key = Hash(token);
        if (!_challenges.TryGetValue(key, out var state) || state.ExpiresAt <= DateTime.UtcNow)
        {
            _challenges.TryRemove(key, out _);
            return EmailVerificationResult.Expired;
        }
        if (Interlocked.Increment(ref state.Attempts) > MaximumAttempts)
        {
            _challenges.TryRemove(key, out _);
            return EmailVerificationResult.TooManyAttempts;
        }
        if (code.Length != 6 || !code.All(char.IsDigit) || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(state.CodeHash), Convert.FromHexString(Hash(code))))
            return EmailVerificationResult.Invalid;
        _challenges.TryRemove(key, out _);
        return new(true, EmailVerificationFailure.None, state.UserId, state.RememberMe);
    }

    public bool TryConsumeContext(string token, out int userId, out bool rememberMe)
    {
        if (_challenges.TryRemove(Hash(token), out var state) && state.ExpiresAt > DateTime.UtcNow)
        { userId = state.UserId; rememberMe = state.RememberMe; return true; }
        userId = 0; rememberMe = false; return false;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _challenges)
            if (pair.Value.ExpiresAt <= now) _challenges.TryRemove(pair.Key, out _);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed class ChallengeState(int userId, bool rememberMe, string codeHash, DateTime expiresAt)
    { public int UserId { get; } = userId; public bool RememberMe { get; } = rememberMe; public string CodeHash { get; } = codeHash; public DateTime ExpiresAt { get; } = expiresAt; public int Attempts; }
}

public sealed class ResendEmailVerificationSender : IEmailVerificationSender
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ResendEmailVerificationSender> _logger;
    public ResendEmailVerificationSender(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<ResendEmailVerificationSender> logger)
    { _configuration = configuration; _httpClientFactory = httpClientFactory; _logger = logger; }

    public async Task SendAsync(string email, string username, string code, string challengeToken, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Email:Resend:ApiKey"]!;
        var fromAddress = _configuration["Email:Resend:FromAddress"]!;
        var fromName = _configuration["Email:Resend:FromName"] ?? "Pet'im";
        var safeName = WebUtility.HtmlEncode(username);
        var payload = new { from = $"{fromName} <{fromAddress}>", to = new[] { email }, subject = "Pet'im doğrulama kodun", text = $"Merhaba {username}, doğrulama kodun: {code}. Kod 5 dakika geçerlidir. Bu işlemi sen yapmadıysan kodu kimseyle paylaşma.", html = $"<p>Merhaba <strong>{safeName}</strong>,</p><p>Doğrulama kodun:</p><p style=\"font-size:30px;font-weight:700;letter-spacing:7px\">{code}</p><p>Kod 5 dakika geçerlidir. Bu işlemi sen yapmadıysan kodu kimseyle paylaşma.</p>" };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"petim-email-verification-{Hash(challengeToken)[..32].ToLowerInvariant()}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new EmailDeliveryException("Doğrulama e-postası gönderilemedi. Biraz sonra tekrar dene.");
            _logger.LogInformation("E-posta doğrulama kodu gönderildi.");
        }
        catch (EmailDeliveryException) { throw; }
        catch (Exception exception)
        { _logger.LogError(exception, "E-posta doğrulama kodu gönderilemedi."); throw new EmailDeliveryException("Doğrulama e-postası gönderilemedi. Biraz sonra tekrar dene.", exception); }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class DevelopmentEmailVerificationSender : IEmailVerificationSender
{
    private readonly IWebHostEnvironment _environment;
    public DevelopmentEmailVerificationSender(IWebHostEnvironment environment) => _environment = environment;
    public async Task SendAsync(string email, string username, string code, string challengeToken, CancellationToken cancellationToken)
    {
        var inbox = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".local", "email-verification-inbox"));
        Directory.CreateDirectory(inbox);
        var payload = JsonSerializer.Serialize(new { email, username, code, challengeToken });
        await File.WriteAllTextAsync(Path.Combine(inbox, $"{Guid.NewGuid():N}.json"), payload, cancellationToken);
    }
}

public sealed record EmailVerificationChallenge(string ChallengeToken, DateTime ExpiresAt, string MaskedEmail);
public sealed record EmailVerificationResult(bool Succeeded, EmailVerificationFailure Failure, int UserId = 0, bool RememberMe = false)
{ public static EmailVerificationResult Expired { get; } = new(false, EmailVerificationFailure.Expired); public static EmailVerificationResult TooManyAttempts { get; } = new(false, EmailVerificationFailure.TooManyAttempts); public static EmailVerificationResult Invalid { get; } = new(false, EmailVerificationFailure.Invalid); }
public enum EmailVerificationFailure { None, Expired, TooManyAttempts, Invalid }
public sealed class EmailDeliveryException : Exception { public EmailDeliveryException(string message) : base(message) { } public EmailDeliveryException(string message, Exception inner) : base(message, inner) { } }
