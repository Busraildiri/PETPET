using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Security;
using PetWork.Services;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/support-reports")]
public sealed class MobileSupportApiController : ControllerBase
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "technical", "account", "content", "privacy", "other"
    };

    private readonly PetWorkDbContext _context;
    private readonly MobileMediaStorageService _mediaStorage;
    private readonly ISupportReportEmailSender _emailSender;
    private readonly ILogger<MobileSupportApiController> _logger;

    public MobileSupportApiController(PetWorkDbContext context, MobileMediaStorageService mediaStorage,
        ISupportReportEmailSender emailSender, ILogger<MobileSupportApiController> logger)
    {
        _context = context;
        _mediaStorage = mediaStorage;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<IActionResult> Create(MobileCreateSupportReportRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var category = request.Category.Trim().ToLowerInvariant();
        if (!Categories.Contains(category)) return BadRequest(new { message = "Geçerli bir sorun kategorisi seçmelisin." });
        var description = request.Description.Trim();
        if (description.Length < 10) return BadRequest(new { message = "Sorunu en az 10 karakterle anlatmalısın." });

        var screenshot = SaveOptionalImage(request.ScreenshotBase64, request.ScreenshotContentType);
        if (screenshot.Error is not null) return BadRequest(new { message = screenshot.Error });

        var report = new MobileSupportReport
        {
            UserId = userId,
            Category = category,
            Description = description,
            ScreenshotPath = screenshot.Path,
            TrackingNumber = $"PET-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant(),
            CreatedAt = DateTime.Now
        };
        _context.MobileSupportReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        var user = await _context.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Username, item.Email })
            .SingleAsync(cancellationToken);
        var screenshotUrl = screenshot.Path is null
            ? null
            : $"{Request.Scheme}://{Request.Host}/{screenshot.Path.TrimStart('/')}";
        try
        {
            await _emailSender.SendAsync(new SupportReportEmail(
                report.TrackingNumber, report.Category, report.Description,
                user.Username, user.Email, report.CreatedAt, screenshotUrl), cancellationToken);
        }
        catch (Exception exception)
        {
            // Rapor veritabanında kalır; geçici e-posta sağlayıcısı sorunu kullanıcı kaydını kaybettirmez.
            _logger.LogError(exception,
                "Sorun bildirimi kaydedildi ancak destek e-postası gönderilemedi. TrackingNumber: {TrackingNumber}",
                report.TrackingNumber);
        }
        return Ok(new { report.TrackingNumber, message = "Sorun bildirimin alındı." });
    }

    private (string? Path, string? Error) SaveOptionalImage(string? imageBase64, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(imageBase64)) return (null, null);
        if (imageBase64.Length > 11_500_000) return (null, "Ekran görüntüsü en fazla 8 MB olabilir.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(imageBase64); }
        catch (FormatException) { return (null, "Ekran görüntüsü okunamadı."); }
        if (bytes.Length is <= 0 or > 8 * 1024 * 1024) return (null, "Ekran görüntüsü en fazla 8 MB olabilir.");
        var extension = contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => null
        };
        if (extension is null || !ValidImage(bytes, extension))
            return (null, "Yalnızca geçerli JPG, PNG veya WebP görselleri yükleyebilirsin.");
        return (_mediaStorage.StageUpload("support-reports", contentType!.ToLowerInvariant(), bytes), null);
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static bool ValidImage(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8.ToArray()) && bytes[8..12].SequenceEqual("WEBP"u8.ToArray()),
        _ => false
    };
}

public sealed class MobileCreateSupportReportRequest
{
    [Required, StringLength(40)] public string Category { get; init; } = string.Empty;
    [Required, StringLength(3000, MinimumLength = 10)] public string Description { get; init; } = string.Empty;
    public string? ScreenshotBase64 { get; init; }
    [StringLength(100)] public string? ScreenshotContentType { get; init; }
}
