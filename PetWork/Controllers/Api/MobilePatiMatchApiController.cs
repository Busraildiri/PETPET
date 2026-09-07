using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

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
    public MobilePatiMatchApiController(PetWorkDbContext context) => _context = context;

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
                profile.Pet.Id, profile.Pet.Name, profile.Pet.Type, profile.Pet.Breed,
                profile.Pet.DateOfBirth, profile.Pet.Age, profile.Pet.Gender,
                profile.Pet.Description, profile.Pet.ProfileImage,
                profile.Purpose, profile.City, profile.District))
            .ToListAsync(cancellationToken);

        return Ok(candidates.Select(WithCalculatedCandidateAge).ToList());
    }

    [HttpPost("decisions")]
    [EnableRateLimiting("mobile-content")]
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
        if (decision is null)
        {
            decision = new PatiMatchDecision { SourcePetId = request.SourcePetId, TargetPetId = request.TargetPetId };
            _context.PatiMatchDecisions.Add(decision);
        }
        decision.IsLike = request.IsLike;
        decision.CreatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        var matched = request.IsLike && await _context.PatiMatchDecisions.AnyAsync(reverse =>
            reverse.SourcePetId == request.TargetPetId && reverse.TargetPetId == request.SourcePetId && reverse.IsLike, cancellationToken);
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
                profile.Pet.Id, profile.Pet.Name, profile.Pet.Type, profile.Pet.Breed,
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
        await _context.SaveChangesAsync(cancellationToken);

        var username = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId.Value)
            .Select(user => user.Username)
            .SingleAsync(cancellationToken);
        return Ok(new PatiMatchMessageResponse(message.Id, message.Body, true, username, message.CreatedAt));
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

public sealed record PatiMatchOverviewResponse(IReadOnlyList<PatiMatchMyPetResponse> Pets, int MatchCount);
public sealed record PatiMatchMyPetResponse(
    int Id, string Name, string Type, string? Breed, DateTime? DateOfBirth, int? StoredAge,
    string? Gender, string? Description, string? ProfileImage, bool IsActive,
    string? Purpose, string? City, string? District, IReadOnlyList<string> PreferredTypes)
{
    public decimal? Age { get; init; }
}
public sealed record PatiMatchCandidateResponse(
    int PetId, string Name, string Type, string? Breed, DateTime? DateOfBirth, int? StoredAge,
    string? Gender, string? Description, string? ProfileImage,
    string Purpose, string City, string? District)
{
    public decimal? Age { get; init; }
}
public sealed record PatiMatchDecisionResponse(bool Matched);
public sealed record PatiMatchMessageResponse(long Id, string Body, bool IsMine, string Username, DateTime CreatedAt);
