using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/profiles")]
public sealed class MobilePublicProfilesApiController(PetWorkDbContext context) : ControllerBase
{
    [HttpGet("{userId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MobileVisibleUserProfileResponse>> GetPublic(
        int userId, CancellationToken cancellationToken)
    {
        var profile = await BuildProfile(userId, null, false, cancellationToken);
        return profile is null ? NotFound(new { message = "Kullanıcı profili bulunamadı." }) : Ok(profile);
    }

    [HttpGet("match/{sourcePetId:int}/{targetPetId:int}")]
    [Authorize]
    public async Task<ActionResult<MobileVisibleUserProfileResponse>> GetForMatch(
        int sourcePetId, int targetPetId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var sourceOwned = await context.Pets.AsNoTracking()
            .AnyAsync(pet => pet.Id == sourcePetId && pet.UserId == userId.Value, cancellationToken);
        var mutualMatch = sourceOwned && await context.PatiMatchDecisions.AsNoTracking()
            .AnyAsync(decision => decision.SourcePetId == sourcePetId && decision.TargetPetId == targetPetId && decision.IsLike, cancellationToken)
            && await context.PatiMatchDecisions.AsNoTracking()
                .AnyAsync(decision => decision.SourcePetId == targetPetId && decision.TargetPetId == sourcePetId && decision.IsLike, cancellationToken);
        if (!mutualMatch) return StatusCode(StatusCodes.Status403Forbidden,
            new { message = "Bu profil yalnızca aktif eşleşmenin taraflarına açıktır." });

        var target = await context.Pets.AsNoTracking()
            .Where(pet => pet.Id == targetPetId)
            .Select(pet => new { pet.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null) return NotFound(new { message = "Eşleşen pati bulunamadı." });

        var profile = await BuildProfile(target.UserId, targetPetId, true, cancellationToken);
        return profile is null ? NotFound(new { message = "Kullanıcı profili bulunamadı." }) : Ok(profile);
    }

    private async Task<MobileVisibleUserProfileResponse?> BuildProfile(
        int userId, int? matchedPetId, bool matchAccess, CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.Id, candidate.Username, candidate.ProfileImage, candidate.Bio })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        var pets = await context.Pets.AsNoTracking()
            .Where(pet => pet.UserId == userId && (pet.IsPublic || (matchAccess && pet.Id == matchedPetId)))
            .OrderByDescending(pet => pet.Id == matchedPetId)
            .ThenBy(pet => pet.Name)
            .ToListAsync(cancellationToken);

        return new MobileVisibleUserProfileResponse(
            user.Id, user.Username, user.ProfileImage, user.Bio,
            pets.Select(pet => ToVisiblePet(pet, pet.Id == matchedPetId)).ToList());
    }

    private static MobileVisiblePetResponse ToVisiblePet(Pet pet, bool isMatchedPet) => new(
        pet.Id, pet.Name, pet.Type, pet.Breed, GetAge(pet), pet.Gender, pet.Description,
        pet.ProfileImage, pet.Character, pet.CareNotes, pet.ChildCompatibility,
        pet.OtherPetCompatibility, pet.IsVaccinated, pet.IsNeutered, pet.IsMicrochipped,
        string.IsNullOrWhiteSpace(pet.TagsCsv) ? [] : pet.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ReadAttributes(pet.ExtraAttributesJson), isMatchedPet);

    private static decimal? GetAge(Pet pet)
    {
        if (pet.DateOfBirth is null) return pet.Age;
        var today = DateTime.UtcNow.Date;
        var birth = pet.DateOfBirth.Value.Date;
        var months = (today.Year - birth.Year) * 12 + today.Month - birth.Month - (today.Day < birth.Day ? 1 : 0);
        return Math.Round(Math.Max(0, months) / 12m, 1, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyDictionary<string, string> ReadAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(); }
        catch (JsonException) { return new Dictionary<string, string>(); }
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed record MobileVisibleUserProfileResponse(
    int UserId, string Username, string? ProfileImage, string? Bio,
    IReadOnlyList<MobileVisiblePetResponse> Pets);

public sealed record MobileVisiblePetResponse(
    int Id, string Name, string Type, string? Breed, decimal? Age, string? Gender,
    string? Description, string? ProfileImage, string? Character, string? CareNotes,
    string? ChildCompatibility, string? OtherPetCompatibility, bool? IsVaccinated,
    bool? IsNeutered, bool? IsMicrochipped, IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> ExtraAttributes, bool IsMatchedPet);
