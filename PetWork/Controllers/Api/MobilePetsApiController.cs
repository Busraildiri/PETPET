using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;

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
    private static readonly HashSet<string> AllowedCharacters = new(["Sakin", "Oyuncu", "Enerjik", "Uysal", "Çekingen", "Sosyal", "Koruyucu", "Bağımsız"], StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedCareOptions = new(["Özel bakım ihtiyacı yok", "Alerjisi var", "Düzenli ilaç kullanıyor", "Özel besleniyor", "Hareket desteği gerekiyor"], StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedCompatibility = new(["Uyumlu", "Kontrollü tanıştırılmalı", "Uyumsuz", "Bilinmiyor"], StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedTags = new(["Eğitimli", "İnsan canlısı", "Sessiz", "Aktif", "Kucak sever", "Oyun sever", "Bahçe sever", "Ev yaşamına uygun"], StringComparer.Ordinal);
    private static readonly Dictionary<string, HashSet<string>> CatAttributes = new(StringComparer.Ordinal)
    {
        ["hairLength"] = new(["Kısa", "Orta", "Uzun"], StringComparer.Ordinal),
        ["litterBoxHabit"] = new(["Alışkın", "Eğitimde", "Alışkın değil"], StringComparer.Ordinal),
        ["indoorOutdoor"] = new(["Yalnızca evde", "Ev ve dışarı", "Çoğunlukla dışarıda"], StringComparer.Ordinal)
    };
    private static readonly Dictionary<string, HashSet<string>> DogAttributes = new(StringComparer.Ordinal)
    {
        ["toiletHabit"] = new(["Eğitimli", "Eğitimde", "Eğitimsiz"], StringComparer.Ordinal),
        ["leashWalking"] = new(["Uyumlu", "Eğitimde", "Zorlanıyor"], StringComparer.Ordinal),
        ["exerciseNeed"] = new(["Düşük", "Orta", "Yüksek"], StringComparer.Ordinal)
    };

    private readonly PetWorkDbContext _context;
    private readonly MobileMediaStorageService _mediaStorage;

    public MobilePetsApiController(PetWorkDbContext context, MobileMediaStorageService mediaStorage)
    {
        _context = context;
        _mediaStorage = mediaStorage;
    }

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
    [RequestSizeLimit(12 * 1024 * 1024)]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobilePetResponse>> Create(
        MobilePetRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });

        var type = NormalizeType(request.Type);
        if (type is null) return BadRequest(new { message = "Lütfen geçerli bir hayvan türü seç." });
        var optionError = ValidateOptions(type, request);
        if (optionError is not null) return BadRequest(new { message = optionError });
        var name = request.Name.Trim();
        if (name.Length < 2) return BadRequest(new { message = "Pati adı en az 2 görünür karakter içermelidir." });
        var imageResult = SaveImage(request.ImageBase64, request.ImageContentType);
        if (imageResult.Error is not null) return BadRequest(new { message = imageResult.Error });

        var pet = new Pet
        {
            UserId = userId.Value,
            Name = name,
            Type = type,
            PetType = type,
            Breed = Clean(request.Breed),
            Gender = Clean(request.Gender),
            Description = Clean(request.Description),
            Character = Clean(request.Character),
            CareNotes = Clean(request.CareNotes),
            ChildCompatibility = Clean(request.ChildCompatibility),
            OtherPetCompatibility = Clean(request.OtherPetCompatibility),
            IsVaccinated = request.IsVaccinated,
            IsNeutered = request.IsNeutered,
            IsMicrochipped = request.IsMicrochipped,
            TagsCsv = NormalizeTags(request.Tags),
            ExtraAttributesJson = NormalizeExtraAttributes(type, request.ExtraAttributes),
            IsPublic = request.IsPublic,
            ProfileImage = imageResult.Path ?? "img/pet-default.jpg"
        };

        SetAge(pet, request.Age);

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync(cancellationToken);
        return Created(string.Empty, ToResponse(pet));
    }

    [HttpPut("{id:int}")]
    [RequestSizeLimit(12 * 1024 * 1024)]
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
        var optionError = ValidateOptions(type, request);
        if (optionError is not null) return BadRequest(new { message = optionError });
        var name = request.Name.Trim();
        if (name.Length < 2) return BadRequest(new { message = "Pati adı en az 2 görünür karakter içermelidir." });
        var imageResult = SaveImage(request.ImageBase64, request.ImageContentType);
        if (imageResult.Error is not null) return BadRequest(new { message = imageResult.Error });

        pet.Name = name;
        pet.Type = type;
        pet.PetType = type;
        pet.Breed = Clean(request.Breed);
        SetAge(pet, request.Age);
        pet.Gender = Clean(request.Gender);
        pet.Description = Clean(request.Description);
        pet.Character = Clean(request.Character);
        pet.CareNotes = Clean(request.CareNotes);
        pet.ChildCompatibility = Clean(request.ChildCompatibility);
        pet.OtherPetCompatibility = Clean(request.OtherPetCompatibility);
        pet.IsVaccinated = request.IsVaccinated;
        pet.IsNeutered = request.IsNeutered;
        pet.IsMicrochipped = request.IsMicrochipped;
        pet.TagsCsv = NormalizeTags(request.Tags);
        pet.ExtraAttributesJson = NormalizeExtraAttributes(type, request.ExtraAttributes);
        pet.IsPublic = request.IsPublic;
        if (imageResult.Path is not null)
        {
            var previousImage = pet.ProfileImage;
            pet.ProfileImage = imageResult.Path;
            await _mediaStorage.StageDeleteAsync(previousImage, cancellationToken);
        }

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
        await _mediaStorage.StageDeleteAsync(pet.ProfileImage, cancellationToken);
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

    private (string? Path, string? Error) SaveImage(string? imageBase64, string? contentType)
    {
        const int maximumImageSize = 8 * 1024 * 1024;
        if (string.IsNullOrWhiteSpace(imageBase64)) return (null, null);
        if (imageBase64.Length > 11_500_000) return (null, "Fotoğraf en fazla 8 MB olabilir.");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(imageBase64); }
        catch (FormatException) { return (null, "Fotoğraf verisi okunamadı."); }
        if (bytes.Length is <= 0 or > maximumImageSize) return (null, "Fotoğraf en fazla 8 MB olabilir.");

        try { return (_mediaStorage.StageUpload("pets", contentType ?? string.Empty, bytes), null); }
        catch (MediaValidationException exception) { return (null, exception.Message); }
    }

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

    private static string? NormalizeTags(IReadOnlyList<string>? tags)
    {
        var cleaned = tags?.Select(tag => tag.Trim().TrimStart('#')).Where(tag => tag.Length is > 0 and <= 30)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray() ?? [];
        return cleaned.Length == 0 ? null : string.Join(',', cleaned);
    }

    private static string? ValidateOptions(string type, MobilePetRequest request)
    {
        static bool CsvIsAllowed(string? value, HashSet<string> allowed) => string.IsNullOrWhiteSpace(value) ||
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(allowed.Contains);
        if (!CsvIsAllowed(request.Character, AllowedCharacters)) return "Geçersiz karakter seçeneği gönderildi.";
        if (!CsvIsAllowed(request.CareNotes, AllowedCareOptions)) return "Geçersiz bakım seçeneği gönderildi.";
        if (!string.IsNullOrWhiteSpace(request.ChildCompatibility) && !AllowedCompatibility.Contains(request.ChildCompatibility)) return "Geçersiz çocuk uyumu seçeneği gönderildi.";
        if (!string.IsNullOrWhiteSpace(request.OtherPetCompatibility) && !AllowedCompatibility.Contains(request.OtherPetCompatibility)) return "Geçersiz hayvan uyumu seçeneği gönderildi.";
        if (request.Tags?.Any(tag => !AllowedTags.Contains(tag.Trim())) == true) return "Geçersiz etiket seçeneği gönderildi.";
        if (request.ExtraAttributes is null) return null;
        var allowedAttributes = type == "Kedi" ? CatAttributes : type == "Köpek" ? DogAttributes : null;
        if (allowedAttributes is null && request.ExtraAttributes.Any(item => !string.IsNullOrWhiteSpace(item.Value))) return "Bu hayvan türü için ek özellik kullanılamaz.";
        if (allowedAttributes is not null && request.ExtraAttributes.Any(item => !string.IsNullOrWhiteSpace(item.Value) &&
            (!allowedAttributes.TryGetValue(item.Key, out var values) || !values.Contains(item.Value.Trim())))) return "Geçersiz tür özelliği gönderildi.";
        return null;
    }

    private static string? NormalizeExtraAttributes(string type, IReadOnlyDictionary<string, string>? attributes)
    {
        if (type is not ("Kedi" or "Köpek") || attributes is null) return null;
        var allowed = type == "Kedi" ? CatAttributes : DogAttributes;
        var cleaned = attributes.Where(item => allowed.ContainsKey(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => item.Value.Trim()[..Math.Min(item.Value.Trim().Length, 80)]);
        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
    }

    private static IReadOnlyDictionary<string, string> ReadExtraAttributes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new Dictionary<string, string>();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new Dictionary<string, string>(); }
        catch (JsonException) { return new Dictionary<string, string>(); }
    }

    private static MobilePetResponse ToResponse(Pet pet) =>
        new(pet.Id, pet.Name, pet.Type, pet.Breed, GetAge(pet), pet.Gender, pet.Description, pet.ProfileImage,
            pet.Character, pet.CareNotes, pet.ChildCompatibility, pet.OtherPetCompatibility, pet.IsVaccinated,
            pet.IsNeutered, pet.IsMicrochipped,
            string.IsNullOrWhiteSpace(pet.TagsCsv) ? [] : pet.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries),
            ReadExtraAttributes(pet.ExtraAttributesJson), pet.IsPublic);
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

    [StringLength(300)] public string? Character { get; init; }
    [StringLength(500)] public string? CareNotes { get; init; }
    [StringLength(30)] public string? ChildCompatibility { get; init; }
    [StringLength(30)] public string? OtherPetCompatibility { get; init; }
    public bool? IsVaccinated { get; init; }
    public bool? IsNeutered { get; init; }
    public bool? IsMicrochipped { get; init; }
    public List<string>? Tags { get; init; }
    public Dictionary<string, string>? ExtraAttributes { get; init; }
    public bool IsPublic { get; init; }

    [StringLength(11_500_000, ErrorMessage = "Fotoğraf verisi çok büyük.")]
    public string? ImageBase64 { get; init; }

    [StringLength(100, ErrorMessage = "Fotoğraf türü geçersiz.")]
    public string? ImageContentType { get; init; }
}

public sealed record MobilePetResponse(
    int Id,
    string Name,
    string Type,
    string? Breed,
    decimal? Age,
    string? Gender,
    string? Description,
    string? ProfileImage,
    string? Character,
    string? CareNotes,
    string? ChildCompatibility,
    string? OtherPetCompatibility,
    bool? IsVaccinated,
    bool? IsNeutered,
    bool? IsMicrochipped,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> ExtraAttributes,
    bool IsPublic);
