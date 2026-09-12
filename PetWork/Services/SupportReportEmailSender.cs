using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PetWork.Services;

public sealed record SupportReportEmail(
    string TrackingNumber,
    string Category,
    string Description,
    string Username,
    string UserEmail,
    DateTime CreatedAt,
    string? ScreenshotUrl);

public interface ISupportReportEmailSender
{
    Task SendAsync(SupportReportEmail report, CancellationToken cancellationToken);
}

public sealed class DevelopmentSupportReportEmailSender : ISupportReportEmailSender
{
    private readonly IWebHostEnvironment _environment;

    public DevelopmentSupportReportEmailSender(IWebHostEnvironment environment) => _environment = environment;

    public async Task SendAsync(SupportReportEmail report, CancellationToken cancellationToken)
    {
        var inbox = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".local", "support-report-inbox"));
        Directory.CreateDirectory(inbox);
        await File.WriteAllTextAsync(
            Path.Combine(inbox, $"{report.TrackingNumber}.json"),
            JsonSerializer.Serialize(report),
            cancellationToken);
    }
}

public sealed class ResendSupportReportEmailSender : ISupportReportEmailSender
{
    private static readonly IReadOnlyDictionary<string, string> CategoryNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["technical"] = "Teknik sorun",
            ["account"] = "Hesap",
            ["content"] = "İçerik",
            ["privacy"] = "Gizlilik",
            ["other"] = "Diğer"
        };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ResendSupportReportEmailSender> _logger;

    public ResendSupportReportEmailSender(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ResendSupportReportEmailSender> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(SupportReportEmail report, CancellationToken cancellationToken)
    {
        var inboxAddress = _configuration["Email:Support:InboxAddress"];
        if (string.IsNullOrWhiteSpace(inboxAddress))
            throw new InvalidOperationException("Email:Support:InboxAddress is not configured.");

        var apiKey = _configuration["Email:Resend:ApiKey"]!;
        var fromAddress = _configuration["Email:Resend:FromAddress"]!;
        var fromName = _configuration["Email:Resend:FromName"] ?? "Pet'im";
        var category = CategoryNames.GetValueOrDefault(report.Category, report.Category);
        var screenshotText = report.ScreenshotUrl is null ? "Yok" : report.ScreenshotUrl;
        var safeTracking = WebUtility.HtmlEncode(report.TrackingNumber);
        var safeCategory = WebUtility.HtmlEncode(category);
        var safeUsername = WebUtility.HtmlEncode(report.Username);
        var safeUserEmail = WebUtility.HtmlEncode(report.UserEmail);
        var safeDescription = WebUtility.HtmlEncode(report.Description).Replace("\n", "<br>", StringComparison.Ordinal);
        var safeScreenshot = report.ScreenshotUrl is null
            ? "Yok"
            : $"<a href=\"{WebUtility.HtmlEncode(report.ScreenshotUrl)}\">Ekran görüntüsünü aç</a>";
        var payload = new
        {
            from = $"{fromName} <{fromAddress}>",
            to = new[] { inboxAddress.Trim() },
            reply_to = report.UserEmail,
            subject = $"[{report.TrackingNumber}] {category} · @{report.Username}",
            text = $"Takip numarası: {report.TrackingNumber}\nKategori: {category}\nKullanıcı: @{report.Username}\nKullanıcı e-postası: {report.UserEmail}\nTarih: {report.CreatedAt:dd.MM.yyyy HH:mm:ss}\n\nAçıklama:\n{report.Description}\n\nEkran görüntüsü: {screenshotText}",
            html = $"<h2>Yeni sorun bildirimi</h2><p><strong>Takip numarası:</strong> {safeTracking}<br><strong>Kategori:</strong> {safeCategory}<br><strong>Kullanıcı:</strong> @{safeUsername}<br><strong>Kullanıcı e-postası:</strong> {safeUserEmail}<br><strong>Tarih:</strong> {report.CreatedAt:dd.MM.yyyy HH:mm:ss}</p><h3>Açıklama</h3><p>{safeDescription}</p><p><strong>Ekran görüntüsü:</strong> {safeScreenshot}</p>"
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"petim-support-{Hash(report.TrackingNumber)[..32]}");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend sorun bildirimi e-postasını reddetti. Status: {StatusCode}, TrackingNumber: {TrackingNumber}",
                (int)response.StatusCode, report.TrackingNumber);
            throw new InvalidOperationException("Support report email could not be delivered.");
        }
        _logger.LogInformation("Sorun bildirimi e-postası gönderildi. TrackingNumber: {TrackingNumber}", report.TrackingNumber);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
