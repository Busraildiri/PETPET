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
[Route("api/mobile/lost-pets")]
public sealed class MobileLostPetsApiController : ControllerBase
{
    private static readonly string[] AllowedKinds = ["lost", "found"];
    private static readonly string[] AllowedStatuses = ["active", "resolved", "closed"];
    private readonly PetWorkDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly MobilePushNotificationService _pushNotifications;
    private readonly MobileMediaStorageService _mediaStorage;

    public MobileLostPetsApiController(PetWorkDbContext context, IWebHostEnvironment environment,
        MobilePushNotificationService pushNotifications, MobileMediaStorageService mediaStorage)
    {
        _context = context;
        _environment = environment;
        _pushNotifications = pushNotifications;
        _mediaStorage = mediaStorage;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MobileLostPetSummary>>> GetListings(
        string? kind,
        string? city,
        string? district,
        double? latitude,
        double? longitude,
        double radiusKm = 50,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var normalizedKind = kind?.Trim().ToLowerInvariant();
        if (normalizedKind is not null && !AllowedKinds.Contains(normalizedKind))
            return BadRequest(new { message = "İlan türü lost veya found olmalıdır." });

        var query = _context.LostPetListings.AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status == "active" && item.ExpiresAt > now);
        if (normalizedKind is not null) query = query.Where(item => item.Kind == normalizedKind);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(item => item.City == city.Trim());
        if (!string.IsNullOrWhiteSpace(district)) query = query.Where(item => item.District == district.Trim());

        var candidates = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .Select(item => new ListingProjection(item, item.Sightings.Count(sighting => !sighting.IsDeleted)))
            .ToListAsync(cancellationToken);

        radiusKm = Math.Clamp(radiusKm, 1, 200);
        var response = candidates
            .Select(item => ToSummary(item.Listing, item.SightingCount, latitude, longitude))
            .Where(item => !latitude.HasValue || !longitude.HasValue || !item.DistanceKm.HasValue || item.DistanceKm <= radiusKm)
            .OrderBy(item => item.DistanceKm ?? double.MaxValue)
            .ThenByDescending(item => item.CreatedAt)
            .Take(100)
            .ToList();
        return Ok(response);
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MobileLostPetSummary>>> GetMine(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var listings = await _context.LostPetListings.AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new ListingProjection(item, item.Sightings.Count(sighting => !sighting.IsDeleted)))
            .ToListAsync(cancellationToken);
        return Ok(listings.Select(item => ToSummary(item.Listing, item.SightingCount, null, null)).ToList());
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MobileLostPetDetail>> GetListing(int id, CancellationToken cancellationToken)
    {
        var listing = await _context.LostPetListings.AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Sightings.Where(sighting => !sighting.IsDeleted)).ThenInclude(sighting => sighting.User)
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (listing is null) return NotFound(new { message = "İlan bulunamadı." });

        var mine = TryGetUserId(out var userId) && listing.UserId == userId;
        var exposeContact = await _context.MobileContactPreferences.AsNoTracking()
            .AnyAsync(item => item.UserId == listing.UserId && item.AllowLostPetSharing, cancellationToken);
        return Ok(ToDetail(listing, mine, exposeContact));
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<MobileLostPetDetail>> CreateListing(
        MobileCreateLostPetRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        var kind = request.Kind.Trim().ToLowerInvariant();
        if (!AllowedKinds.Contains(kind)) return BadRequest(new { message = "İlan türü geçersiz." });
        if (!ValidCoordinates(request.Latitude, request.Longitude)) return BadRequest(new { message = "Konum koordinatları geçersiz." });
        if (request.EventAt > DateTime.Now.AddMinutes(10)) return BadRequest(new { message = "Olay zamanı gelecekte olamaz." });

        var contact = await ResolveListingContact(userId, request.AllowInAppMessages, request.SharePhone,
            request.ShareEmail, cancellationToken);
        if (contact.Error is not null) return BadRequest(new { message = contact.Error });

        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return Unauthorized(new { message = "Kullanıcı hesabı bulunamadı." });
        var imageResult = SaveImage(request.ImageBase64, request.ImageContentType);
        if (imageResult.Error is not null) return BadRequest(new { message = imageResult.Error });

        var listing = new LostPetListing
        {
            UserId = userId,
            Kind = kind,
            PetName = request.PetName.Trim(),
            Species = request.Species.Trim(),
            Breed = Clean(request.Breed),
            DistinguishingFeatures = request.DistinguishingFeatures.Trim(),
            EventAt = AsUnspecified(request.EventAt),
            City = request.City.Trim(),
            District = request.District.Trim(),
            Neighborhood = Clean(request.Neighborhood),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CollarOrMicrochip = Clean(request.CollarOrMicrochip),
            Notes = Clean(request.Notes),
            ImagePath = imageResult.Path!,
            AllowInAppMessages = request.AllowInAppMessages,
            ContactPhone = contact.Phone,
            ContactEmail = contact.Email,
            Status = "active",
            SourceName = "Pet'im",
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddDays(90)
        };
        _context.LostPetListings.Add(listing);
        user.ExperiencePoints += 15;
        await _context.SaveChangesAsync(cancellationToken);

        listing.User = user;
        return CreatedAtAction(nameof(GetListing), new { id = listing.Id }, ToDetail(listing, true, true));
    }

    [HttpPost("{id:int}/sightings")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<MobileLostPetSightingResponse>> AddSighting(
        int id,
        MobileCreateLostPetSightingRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!ValidCoordinates(request.Latitude, request.Longitude)) return BadRequest(new { message = "Konum koordinatları geçersiz." });
        if (request.SeenAt > DateTime.Now.AddMinutes(10)) return BadRequest(new { message = "Görülme zamanı gelecekte olamaz." });
        var listing = await _context.LostPetListings
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted && item.Status == "active", cancellationToken);
        if (listing is null) return NotFound(new { message = "Aktif ilan bulunamadı." });
        if (listing.UserId == userId) return BadRequest(new { message = "Kendi ilanına görülme bildirimi ekleyemezsin." });
        if (!listing.AllowInAppMessages)
            return Conflict(new { message = "İlan sahibi uygulama içinden görülme bildirimi kabul etmiyor. İlandaki diğer iletişim yöntemlerini kullanabilirsin." });

        var sighting = new LostPetSighting
        {
            LostPetListingId = id,
            UserId = userId,
            LocationLabel = request.LocationLabel.Trim(),
            SeenAt = AsUnspecified(request.SeenAt),
            Note = Clean(request.Note),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CreatedAt = DateTime.Now
        };
        _context.LostPetSightings.Add(sighting);
        var notification = new MobileNotification
        {
            UserId = listing.UserId,
            Type = "lost_sighting",
            Title = $"{listing.PetName} için yeni görülme bildirimi",
            Body = $"{request.LocationLabel.Trim()} konumunda yeni bir gözlem paylaşıldı.",
            EntityType = "lost_pet",
            EntityId = listing.Id,
            CreatedAt = DateTime.Now
        };
        _context.MobileNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        await _pushNotifications.SendAsync(notification, cancellationToken);
        return CreatedAtAction(nameof(GetListing), new { id }, new MobileLostPetSightingResponse(
            sighting.Id, sighting.LocationLabel, sighting.SeenAt, sighting.Note,
            RoundCoordinate(sighting.Latitude), RoundCoordinate(sighting.Longitude), sighting.CreatedAt));
    }

    [HttpPut("{id:int}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, MobileUpdateLostPetStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var status = request.Status.Trim().ToLowerInvariant();
        if (!AllowedStatuses.Contains(status)) return BadRequest(new { message = "İlan durumu geçersiz." });
        var listing = await _context.LostPetListings.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (listing is null) return NotFound(new { message = "İlan bulunamadı." });
        if (listing.UserId != userId && !User.IsInRole("Admin")) return Forbid();
        listing.Status = status;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { status });
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteListing(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var listing = await _context.LostPetListings.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (listing is null) return NotFound(new { message = "İlan bulunamadı." });
        if (listing.UserId != userId && !User.IsInRole("Admin")) return Forbid();
        listing.IsDeleted = true;
        await _mediaStorage.StageDeleteAsync(listing.ImagePath, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static MobileLostPetSummary ToSummary(LostPetListing item, int sightingCount, double? latitude, double? longitude) => new(
        item.Id, item.Kind, item.PetName, item.Species, item.Breed, item.City, item.District, item.Neighborhood,
        RoundCoordinate(item.Latitude), RoundCoordinate(item.Longitude), item.EventAt, item.ImagePath, item.Status,
        item.CreatedAt, sightingCount, Distance(latitude, longitude, item.Latitude, item.Longitude));

    private static MobileLostPetDetail ToDetail(LostPetListing item, bool owner, bool exposeContact) => new(
        item.Id, item.Kind, item.PetName, item.Species, item.Breed, item.DistinguishingFeatures, item.EventAt,
        item.City, item.District, item.Neighborhood, owner ? item.Latitude : RoundCoordinate(item.Latitude),
        owner ? item.Longitude : RoundCoordinate(item.Longitude), item.CollarOrMicrochip, item.Notes, item.ImagePath,
        item.Status, item.SourceName, item.CreatedAt, item.ExpiresAt, item.User.Username, owner,
        item.AllowInAppMessages, exposeContact ? item.ContactPhone : null, exposeContact ? item.ContactEmail : null,
        item.Sightings.OrderByDescending(sighting => sighting.SeenAt).Select(sighting => new MobileLostPetSightingResponse(
            sighting.Id, sighting.LocationLabel, sighting.SeenAt, sighting.Note,
            owner ? sighting.Latitude : RoundCoordinate(sighting.Latitude), owner ? sighting.Longitude : RoundCoordinate(sighting.Longitude),
            sighting.CreatedAt)).ToList());

    private async Task<(string? Phone, string? Email, string? Error)> ResolveListingContact(int userId,
        bool allowInAppMessages, bool sharePhone, bool shareEmail, CancellationToken cancellationToken)
    {
        if (!sharePhone && !shareEmail)
            return allowInAppMessages ? (null, null, null) : (null, null, "En az bir iletişim yöntemi seçmelisin.");

        var preference = await _context.MobileContactPreferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference?.AllowLostPetSharing != true)
            return (null, null, "Telefon veya e-posta paylaşmak için Ayarlar > İletişim bilgileri bölümündeki kayıp pati iznini açmalısın.");
        if (sharePhone && string.IsNullOrWhiteSpace(preference.Phone))
            return (null, null, "Telefonu seçebilmek için Ayarlar > İletişim bilgileri bölümüne telefon numaranı kaydetmelisin.");
        if (shareEmail && string.IsNullOrWhiteSpace(preference.ContactEmail))
            return (null, null, "E-postayı seçebilmek için Ayarlar > İletişim bilgileri bölümüne e-posta adresini kaydetmelisin.");
        return (sharePhone ? preference.Phone : null, shareEmail ? preference.ContactEmail : null, null);
    }

    private (string? Path, string? Error) SaveImage(string imageBase64, string? contentType)
    {
        const int maximumImageSize = 8 * 1024 * 1024;
        if (string.IsNullOrWhiteSpace(imageBase64)) return (null, "İlan fotoğrafı gereklidir.");
        if (imageBase64.Length > 11_500_000) return (null, "Fotoğraf en fazla 8 MB olabilir.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(imageBase64); }
        catch (FormatException) { return (null, "Fotoğraf verisi okunamadı."); }
        if (bytes.Length is <= 0 or > maximumImageSize) return (null, "Fotoğraf en fazla 8 MB olabilir.");
        var extension = contentType?.ToLowerInvariant() switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => null };
        if (extension is null) return (null, "Yalnızca JPG, PNG veya WebP fotoğrafları yükleyebilirsin.");
        if (!HasValidImageSignature(bytes, extension)) return (null, "Seçilen dosya geçerli bir fotoğraf değil.");
        var normalizedContentType = contentType!.ToLowerInvariant();
        return (_mediaStorage.StageUpload("lost-pets", normalizedContentType, bytes), null);
    }

    private static bool HasValidImageSignature(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8.ToArray()) && bytes[8..12].SequenceEqual("WEBP"u8.ToArray()),
        _ => false
    };

    private bool TryGetUserId(out int userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out userId);
    }

    private static DateTime AsUnspecified(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidCoordinates(double? latitude, double? longitude) =>
        (!latitude.HasValue && !longitude.HasValue) ||
        (latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180);
    private static double? RoundCoordinate(double? value) => value.HasValue ? Math.Round(value.Value, 3) : null;
    private static double? Distance(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue) return null;
        const double radius = 6371;
        static double Rad(double value) => value * Math.PI / 180;
        var dLat = Rad(lat2.Value - lat1.Value); var dLon = Rad(lon2.Value - lon1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1.Value)) * Math.Cos(Rad(lat2.Value)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return Math.Round(radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 1);
    }

    private sealed record ListingProjection(LostPetListing Listing, int SightingCount);
}

public sealed class MobileCreateLostPetRequest
{
    [Required, StringLength(10)] public string Kind { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 1)] public string PetName { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 2)] public string Species { get; init; } = string.Empty;
    [StringLength(100)] public string? Breed { get; init; }
    [Required, StringLength(1000, MinimumLength = 5)] public string DistinguishingFeatures { get; init; } = string.Empty;
    public DateTime EventAt { get; init; }
    [Required, StringLength(80, MinimumLength = 2)] public string City { get; init; } = string.Empty;
    [Required, StringLength(80, MinimumLength = 2)] public string District { get; init; } = string.Empty;
    [StringLength(120)] public string? Neighborhood { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    [StringLength(500)] public string? CollarOrMicrochip { get; init; }
    [StringLength(1500)] public string? Notes { get; init; }
    [Required] public string ImageBase64 { get; init; } = string.Empty;
    [Required, StringLength(100)] public string ImageContentType { get; init; } = string.Empty;
    public bool AllowInAppMessages { get; init; } = true;
    public bool SharePhone { get; init; }
    public bool ShareEmail { get; init; }
}

public sealed class MobileCreateLostPetSightingRequest
{
    [Required, StringLength(200, MinimumLength = 3)] public string LocationLabel { get; init; } = string.Empty;
    public DateTime SeenAt { get; init; }
    [StringLength(1000)] public string? Note { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

public sealed class MobileUpdateLostPetStatusRequest
{
    [Required, StringLength(20)] public string Status { get; init; } = string.Empty;
}

public sealed record MobileLostPetSummary(int Id, string Kind, string PetName, string Species, string? Breed,
    string City, string District, string? Neighborhood, double? Latitude, double? Longitude, DateTime EventAt,
    string ImagePath, string Status, DateTime CreatedAt, int SightingCount, double? DistanceKm);

public sealed record MobileLostPetSightingResponse(int Id, string LocationLabel, DateTime SeenAt, string? Note,
    double? Latitude, double? Longitude, DateTime CreatedAt);

public sealed record MobileLostPetDetail(int Id, string Kind, string PetName, string Species, string? Breed,
    string DistinguishingFeatures, DateTime EventAt, string City, string District, string? Neighborhood,
    double? Latitude, double? Longitude, string? CollarOrMicrochip, string? Notes, string ImagePath,
    string Status, string SourceName, DateTime CreatedAt, DateTime ExpiresAt, string OwnerUsername,
    bool IsMine, bool AllowInAppMessages, string? ContactPhone, string? ContactEmail,
    IReadOnlyList<MobileLostPetSightingResponse> Sightings);
