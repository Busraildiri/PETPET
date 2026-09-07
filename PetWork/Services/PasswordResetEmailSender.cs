using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PetWork.Services;

public interface IPasswordResetEmailSender
{
    Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken);
}

public sealed class DevelopmentPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IWebHostEnvironment _environment;

    public DevelopmentPasswordResetEmailSender(IWebHostEnvironment environment) => _environment = environment;

    public async Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var inbox = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".local", "password-reset-inbox"));
        Directory.CreateDirectory(inbox);
        var fileName = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant() + ".json";
        var payload = JsonSerializer.Serialize(new
        {
            email,
            username,
            resetUrl = $"petim://reset-password?token={Uri.EscapeDataString(token)}",
            expiresAt
        });
        await File.WriteAllTextAsync(Path.Combine(inbox, fileName), payload, cancellationToken);
    }
}

public sealed class UnavailablePasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly ILogger<UnavailablePasswordResetEmailSender> _logger;

    public UnavailablePasswordResetEmailSender(ILogger<UnavailablePasswordResetEmailSender> logger) => _logger = logger;

    public Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Password reset requested but no production email provider is configured.");
        return Task.CompletedTask;
    }
}

public sealed class ResendPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ResendPasswordResetEmailSender> _logger;

    public ResendPasswordResetEmailSender(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<ResendPasswordResetEmailSender> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Email:Resend:ApiKey"]!;
        var fromAddress = _configuration["Email:Resend:FromAddress"]!;
        var fromName = _configuration["Email:Resend:FromName"] ?? "Pet'im";
        var deepLinkBase = _configuration["Email:PasswordReset:DeepLinkBase"]!;
        var separator = deepLinkBase.Contains('?') ? '&' : '?';
        var resetUrl = $"{deepLinkBase}{separator}token={Uri.EscapeDataString(token)}";
        var safeName = WebUtility.HtmlEncode(username);
        var safeUrl = WebUtility.HtmlEncode(resetUrl);
        var payload = new
        {
            from = $"{fromName} <{fromAddress}>",
            to = new[] { email },
            subject = "Pet'im şifre yenileme",
            text = $"Merhaba {username}, şifreni yenilemek için bu bağlantıyı aç: {resetUrl} Bağlantı 30 dakika geçerlidir. Bu talebi sen yapmadıysan e-postayı yok say.",
            html = $"<p>Merhaba <strong>{safeName}</strong>,</p><p>Şifreni yenilemek için aşağıdaki düğmeyi kullan.</p><p><a href=\"{safeUrl}\" style=\"display:inline-block;padding:12px 20px;background:#71486B;color:#fff;text-decoration:none;border-radius:12px;font-weight:700\">Şifremi yenile</a></p><p>Bağlantı 30 dakika geçerlidir. Bu talebi sen yapmadıysan e-postayı yok say.</p>"
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"petim-password-reset-{Hash(token)[..32]}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Resend şifre yenileme iletisini reddetti. Status: {StatusCode}", (int)response.StatusCode);
                throw new InvalidOperationException("Password reset email could not be delivered.");
            }
            _logger.LogInformation("Şifre yenileme e-postası gönderildi.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            _logger.LogError(exception, "Şifre yenileme e-postası gönderilemedi.");
            throw new InvalidOperationException("Password reset email could not be delivered.", exception);
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
