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
[Authorize]
[Route("api/mobile/pati-match")]
public sealed class MobilePatiMatchApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedPurposes = new(StringComparer.OrdinalIgnoreCase)
    {
        "friendship", "mate"
    };
    private static readonly string[] AllowedPetTypes = ["Kedi", "Köpek", "Kuş", "Tavşan", "Balık", "Diğer"];

    private readonly PetWorkDbContext _context;
    private readonly MobilePushNotificationService _pushNotifications;
    public MobilePatiMatchApiController(PetWorkDbContext context, MobilePushNotificationService pushNotifications)
    {
        _context = context;
        _pushNotifications = pushNotifications;
    }

    [HttpGet]
    public async Task<ActionResult<PatiMatchOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var pets = await _context.Pets.AsNoTracking()
            .Where(pet => pet.UserId == userId.Value)
            .OrderBy(pet => pet.Name)
            .ToListAsync(cancellationToken);

        var profiles = await _context.PatiMatchProfiles.AsNoTracking()
            .Where(profile => profile.Pet.UserId == userId.Value)
            .ToDictionaryAsync(profile => profile.PetId, cancellationToken);

        var petResponses = pets.Select(pet =>
        {
            profiles.TryGetValue(pet.Id, out var profile);
            return WithCalculatedMyPetAge(new PatiMatchMyPetResponse(
                pet.Id, pet.Name, pet.Type, pet.Breed, pet.DateOfBirth, pet.Age,
                pet.Gender, pet.Description, pet.ProfileImage,
                profile?.IsActive ?? false, profile?.Purpose, profile?.City, profile?.District,
                ParsePreferredTypes(profile?.PreferredTypesCsv, pet.Type)));
        }).ToList();

        var matchCount = await _context.PatiMatchDecisions
            .Where(decision => decision.SourcePet.UserId == userId.Value && decision.IsLike)
            .CountAsync(decision => _context.PatiMatchDecisions.Any(reverse =>
                reverse.SourcePetId == decision.TargetPetId &&
                reverse.TargetPetId == decision.SourcePetId &&
                reverse.IsLike), cancellationToken);

        return Ok(new PatiMatchOverviewResponse(petResponses, matchCount));
    }

    [HttpPost("enroll")]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<PatiMatchMyPetResponse>> Enroll(PatiMatchEnrollRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!request.AcceptSafetyTerms) return BadRequest(new { message = "PatiMatch güvenlik kurallarını kabul etmelisin." });
        var purpose = AllowedPurposes.FirstOrDefault(value => value.Equals(request.Purpose?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (purpose is null) return BadRequest(new { message = "Geçerli bir PatiMatch amacı seçmelisin." });

        var pet = await _context.Pets.FirstOrDefaultAsync(candidate => candidate.Id == request.PetId && candidate.UserId == userId.Value, cancellationToken);
        if (pet is null) return NotFound(new { message = "Pati profili bulunamadı." });
        if (string.IsNullOrWhiteSpace(pet.ProfileImage) || pet.ProfileImage == "img/pet-default.jpg")
            return BadRequest(new { message = "PatiMatch'e katılmadan önce patine gerçek bir fotoğraf eklemelisin." });

        var preferredTypes = purpose == "mate"
            ? [pet.Type]
            : NormalizePreferredTypes(request.PreferredTypes);
        if (preferredTypes.Length == 0)
            return BadRequest(new { message = "Karşına çıkmasını istediğin en az bir pati türü seçmelisin." });

        var profile = await _context.PatiMatchProfiles.FirstOrDefaultAsync(candidate => candidate.PetId == pet.Id, cancellationToken);
        if (profile is null)
        {
            profile = new PatiMatchProfile { PetId = pet.Id, CreatedAt = DateTime.Now };
            _context.PatiMatchProfiles.Add(profile);
        }

        profile.Purpose = purpose;
        profile.City = request.City.Trim();
        profile.District = Clean(request.District);
        profile.PreferredTypesCsv = string.Join(',', preferredTypes);
        profile.IsActive = true;
        profile.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToMyPet(pet, profile));
    }

    [HttpDelete("profiles/{petId:int}")]
    public async Task<IActionResult> Deactivate(int petId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        var profile = await _context.PatiMatchProfiles
            .Include(candidate => candidate.Pet)
            .FirstOrDefaultAsync(candidate => candidate.PetId == petId && candidate.Pet.UserId == userId.Value, cancellationToken);
        if (profile is null) return NotFound(new { message = "PatiMatch profili bulunamadı." });
        profile.IsActive = false;
        profile.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("candidates")]
    public async Task<ActionResult<IReadOnlyList<PatiMatchCandidateResponse>>> GetCandidates(int petId, CancellationToken cancellationToken)
    {
        var sourceProfile = await GetOwnedActiveProfile(petId, cancellationToken);
        if (sourceProfile is null) return NotFound(new { message = "Aktif PatiMatch profilin bulunamadı." });

        var decidedPetIds = _context.PatiMatchDecisions
            .Where(decision => decision.SourcePetId == petId)
            .Select(decision => decision.TargetPetId);
        var preferredTypes = ParsePreferredTypes(sourceProfile.PreferredTypesCsv, sourceProfile.Pet.Type);

        var candidates = await _context.PatiMatchProfiles.AsNoTracking()
            .Where(profile => profile.IsActive && profile.PetId != petId &&
                profile.Pet.UserId != sourceProfile.Pet.UserId &&
                profile.Purpose == sourceProfile.Purpose &&
                preferredTypes.Contains(profile.Pet.Type) &&
                (sourceProfile.Purpose != "mate" || profile.Pet.Type == sourceProfile.Pet.Type) &&
                !decidedPetIds.Contains(profile.PetId))
            .OrderByDescending(profile => profile.UpdatedAt)
            .Take(20)
            .Select(profile => new PatiMatchCandidateResponse(
                profile.Pet.Id, profile.Pet.UserId, profile.Pet.User.Username, profile.Pet.Name, profile.Pet.Type, profile.Pet.Breed,
                profile.Pet.DateOfBirth, profile.Pet.Age, profile.Pet.Gender,
                profile.Pet.Description, profile.Pet.ProfileImage,
                profile.Purpose, profile.City, profile.District))
            .ToListAsync(cancellationToken);

        return Ok(candidates.Select(WithCalculatedCandidateAge).ToList());
    }

    [HttpPost("decisions")]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<PatiMatchDecisionResponse>> Decide(PatiMatchDecisionRequest request, CancellationToken cancellationToken)
    {
        var sourceProfile = await GetOwnedActiveProfile(request.SourcePetId, cancellationToken);
        if (sourceProfile is null) return NotFound(new { message = "Aktif PatiMatch profilin bulunamadı." });
        if (request.SourcePetId == request.TargetPetId) return BadRequest(new { message = "Kendi patini değerlendiremezsin." });
        var preferredTypes = ParsePreferredTypes(sourceProfile.PreferredTypesCsv, sourceProfile.Pet.Type);

        var targetExists = await _context.PatiMatchProfiles.AnyAsync(profile =>
            profile.PetId == request.TargetPetId && profile.IsActive &&
            profile.Pet.UserId != sourceProfile.Pet.UserId && profile.Purpose == sourceProfile.Purpose &&
            preferredTypes.Contains(profile.Pet.Type) &&
            (sourceProfile.Purpose != "mate" || profile.Pet.Type == sourceProfile.Pet.Type), cancellationToken);
        if (!targetExists) return NotFound(new { message = "Bu PatiMatch profili artık aktif değil." });

        var decision = await _context.PatiMatchDecisions.FirstOrDefaultAsync(candidate =>
            candidate.SourcePetId == request.SourcePetId && candidate.TargetPetId == request.TargetPetId, cancellationToken);
        var reverseLiked = request.IsLike && await _context.PatiMatchDecisions.AnyAsync(reverse =>
            reverse.SourcePetId == request.TargetPetId && reverse.TargetPetId == request.SourcePetId && reverse.IsLike, cancellationToken);
        var wasMatched = decision?.IsLike == true && reverseLiked;
        if (decision is null)
        {
            decision = new PatiMatchDecision { SourcePetId = request.SourcePetId, TargetPetId = request.TargetPetId };
            _context.PatiMatchDecisions.Add(decision);
        }
        decision.IsLike = request.IsLike;
        decision.CreatedAt = DateTime.Now;
        var matched = request.IsLike && reverseLiked;
        var notifications = new List<MobileNotification>();
        if (matched && !wasMatched)
        {
            var pets = await _context.Pets.AsNoTracking()
                .Where(pet => pet.Id == request.SourcePetId || pet.Id == request.TargetPetId)
                .Select(pet => new { pet.Id, pet.Name, pet.UserId })
                .ToListAsync(cancellationToken);
            var sourcePet = pets.Single(pet => pet.Id == request.SourcePetId);
            var targetPet = pets.Single(pet => pet.Id == request.TargetPetId);
            notifications.Add(new MobileNotification
            {
                UserId = sourcePet.UserId, Type = "pati_match", Title = "Yeni PatiMatch!",
                Body = $"{sourcePet.Name} ile {targetPet.Name} eşleşti.", EntityType = "pati_match",
                EntityId = targetPet.Id, CreatedAt = DateTime.Now
            });
            notifications.Add(new MobileNotification
            {
                UserId = targetPet.UserId, Type = "pati_match", Title = "Yeni PatiMatch!",
                Body = $"{targetPet.Name} ile {sourcePet.Name} eşleşti.", EntityType = "pati_match",
                EntityId = sourcePet.Id, CreatedAt = DateTime.Now
            });
            _context.MobileNotifications.AddRange(notifications);
        }
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
            await _pushNotifications.SendAsync(notification, cancellationToken);
        return Ok(new PatiMatchDecisionResponse(matched));
    }

    [HttpGet("matches")]
    public async Task<ActionResult<IReadOnlyList<PatiMatchCandidateResponse>>> GetMatches(int petId, CancellationToken cancellationToken)
    {
        var sourceProfile = await GetOwnedActiveProfile(petId, cancellationToken);
        if (sourceProfile is null) return NotFound(new { message = "Aktif PatiMatch profilin bulunamadı." });

        var mutualPetIds = _context.PatiMatchDecisions
            .Where(decision => decision.SourcePetId == petId && decision.IsLike)
            .Where(decision => _context.PatiMatchDecisions.Any(reverse =>
                reverse.SourcePetId == decision.TargetPetId && reverse.TargetPetId == petId && reverse.IsLike))
            .Select(decision => decision.TargetPetId);

        var matches = await _context.PatiMatchProfiles.AsNoTracking()
            .Where(profile => profile.IsActive && mutualPetIds.Contains(profile.PetId))
            .OrderByDescending(profile => profile.UpdatedAt)
            .Select(profile => new PatiMatchCandidateResponse(
                profile.Pet.Id, profile.Pet.UserId, profile.Pet.User.Username, profile.Pet.Name, profile.Pet.Type, profile.Pet.Breed,
                profile.Pet.DateOfBirth, profile.Pet.Age, profile.Pet.Gender,
                profile.Pet.Description, profile.Pet.ProfileImage,
                profile.Purpose, profile.City, profile.District))
            .ToListAsync(cancellationToken);
        return Ok(matches.Select(WithCalculatedCandidateAge).ToList());
    }

    [HttpGet("messages")]
    public async Task<ActionResult<IReadOnlyList<PatiMatchMessageResponse>>> GetMessages(
        int sourcePetId,
        int targetPetId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!await HasOwnedMutualMatch(userId.Value, sourcePetId, targetPetId, cancellationToken))
            return Forbid();

        var petOneId = Math.Min(sourcePetId, targetPetId);
        var petTwoId = Math.Max(sourcePetId, targetPetId);
        var messages = await _context.PatiMatchMessages.AsNoTracking()
            .Where(message => message.PetOneId == petOneId && message.PetTwoId == petTwoId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(100)
            .Select(message => new PatiMatchMessageResponse(
                message.Id,
                message.Body,
                message.SenderUserId == userId.Value,
                message.SenderUser.Username,
                message.CreatedAt))
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return Ok(messages);
    }

    [HttpPost("messages")]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<PatiMatchMessageResponse>> SendMessage(
        PatiMatchMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!await HasOwnedMutualMatch(userId.Value, request.SourcePetId, request.TargetPetId, cancellationToken))
            return Forbid();

        var body = request.Body.Trim();
        if (body.Length is < 1 or > 1000)
            return BadRequest(new { message = "Mesaj 1-1000 karakter arasında olmalı." });

        var message = new PatiMatchMessage
        {
            PetOneId = Math.Min(request.SourcePetId, request.TargetPetId),
            PetTwoId = Math.Max(request.SourcePetId, request.TargetPetId),
            SenderUserId = userId.Value,
            Body = body,
            CreatedAt = DateTime.Now
        };
        _context.PatiMatchMessages.Add(message);
        var participants = await _context.Pets.AsNoTracking()
            .Where(pet => pet.Id == request.SourcePetId || pet.Id == request.TargetPetId)
            .Select(pet => new { pet.Id, pet.Name, pet.UserId, pet.User.Username })
            .ToListAsync(cancellationToken);
        var sourcePet = participants.Single(pet => pet.Id == request.SourcePetId);
        var targetPet = participants.Single(pet => pet.Id == request.TargetPetId);
        var notification = new MobileNotification
        {
            UserId = targetPet.UserId, Type = "pati_match_message", Title = $"{sourcePet.Name} tarafından yeni mesaj",
            Body = body.Length > 100 ? body[..100] + "…" : body, EntityType = "pati_match",
            EntityId = sourcePet.Id, CreatedAt = DateTime.Now
        };
        _context.MobileNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        await _pushNotifications.SendAsync(notification, cancellationToken);

        var username = sourcePet.Username;
        return Ok(new PatiMatchMessageResponse(message.Id, message.Body, true, username, message.CreatedAt));
    }

    [HttpGet("contact-share")]
    public async Task<ActionResult<PatiMatchContactShareStateResponse>> GetContactShare(
        int sourcePetId, int targetPetId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!await HasOwnedMutualMatch(userId.Value, sourcePetId, targetPetId, cancellationToken)) return Forbid();
        return Ok(await BuildContactShareState(userId.Value, sourcePetId, targetPetId, cancellationToken));
    }

    [HttpPost("contact-share")]
    [EnableRateLimiting("mobile-content")]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<PatiMatchContactShareStateResponse>> ShareContact(
        PatiMatchContactShareRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!await HasOwnedMutualMatch(userId.Value, request.SourcePetId, request.TargetPetId, cancellationToken)) return Forbid();

        var preference = await _context.MobileContactPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId.Value, cancellationToken);
        if (preference is null || !preference.AllowPatiMatchSharing)
            return BadRequest(new { message = "Önce Ayarlar > İletişim bilgileri bölümünden PatiMatch paylaşımına izin vermelisin." });
        if (string.IsNullOrWhiteSpace(preference.Phone) && string.IsNullOrWhiteSpace(preference.ContactEmail))
            return BadRequest(new { message = "Paylaşmak için Ayarlar bölümüne telefon veya e-posta eklemelisin." });

        var petOneId = Math.Min(request.SourcePetId, request.TargetPetId);
        var petTwoId = Math.Max(request.SourcePetId, request.TargetPetId);
        var existing = await _context.PatiMatchContactShares.AnyAsync(item =>
            item.PetOneId == petOneId && item.PetTwoId == petTwoId && item.SharedByUserId == userId.Value,
            cancellationToken);
        if (existing)
            return Conflict(new { message = "İletişim bilgilerini bu eşleşmeyle daha önce paylaştın. Tek seferlik paylaşım yeniden açılamaz." });

        var share = new PatiMatchContactShare
        {
            PetOneId = petOneId, PetTwoId = petTwoId, SharedByUserId = userId.Value,
            Phone = preference.Phone, ContactEmail = preference.ContactEmail, SharedAt = DateTime.Now
        };
        _context.PatiMatchContactShares.Add(share);
        var participants = await _context.Pets.AsNoTracking()
            .Where(pet => pet.Id == request.SourcePetId || pet.Id == request.TargetPetId)
            .Select(pet => new { pet.Id, pet.Name, pet.UserId })
            .ToListAsync(cancellationToken);
        var sourcePet = participants.Single(pet => pet.Id == request.SourcePetId);
        var targetPet = participants.Single(pet => pet.Id == request.TargetPetId);
        var notification = new MobileNotification
        {
            UserId = targetPet.UserId, Type = "pati_match_contact", Title = $"{sourcePet.Name} için iletişim bilgisi paylaşıldı",
            Body = "Eşleşme sohbetinde paylaşılan telefon veya e-posta bilgisini görebilirsin.",
            EntityType = "pati_match", EntityId = sourcePet.Id, CreatedAt = DateTime.Now
        };
        _context.MobileNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        await _pushNotifications.SendAsync(notification, cancellationToken);
        return Ok(await BuildContactShareState(userId.Value, request.SourcePetId, request.TargetPetId, cancellationToken));
    }

    [HttpDelete("contact-share")]
    public async Task<ActionResult<PatiMatchContactShareStateResponse>> RevokeContactShare(
        int sourcePetId, int targetPetId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!await HasOwnedMutualMatch(userId.Value, sourcePetId, targetPetId, cancellationToken)) return Forbid();

        var petOneId = Math.Min(sourcePetId, targetPetId);
        var petTwoId = Math.Max(sourcePetId, targetPetId);
        var share = await _context.PatiMatchContactShares.FirstOrDefaultAsync(item =>
            item.PetOneId == petOneId && item.PetTwoId == petTwoId &&
            item.SharedByUserId == userId.Value && item.RevokedAt == null, cancellationToken);
        if (share is null) return NotFound(new { message = "Aktif iletişim paylaşımı bulunamadı." });
        share.RevokedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(await BuildContactShareState(userId.Value, sourcePetId, targetPetId, cancellationToken));
    }

    private async Task<PatiMatchContactShareStateResponse> BuildContactShareState(
        int userId, int sourcePetId, int targetPetId, CancellationToken cancellationToken)
    {
        var petOneId = Math.Min(sourcePetId, targetPetId);
        var petTwoId = Math.Max(sourcePetId, targetPetId);
        var shares = await _context.PatiMatchContactShares.AsNoTracking()
            .Where(item => item.PetOneId == petOneId && item.PetTwoId == petTwoId)
            .Select(item => new { item.SharedByUserId, item.SharedByUser.Username, item.Phone, item.ContactEmail, item.SharedAt, item.RevokedAt })
            .ToListAsync(cancellationToken);
        var mine = shares.FirstOrDefault(item => item.SharedByUserId == userId);
        var peer = shares.FirstOrDefault(item => item.SharedByUserId != userId && item.RevokedAt == null);
        var myStatus = mine is null ? "none" : mine.RevokedAt is null ? "shared" : "revoked";
        return new PatiMatchContactShareStateResponse(myStatus,
            mine is { RevokedAt: null } ? new PatiMatchSharedContactResponse(mine.Username, mine.Phone, mine.ContactEmail, mine.SharedAt) : null,
            peer is null ? null : new PatiMatchSharedContactResponse(peer.Username, peer.Phone, peer.ContactEmail, peer.SharedAt));
    }

    private async Task<bool> HasOwnedMutualMatch(
        int userId,
        int sourcePetId,
        int targetPetId,
        CancellationToken cancellationToken)
    {
        if (sourcePetId == targetPetId) return false;

        var ownsSource = await _context.PatiMatchProfiles.AnyAsync(profile =>
            profile.PetId == sourcePetId && profile.IsActive && profile.Pet.UserId == userId,
            cancellationToken);
        if (!ownsSource) return false;

        var targetIsActive = await _context.PatiMatchProfiles.AnyAsync(profile =>
            profile.PetId == targetPetId && profile.IsActive && profile.Pet.UserId != userId,
            cancellationToken);
        if (!targetIsActive) return false;

        return await _context.PatiMatchDecisions.AnyAsync(decision =>
                decision.SourcePetId == sourcePetId && decision.TargetPetId == targetPetId && decision.IsLike,
                cancellationToken)
            && await _context.PatiMatchDecisions.AnyAsync(decision =>
                decision.SourcePetId == targetPetId && decision.TargetPetId == sourcePetId && decision.IsLike,
                cancellationToken);
    }

    private async Task<PatiMatchProfile?> GetOwnedActiveProfile(int petId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return null;
        return await _context.PatiMatchProfiles.Include(profile => profile.Pet)
            .FirstOrDefaultAsync(profile => profile.PetId == petId && profile.IsActive && profile.Pet.UserId == userId.Value, cancellationToken);
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] NormalizePreferredTypes(IEnumerable<string>? values)
    {
        if (values is null) return [];
        return values.Select(value => AllowedPetTypes.FirstOrDefault(allowed =>
                allowed.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ParsePreferredTypes(string? csv, string fallbackType)
    {
        var parsed = NormalizePreferredTypes(csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return parsed.Length > 0 ? parsed : [fallbackType];
    }

    private static decimal? CalculateAge(DateTime? dateOfBirth, int? storedAge)
    {
        if (dateOfBirth is null) return storedAge;
        var today = DateTime.UtcNow.Date;
        var birthDate = dateOfBirth.Value.Date;
        var months = (today.Year - birthDate.Year) * 12 + today.Month - birthDate.Month;
        if (today.Day < birthDate.Day) months--;
        return Math.Round(Math.Max(0, months) / 12m, 1, MidpointRounding.AwayFromZero);
    }

    private static PatiMatchMyPetResponse WithCalculatedMyPetAge(PatiMatchMyPetResponse pet) => pet with { Age = CalculateAge(pet.DateOfBirth, pet.StoredAge) };
    private static PatiMatchCandidateResponse WithCalculatedCandidateAge(PatiMatchCandidateResponse pet) => pet with { Age = CalculateAge(pet.DateOfBirth, pet.StoredAge) };
    private static PatiMatchMyPetResponse ToMyPet(Pet pet, PatiMatchProfile profile) => WithCalculatedMyPetAge(new(
        pet.Id, pet.Name, pet.Type, pet.Breed, pet.DateOfBirth, pet.Age, pet.Gender, pet.Description,
        pet.ProfileImage, profile.IsActive, profile.Purpose, profile.City, profile.District,
        ParsePreferredTypes(profile.PreferredTypesCsv, pet.Type)));
}

public sealed class PatiMatchEnrollRequest
{
    [Range(1, int.MaxValue)] public int PetId { get; init; }
    [Required, StringLength(20)] public string Purpose { get; init; } = "friendship";
    [Required, StringLength(80, MinimumLength = 2)] public string City { get; init; } = string.Empty;
    [StringLength(80)] public string? District { get; init; }
    [MinLength(1), MaxLength(6)] public IReadOnlyList<string> PreferredTypes { get; init; } = [];
    public bool AcceptSafetyTerms { get; init; }
}

public sealed class PatiMatchDecisionRequest
{
    [Range(1, int.MaxValue)] public int SourcePetId { get; init; }
    [Range(1, int.MaxValue)] public int TargetPetId { get; init; }
    public bool IsLike { get; init; }
}

public sealed class PatiMatchMessageRequest
{
    [Range(1, int.MaxValue)] public int SourcePetId { get; init; }
    [Range(1, int.MaxValue)] public int TargetPetId { get; init; }
    [Required, StringLength(1000, MinimumLength = 1)] public string Body { get; init; } = string.Empty;
}

public sealed class PatiMatchContactShareRequest
{
    [Range(1, int.MaxValue)] public int SourcePetId { get; init; }
    [Range(1, int.MaxValue)] public int TargetPetId { get; init; }
}

public sealed record PatiMatchOverviewResponse(IReadOnlyList<PatiMatchMyPetResponse> Pets, int MatchCount);
public sealed record PatiMatchMyPetResponse(
    int Id, string Name, string Type, string? Breed, DateTime? DateOfBirth, int? StoredAge,
    string? Gender, string? Description, string? ProfileImage, bool IsActive,
    string? Purpose, string? City, string? District, IReadOnlyList<string> PreferredTypes)
{
    public decimal? Age { get; init; }
}
public sealed record PatiMatchCandidateResponse(
    int PetId, int OwnerUserId, string OwnerUsername, string Name, string Type, string? Breed, DateTime? DateOfBirth, int? StoredAge,
    string? Gender, string? Description, string? ProfileImage,
    string Purpose, string City, string? District)
{
    public decimal? Age { get; init; }
}
public sealed record PatiMatchDecisionResponse(bool Matched);
public sealed record PatiMatchMessageResponse(long Id, string Body, bool IsMine, string Username, DateTime CreatedAt);
public sealed record PatiMatchSharedContactResponse(string Username, string? Phone, string? Email, DateTime SharedAt);
public sealed record PatiMatchContactShareStateResponse(string MyStatus, PatiMatchSharedContactResponse? Mine, PatiMatchSharedContactResponse? Peer);
