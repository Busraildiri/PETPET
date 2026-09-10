using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;
using PetWork.Security;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/adoption")]
public sealed class MobileAdoptionApiController : ControllerBase
{
    private static readonly string[] ListingStatuses = ["active", "adopted", "closed"];
    private static readonly string[] ApplicationStatuses = ["pending", "accepted", "rejected"];
    private readonly PetWorkDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly MobilePushNotificationService _pushNotifications;
    private readonly MobileMediaStorageService _mediaStorage;
    private readonly ResourceAuthorizationService _resourceAuthorization;

    public MobileAdoptionApiController(PetWorkDbContext context, IWebHostEnvironment environment,
        MobilePushNotificationService pushNotifications, MobileMediaStorageService mediaStorage,
        ResourceAuthorizationService resourceAuthorization)
    {
        _context = context;
        _environment = environment;
        _pushNotifications = pushNotifications;
        _mediaStorage = mediaStorage;
        _resourceAuthorization = resourceAuthorization;
    }

    [HttpGet("listings")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MobileAdoptionListingResponse>>> GetListings(CancellationToken cancellationToken)
    {
        var hasUser = TryGetUserId(out var userId);
        var items = await _context.AdoptionListings.AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Applications)
            .Where(item => !item.IsDeleted && (item.Status == "active" ||
                (hasUser && (item.UserId == userId || item.Applications.Any(application => application.UserId == userId)))))
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(item => ToResponse(item, hasUser ? userId : null)).ToList());
    }

    [HttpPost("listings")]
    [Authorize]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<MobileAdoptionListingResponse>> CreateListing(MobileCreateAdoptionListingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (request.AgeYears is < 0 or > 40) return BadRequest(new { message = "Yaş bilgisi geçersiz." });
        var image = SaveImage(request.ImageBase64, request.ImageContentType);
        if (image.Error is not null) return BadRequest(new { message = image.Error });

        var listing = new AdoptionListing
        {
            UserId = userId,
            PetName = request.PetName.Trim(), Species = request.Species.Trim(), Breed = Clean(request.Breed),
            AgeYears = request.AgeYears, Gender = Clean(request.Gender), City = request.City.Trim(),
            District = Clean(request.District), HealthInfo = request.HealthInfo.Trim(), Story = request.Story.Trim(),
            ImagePath = image.Path!, Status = "active", CreatedAt = DateTime.Now
        };
        _context.AdoptionListings.Add(listing);
        await _context.SaveChangesAsync(cancellationToken);
        listing.User = await _context.Users.AsNoTracking().FirstAsync(user => user.Id == userId, cancellationToken);
        return CreatedAtAction(nameof(GetListings), new { id = listing.Id }, ToResponse(listing, userId));
    }

    [HttpPost("listings/{id:int}/applications")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<MobileAdoptionApplicationResponse>> Apply(int id, MobileCreateAdoptionApplicationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var listing = await _context.AdoptionListings.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted && item.Status == "active", cancellationToken);
        if (listing is null) return NotFound(new { message = "Aktif sahiplendirme ilanı bulunamadı." });
        if (listing.UserId == userId) return BadRequest(new { message = "Kendi ilanına başvuru yapamazsın." });
        if (await _context.AdoptionApplications.AnyAsync(item => item.AdoptionListingId == id && item.UserId == userId, cancellationToken))
            return Conflict(new { message = "Bu ilana daha önce başvurdun." });

        var application = new AdoptionApplication
        {
            AdoptionListingId = id, UserId = userId, Message = request.Message.Trim(), Status = "pending", CreatedAt = DateTime.Now
        };
        _context.AdoptionApplications.Add(application);
        var applicantUsername = await _context.Users.Where(user => user.Id == userId).Select(user => user.Username).FirstAsync(cancellationToken);
        var notification = new MobileNotification
        {
            UserId = listing.UserId,
            Type = "adoption_application",
            Title = $"{listing.PetName} için yeni başvuru",
            Body = $"@{applicantUsername} sahiplendirme ilanına başvurdu.",
            EntityType = "adoption",
            EntityId = listing.Id,
            CreatedAt = DateTime.Now
        };
        _context.MobileNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        await _pushNotifications.SendAsync(notification, cancellationToken);
        return Ok(new MobileAdoptionApplicationResponse(application.Id, applicantUsername, application.Message, application.Status, application.CreatedAt));
    }

    [HttpGet("listings/{id:int}/applications")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MobileAdoptionApplicationResponse>>> GetApplications(int id, CancellationToken cancellationToken)
    {
        var listing = await _context.AdoptionListings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (listing is null) return NotFound(new { message = "İlan bulunamadı." });
        var access = await _resourceAuthorization.AuthorizeAsync(User, listing.UserId, ResourceAccessRequirement.OwnerOrAdmin, cancellationToken);
        if (access.Status == ResourceAuthorizationStatus.Unauthenticated) return Unauthorized();
        if (!access.IsAllowed) return Forbid();
        var applications = await _context.AdoptionApplications.AsNoTracking().Include(item => item.User)
            .Where(item => item.AdoptionListingId == id).OrderByDescending(item => item.CreatedAt)
            .Select(item => new MobileAdoptionApplicationResponse(item.Id, item.User.Username, item.Message, item.Status, item.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(applications);
    }

    [HttpPut("applications/{applicationId:int}/status")]
    [Authorize]
    [SensitiveRateLimit("Expensive")]
    public async Task<IActionResult> UpdateApplication(int applicationId, MobileUpdateAdoptionApplicationRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status.Trim().ToLowerInvariant();
        if (!ApplicationStatuses.Contains(status) || status == "pending") return BadRequest(new { message = "Başvuru durumu accepted veya rejected olmalıdır." });
        var application = await _context.AdoptionApplications.Include(item => item.AdoptionListing)
            .FirstOrDefaultAsync(item => item.Id == applicationId, cancellationToken);
        if (application is null) return NotFound(new { message = "Başvuru bulunamadı." });
        var access = await _resourceAuthorization.AuthorizeAsync(User, application.AdoptionListing.UserId, ResourceAccessRequirement.OwnerOrAdmin, cancellationToken);
        if (access.Status == ResourceAuthorizationStatus.Unauthenticated) return Unauthorized();
        if (!access.IsAllowed) return Forbid();
        if (application.AdoptionListing.Status != "active" && application.Status != status)
            return Conflict(new { message = "Bu sahiplendirme ilanı artık aktif değil." });
        var changed = application.Status != status;
        application.Status = status;
        var notifications = new List<MobileNotification>();
        if (changed)
        {
            var notification = new MobileNotification
            {
                UserId = application.UserId,
                Type = "adoption_status",
                Title = $"{application.AdoptionListing.PetName} başvurun güncellendi",
                Body = status == "accepted" ? "Sahiplendirme başvurun kabul edildi." : "Sahiplendirme başvurun reddedildi.",
                EntityType = "adoption",
                EntityId = application.AdoptionListingId,
                CreatedAt = DateTime.Now
            };
            notifications.Add(notification);
            _context.MobileNotifications.Add(notification);

            if (status == "accepted")
            {
                application.AdoptionListing.Status = "adopted";
                var otherApplications = await _context.AdoptionApplications
                    .Where(item => item.AdoptionListingId == application.AdoptionListingId &&
                                   item.Id != application.Id && item.Status == "pending")
                    .ToListAsync(cancellationToken);
                foreach (var other in otherApplications)
                {
                    other.Status = "rejected";
                    var rejectedNotification = new MobileNotification
                    {
                        UserId = other.UserId,
                        Type = "adoption_status",
                        Title = $"{application.AdoptionListing.PetName} başvurun güncellendi",
                        Body = "Sahiplendirme ilanı başka bir başvuruyla sonuçlandı.",
                        EntityType = "adoption",
                        EntityId = application.AdoptionListingId,
                        CreatedAt = DateTime.Now
                    };
                    notifications.Add(rejectedNotification);
                    _context.MobileNotifications.Add(rejectedNotification);
                }
            }
        }
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
            await _pushNotifications.SendAsync(notification, cancellationToken);
        return Ok(new { status });
    }

    [HttpPut("listings/{id:int}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateListingStatus(int id, MobileUpdateAdoptionListingRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status.Trim().ToLowerInvariant();
        if (!ListingStatuses.Contains(status)) return BadRequest(new { message = "İlan durumu geçersiz." });
        var listing = await _context.AdoptionListings.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (listing is null) return NotFound(new { message = "İlan bulunamadı." });
        var access = await _resourceAuthorization.AuthorizeAsync(User, listing.UserId, ResourceAccessRequirement.OwnerOrAdmin, cancellationToken);
        if (access.Status == ResourceAuthorizationStatus.Unauthenticated) return Unauthorized();
        if (!access.IsAllowed) return Forbid();
        listing.Status = status;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { status });
    }

    [HttpPost("listings/{id:int}/reports")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<IActionResult> Report(int id, MobileReportRequest request, CancellationToken cancellationToken)
    {
        var listing = await _context.AdoptionListings.AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(item => new { item.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (listing is null) return NotFound(new { message = "İlan bulunamadı." });
        var access = await _resourceAuthorization.AuthorizeAsync(User, listing.UserId, ResourceAccessRequirement.NonOwner, cancellationToken);
        if (access.Status == ResourceAuthorizationStatus.Unauthenticated) return Unauthorized();
        if (!access.IsAllowed) return Forbid();
        var userId = access.UserId!.Value;
        if (await _context.AdoptionListingReports.AnyAsync(item => item.AdoptionListingId == id && item.UserId == userId, cancellationToken))
            return Conflict(new { message = "Bu ilanı daha önce bildirdin." });
        _context.AdoptionListingReports.Add(new AdoptionListingReport { AdoptionListingId = id, UserId = userId, Reason = request.Reason.Trim(), CreatedAt = DateTime.Now });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Bildirimin alındı." });
    }

    private static MobileAdoptionListingResponse ToResponse(AdoptionListing item, int? userId)
    {
        var ownApplication = userId.HasValue
            ? item.Applications.FirstOrDefault(application => application.UserId == userId.Value)
            : null;
        return new MobileAdoptionListingResponse(
            item.Id, item.PetName, item.Species, item.Breed, item.AgeYears, item.Gender, item.City, item.District,
            item.HealthInfo, item.Story, item.ImagePath, item.Status, item.User.Username, item.CreatedAt,
            userId.HasValue && item.UserId == userId, ownApplication is not null, ownApplication?.Status);
    }

    private (string? Path, string? Error) SaveImage(string imageBase64, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(imageBase64)) return (null, "İlan fotoğrafı gereklidir.");
        if (imageBase64.Length > 11_500_000) return (null, "Fotoğraf en fazla 8 MB olabilir.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(imageBase64); } catch (FormatException) { return (null, "Fotoğraf verisi okunamadı."); }
        if (bytes.Length is <= 0 or > 8 * 1024 * 1024) return (null, "Fotoğraf en fazla 8 MB olabilir.");
        var extension = ImageExtension(contentType);
        if (extension is null || !ValidImage(bytes, extension)) return (null, "Yalnızca geçerli JPG, PNG veya WebP fotoğrafları yükleyebilirsin.");
        return (_mediaStorage.StageUpload("adoption", contentType!.ToLowerInvariant(), bytes), null);
    }

    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? ImageExtension(string? contentType) => contentType?.ToLowerInvariant() switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => null };
    private static bool ValidImage(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8.ToArray()) && bytes[8..12].SequenceEqual("WEBP"u8.ToArray()),
        _ => false
    };
}

public sealed class MobileCreateAdoptionListingRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string PetName { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 2)] public string Species { get; init; } = string.Empty;
    [StringLength(100)] public string? Breed { get; init; }
    public int? AgeYears { get; init; }
    [StringLength(20)] public string? Gender { get; init; }
    [Required, StringLength(80, MinimumLength = 2)] public string City { get; init; } = string.Empty;
    [StringLength(80)] public string? District { get; init; }
    [Required, StringLength(500, MinimumLength = 5)] public string HealthInfo { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 10)] public string Story { get; init; } = string.Empty;
    [Required] public string ImageBase64 { get; init; } = string.Empty;
    [Required, StringLength(100)] public string ImageContentType { get; init; } = string.Empty;
}
public sealed class MobileCreateAdoptionApplicationRequest { [Required, StringLength(1000, MinimumLength = 10)] public string Message { get; init; } = string.Empty; }
public sealed class MobileUpdateAdoptionApplicationRequest { [Required, StringLength(20)] public string Status { get; init; } = string.Empty; }
public sealed class MobileUpdateAdoptionListingRequest { [Required, StringLength(20)] public string Status { get; init; } = string.Empty; }
public sealed class MobileReportRequest { [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; init; } = string.Empty; }
public sealed record MobileAdoptionListingResponse(int Id, string PetName, string Species, string? Breed, int? AgeYears, string? Gender,
    string City, string? District, string HealthInfo, string Story, string ImagePath, string Status, string OwnerUsername,
    DateTime CreatedAt, bool IsMine, bool HasApplied, string? ApplicationStatus);
public sealed record MobileAdoptionApplicationResponse(int Id, string Username, string Message, string Status, DateTime CreatedAt);
