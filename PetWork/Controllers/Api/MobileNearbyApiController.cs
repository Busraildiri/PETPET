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
}
