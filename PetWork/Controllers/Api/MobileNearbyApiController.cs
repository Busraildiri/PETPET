using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PetWork.Services;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/nearby")]
public sealed class MobileNearbyApiController : ControllerBase
{
    private readonly GooglePlacesService _googlePlaces;
    private readonly ILogger<MobileNearbyApiController> _logger;

    public MobileNearbyApiController(GooglePlacesService googlePlaces, ILogger<MobileNearbyApiController> logger)
    {
        _googlePlaces = googlePlaces;
        _logger = logger;
    }

    [HttpGet("veterinarians")]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<IReadOnlyList<NearbyVeterinarian>>> GetVeterinarians(
        [FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] int radiusMeters = 5000,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return BadRequest(new { message = "Geçerli bir konum gönderilmelidir." });
        radiusMeters = Math.Clamp(radiusMeters, 1000, 20_000);
        try
        {
            return Ok(await _googlePlaces.SearchVeterinariansAsync(latitude, longitude, radiusMeters, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Google Places yapılandırması eksik.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Yakındaki veteriner hizmeti henüz yapılandırılmadı." });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Google Places veteriner araması başarısız oldu.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Veterinerler şu anda alınamadı. Lütfen tekrar dene." });
        }
    }

    [HttpGet("veterinarians/search")]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<IReadOnlyList<NearbyVeterinarian>>> SearchVeterinarians(
        [FromQuery] string? city, [FromQuery] string? district,
        CancellationToken cancellationToken = default)
    {
        city = city?.Trim();
        district = district?.Trim();
        if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(district))
            return BadRequest(new { message = "Şehir veya ilçe bilgilerinden en az birini yazmalısın." });
        if (city?.Length > 80 || district?.Length > 80)
            return BadRequest(new { message = "Şehir ve ilçe bilgisi 80 karakterden uzun olamaz." });
        try
        {
            return Ok(await _googlePlaces.SearchVeterinariansByAreaAsync(city, district, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Google Places yapılandırması eksik.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Yakındaki veteriner hizmeti henüz yapılandırılmadı." });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Google Places manuel veteriner araması başarısız oldu.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Bu konumdaki veterinerler şu anda alınamadı. Lütfen tekrar dene." });
        }
    }
}
