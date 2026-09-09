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

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/product-reviews")]
public sealed class MobileProductReviewsApiController : ControllerBase
{
    private readonly PetWorkDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly MobileMediaStorageService _mediaStorage;
    public MobileProductReviewsApiController(PetWorkDbContext context, IWebHostEnvironment environment, MobileMediaStorageService mediaStorage) { _context = context; _environment = environment; _mediaStorage = mediaStorage; }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MobileProductReviewResponse>>> GetReviews(CancellationToken cancellationToken)
    {
        var items = await _context.ProductReviews.AsNoTracking().Include(item => item.User)
            .Where(item => !item.IsDeleted).OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileProductReviewResponse>> CreateReview(MobileCreateProductReviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!ValidScore(request.TasteScore) || !ValidScore(request.IngredientScore) || !ValidScore(request.DigestionScore) || !ValidScore(request.ValueScore))
            return BadRequest(new { message = "Bütün puanlar 1 ile 5 arasında olmalıdır." });
        var image = SaveOptionalImage(request.ImageBase64, request.ImageContentType);
        if (image.Error is not null) return BadRequest(new { message = image.Error });
        var review = new ProductReview
        {
            UserId = userId, Brand = request.Brand.Trim(), ProductName = request.ProductName.Trim(), PetType = request.PetType.Trim(),
            Experience = request.Experience.Trim(), TasteScore = request.TasteScore, IngredientScore = request.IngredientScore,
            DigestionScore = request.DigestionScore, ValueScore = request.ValueScore, ImagePath = image.Path, CreatedAt = DateTime.Now
        };
        _context.ProductReviews.Add(review);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch { _mediaStorage.DiscardPending(review.ImagePath); DeleteUploadedImage(review.ImagePath); throw; }
        review.User = await _context.Users.AsNoTracking().FirstAsync(user => user.Id == userId, cancellationToken);
        return Ok(ToResponse(review));
    }

    [HttpPost("{id:int}/reports")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<IActionResult> Report(int id, MobileReportRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await _context.ProductReviews.AnyAsync(item => item.Id == id && !item.IsDeleted, cancellationToken))
            return NotFound(new { message = "Deneyim bulunamadı." });
        if (await _context.ProductReviewReports.AnyAsync(item => item.ProductReviewId == id && item.UserId == userId, cancellationToken))
            return Conflict(new { message = "Bu içeriği daha önce bildirdin." });
        _context.ProductReviewReports.Add(new ProductReviewReport { ProductReviewId = id, UserId = userId, Reason = request.Reason.Trim(), CreatedAt = DateTime.Now });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Bildirimin alındı." });
    }

    private static MobileProductReviewResponse ToResponse(ProductReview item)
    {
        var average = Math.Round((item.TasteScore + item.IngredientScore + item.DigestionScore + item.ValueScore) / 4d, 1);
        return new(item.Id, item.Brand, item.ProductName, item.PetType, item.Experience, item.TasteScore, item.IngredientScore,
            item.DigestionScore, item.ValueScore, average, item.ImagePath, item.User.Username, item.CreatedAt);
    }

    private (string? Path, string? Error) SaveOptionalImage(string? imageBase64, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(imageBase64)) return (null, null);
        if (imageBase64.Length > 11_500_000) return (null, "Fotoğraf en fazla 8 MB olabilir.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(imageBase64); } catch (FormatException) { return (null, "Fotoğraf verisi okunamadı."); }
        if (bytes.Length is <= 0 or > 8 * 1024 * 1024) return (null, "Fotoğraf en fazla 8 MB olabilir.");
        var extension = contentType?.ToLowerInvariant() switch { "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => null };
        if (extension is null || !ValidImage(bytes, extension)) return (null, "Yalnızca geçerli JPG, PNG veya WebP fotoğrafları yükleyebilirsin.");
        return (_mediaStorage.StageUpload("product-reviews", extension, contentType!.ToLowerInvariant(), bytes), null);
    }

    private void DeleteUploadedImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var root = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "product-reviews"));
        var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, path));
        if (fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }
    private bool TryGetUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
    private static bool ValidScore(int score) => score is >= 1 and <= 5;
    private static bool ValidImage(byte[] bytes, string extension) => extension switch
    {
        ".jpg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8.ToArray()) && bytes[8..12].SequenceEqual("WEBP"u8.ToArray()),
        _ => false
    };
}

public sealed class MobileCreateProductReviewRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Brand { get; init; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 2)] public string ProductName { get; init; } = string.Empty;
    [Required, StringLength(50, MinimumLength = 2)] public string PetType { get; init; } = string.Empty;
    [Required, StringLength(1500, MinimumLength = 10)] public string Experience { get; init; } = string.Empty;
    public int TasteScore { get; init; }
    public int IngredientScore { get; init; }
    public int DigestionScore { get; init; }
    public int ValueScore { get; init; }
    public string? ImageBase64 { get; init; }
    [StringLength(100)] public string? ImageContentType { get; init; }
}
public sealed record MobileProductReviewResponse(int Id, string Brand, string ProductName, string PetType, string Experience,
    int TasteScore, int IngredientScore, int DigestionScore, int ValueScore, double AverageScore, string? ImagePath,
    string Username, DateTime CreatedAt);
