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
            .Select(pet => new MobilePetResponse(
                pet.Id, pet.Name, pet.Type, pet.Breed, pet.Age, pet.Gender,
                pet.Description, pet.ProfileImage))
            .ToListAsync(cancellationToken);

        return Ok(pets);
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

        var pet = new Pet
        {
            UserId = userId.Value,
            Name = request.Name.Trim(),
            Type = type,
            PetType = type,
            Breed = Clean(request.Breed),
            Age = request.Age,
            Gender = Clean(request.Gender),
            Description = Clean(request.Description),
            ProfileImage = "img/pet-default.jpg"
        };

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

        pet.Name = request.Name.Trim();
        pet.Type = type;
        pet.PetType = type;
        pet.Breed = Clean(request.Breed);
        pet.Age = request.Age;
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

    private static MobilePetResponse ToResponse(Pet pet) =>
        new(pet.Id, pet.Name, pet.Type, pet.Breed, pet.Age, pet.Gender, pet.Description, pet.ProfileImage);
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

    [Range(0, 80, ErrorMessage = "Yaş 0-80 arasında olmalıdır.")]
    public int? Age { get; init; }

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
    int? Age,
    string? Gender,
    string? Description,
    string? ProfileImage);
