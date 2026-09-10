using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PetWork.Controllers;

[ApiController]
[AllowAnonymous]
[Route("security/csp-report")]
public sealed class CspReportController : ControllerBase
{
    private readonly ILogger<CspReportController> _logger;

    public CspReportController(ILogger<CspReportController> logger) => _logger = logger;

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("mobile-content")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Report(CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            var report = document.RootElement.TryGetProperty("csp-report", out var legacyReport)
                ? legacyReport
                : document.RootElement;
            _logger.LogWarning(
                "CSP report: directive={Directive}, blocked={BlockedUri}, document={DocumentUri}",
                ReadLimited(report, "violated-directive"),
                ReadUriWithoutQuery(report, "blocked-uri"),
                ReadUriWithoutQuery(report, "document-uri"));
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(exception, "Geçersiz CSP raporu yok sayıldı.");
        }

        return NoContent();
    }

    private static string ReadLimited(JsonElement report, string property)
    {
        if (!report.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return "unknown";
        var text = value.GetString() ?? "unknown";
        return text.Length <= 500 ? text : text[..500];
    }

    private static string ReadUriWithoutQuery(JsonElement report, string property)
    {
        var text = ReadLimited(report, property);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return text;
        var sanitized = $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }
}
