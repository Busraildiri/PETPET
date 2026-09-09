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
[Route("api/mobile/pets")]
public sealed class MobilePetsApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Kedi", "Köpek", "Kuş", "Tavşan", "Balık", "Diğer"
    };

    private readonly PetWorkDbContext _context;

    public MobilePetsApiController(PetWorkDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MobilePetResponse>>> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var pets = await _context.Pets.AsNoTracking()
            .Where(pet => pet.UserId == userId.Value)
            .OrderBy(pet => pet.Name)
            .ToListAsync(cancellationToken);

        return Ok(pets.Select(ToResponse));
    }

    [HttpPost]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobilePetResponse>> Create(
        MobilePetRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var type = NormalizeType(request.Type);
        if (type is null) return BadRequest(new { message = "Lütfen geçerli bir hayvan türü seç." });
        var name = request.Name.Trim();
        if (name.Length < 2) return BadRequest(new { message = "Pati adı en az 2 görünür karakter içermelidir." });

        var pet = new Pet
        {
            UserId = userId.Value,
            Name = name,
            Type = type,
            PetType = type,
            Breed = Clean(request.Breed),
            Gender = Clean(request.Gender),
            Description = Clean(request.Description),
            ProfileImage = "img/pet-default.jpg"
        };

        SetAge(pet, request.Age);

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync(cancellationToken);
        return Created(string.Empty, ToResponse(pet));
    }

    [HttpPut("{id:int}")]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobilePetResponse>> Update(
        int id,
        MobilePetRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var pet = await _context.Pets
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value, cancellationToken);
        if (pet is null) return NotFound(new { message = "Pati profili bulunamadı." });

        var type = NormalizeType(request.Type);
        if (type is null) return BadRequest(new { message = "Lütfen geçerli bir hayvan türü seç." });
        var name = request.Name.Trim();
        if (name.Length < 2) return BadRequest(new { message = "Pati adı en az 2 görünür karakter içermelidir." });

        pet.Name = name;
        pet.Type = type;
        pet.PetType = type;
        pet.Breed = Clean(request.Breed);
        SetAge(pet, request.Age);
        pet.Gender = Clean(request.Gender);
        pet.Description = Clean(request.Description);

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(pet));
    }

    [HttpDelete("{id:int}")]
    [EnableRateLimiting("mobile-content")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var pet = await _context.Pets
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId.Value, cancellationToken);
        if (pet is null) return NotFound(new { message = "Pati profili bulunamadı." });

        var matchMessages = await _context.PatiMatchMessages
            .Where(item => item.PetOneId == id || item.PetTwoId == id)
            .ToListAsync(cancellationToken);
        var matchDecisions = await _context.PatiMatchDecisions
            .Where(item => item.SourcePetId == id || item.TargetPetId == id)
            .ToListAsync(cancellationToken);
        var matchProfile = await _context.PatiMatchProfiles
            .FirstOrDefaultAsync(item => item.PetId == id, cancellationToken);

        _context.PatiMatchMessages.RemoveRange(matchMessages);
        _context.PatiMatchDecisions.RemoveRange(matchDecisions);
        if (matchProfile is not null) _context.PatiMatchProfiles.Remove(matchProfile);
        _context.Pets.Remove(pet);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static string? NormalizeType(string value) =>
        AllowedTypes.FirstOrDefault(type => type.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void SetAge(Pet pet, decimal? age)
    {
        if (age is null)
        {
            pet.Age = null;
            pet.DateOfBirth = null;
            return;
        }

        var monthCount = (int)Math.Round(age.Value * 12m, MidpointRounding.AwayFromZero);
        pet.Age = (int)Math.Floor(age.Value);
        pet.DateOfBirth = DateTime.UtcNow.Date.AddMonths(-monthCount);
    }

    private static decimal? GetAge(Pet pet)
    {
        if (pet.DateOfBirth is null) return pet.Age;

        var today = DateTime.UtcNow.Date;
        var birthDate = pet.DateOfBirth.Value.Date;
        var monthCount = (today.Year - birthDate.Year) * 12 + today.Month - birthDate.Month;
        if (today.Day < birthDate.Day) monthCount--;
        return Math.Round(Math.Max(0, monthCount) / 12m, 1, MidpointRounding.AwayFromZero);
    }

    private static MobilePetResponse ToResponse(Pet pet) =>
        new(pet.Id, pet.Name, pet.Type, pet.Breed, GetAge(pet), pet.Gender, pet.Description, pet.ProfileImage);
}

public sealed class MobilePetRequest
{
    [Required(ErrorMessage = "Patinin adı gereklidir.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Pati adı 2-50 karakter arasında olmalıdır.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Hayvan türü seçmelisin.")]
    [StringLength(30)]
    public string Type { get; init; } = string.Empty;

    [StringLength(80)]
    public string? Breed { get; init; }

    [Range(typeof(decimal), "0", "80", ErrorMessage = "Yaş 0-80 arasında olmalıdır.")]
    public decimal? Age { get; init; }

    [StringLength(20)]
    public string? Gender { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }
}

public sealed record MobilePetResponse(
    int Id,
    string Name,
    string Type,
    string? Breed,
    decimal? Age,
    string? Gender,
    string? Description,
    string? ProfileImage);
