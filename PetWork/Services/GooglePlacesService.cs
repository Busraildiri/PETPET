using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;
using System.Text.Json;

namespace PetWork.Services;

public sealed class GooglePlacesService
{
    private const string FieldMask =
        "places.id,places.displayName,places.formattedAddress,places.location," +
        "places.rating,places.userRatingCount,places.currentOpeningHours.openNow,places.googleMapsUri";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string _apiKey;

    public GooglePlacesService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
        _apiKey = configuration["GooglePlaces:ApiKey"] ?? string.Empty;
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchVeterinariansAsync(
        double latitude, double longitude, int radiusMeters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var cacheKey = $"google-vets:{latitude:F3}:{longitude:F3}:{radiusMeters}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NearbyVeterinarian>? cached) && cached is not null)
            return cached;

        var body = new
        {
            includedTypes = new[] { "veterinary_care" }, maxResultCount = 20, rankPreference = "DISTANCE",
            languageCode = "tr", regionCode = "TR",
            locationRestriction = new { circle = new { center = new { latitude, longitude }, radius = radiusMeters } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchNearby") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Google Places isteği başarısız oldu ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var results = new List<NearbyVeterinarian>();
        if (document.RootElement.TryGetProperty("places", out var places))
        {
            foreach (var place in places.EnumerateArray())
            {
                if (!place.TryGetProperty("location", out var location)) continue;
                var placeLatitude = location.GetProperty("latitude").GetDouble();
                var placeLongitude = location.GetProperty("longitude").GetDouble();
                var displayName = place.TryGetProperty("displayName", out var displayNameElement)
                    && displayNameElement.TryGetProperty("text", out var displayNameText)
                    ? displayNameText.GetString() ?? "Veteriner kliniği" : "Veteriner kliniği";

                results.Add(new NearbyVeterinarian(
                    place.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"), displayName,
                    place.TryGetProperty("formattedAddress", out var address) ? address.GetString() : null,
                    CalculateDistanceMeters(latitude, longitude, placeLatitude, placeLongitude),
                    place.TryGetProperty("rating", out var rating) ? rating.GetDouble() : null,
                    place.TryGetProperty("userRatingCount", out var ratingCount) ? ratingCount.GetInt32() : null,
                    place.TryGetProperty("currentOpeningHours", out var hours) && hours.TryGetProperty("openNow", out var openNow) ? openNow.GetBoolean() : null,
                    place.TryGetProperty("googleMapsUri", out var mapsUri) ? mapsUri.GetString() : null,
                    placeLatitude, placeLongitude));
            }
        }

        var ordered = results.OrderBy(place => place.DistanceMeters).ToArray();
        _cache.Set(cacheKey, ordered, TimeSpan.FromMinutes(15));
        return ordered;
    }

    private static int CalculateDistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusMeters = 6_371_000;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var a = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(DegreesToRadians(latitude1)) * Math.Cos(DegreesToRadians(latitude2))
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        return (int)Math.Round(earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}

public sealed record NearbyVeterinarian(string Id, string Name, string? Address, int DistanceMeters,
    double? Rating, int? UserRatingCount, bool? OpenNow, string? GoogleMapsUri, double Latitude, double Longitude);
