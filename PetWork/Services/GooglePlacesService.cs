using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;
using System.Text.Json;

namespace PetWork.Services;

public sealed class GooglePlacesService
{
    private static readonly HashSet<string> TurkeyCities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Aksaray", "Amasya", "Ankara", "Antalya", "Ardahan", "Artvin",
        "Aydın", "Balıkesir", "Bartın", "Batman", "Bayburt", "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa",
        "Çanakkale", "Çankırı", "Çorum", "Denizli", "Diyarbakır", "Düzce", "Edirne", "Elazığ", "Erzincan", "Erzurum",
        "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkari", "Hatay", "Iğdır", "Isparta", "İstanbul", "İzmir",
        "Kahramanmaraş", "Karabük", "Karaman", "Kars", "Kastamonu", "Kayseri", "Kilis", "Kırıkkale", "Kırklareli",
        "Kırşehir", "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Mardin", "Mersin", "Muğla", "Muş", "Nevşehir",
        "Niğde", "Ordu", "Osmaniye", "Rize", "Sakarya", "Samsun", "Şanlıurfa", "Siirt", "Sinop", "Sivas", "Şırnak",
        "Tekirdağ", "Tokat", "Trabzon", "Tunceli", "Uşak", "Van", "Yalova", "Yozgat", "Zonguldak"
    };
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

    public async Task<IReadOnlyList<LocationSuggestion>> SearchLocationSuggestionsAsync(
        string input, string kind, string? city, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        input = input.Trim();
        kind = kind.Trim().ToLowerInvariant();
        city = city?.Trim();
        if (input.Length < 2) return Array.Empty<LocationSuggestion>();
        if (kind is not ("city" or "district"))
            throw new ArgumentException("Konum türü city veya district olmalıdır.");
        if (kind == "district" && string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("İlçe araması için önce şehir seçilmelidir.");

        var cacheKey = $"google-location:{kind}:{city?.ToLowerInvariant()}:{input.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<LocationSuggestion>? cached) && cached is not null)
            return cached;

        var body = new
        {
            input,
            includedPrimaryTypes = new[] { kind == "city" ? "(cities)" : "(regions)" },
            includedRegionCodes = new[] { "tr" },
            languageCode = "tr",
            regionCode = "TR"
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:autocomplete")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask",
            "suggestions.placePrediction.placeId,suggestions.placePrediction.text," +
            "suggestions.placePrediction.structuredFormat,suggestions.placePrediction.types");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Google Places konum önerisi isteği başarısız oldu ({(int)response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var results = new List<LocationSuggestion>();
        if (document.RootElement.TryGetProperty("suggestions", out var suggestions))
        {
            foreach (var suggestion in suggestions.EnumerateArray())
            {
                if (!suggestion.TryGetProperty("placePrediction", out var prediction)) continue;
                var placeId = prediction.TryGetProperty("placeId", out var idElement) ? idElement.GetString() : null;
                var mainText = ReadNestedText(prediction, "structuredFormat", "mainText") ?? ReadNestedText(prediction, "text");
                var secondaryText = ReadNestedText(prediction, "structuredFormat", "secondaryText");
                if (string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(mainText)) continue;
                if (kind == "city" && !TurkeyCities.Contains(mainText.Trim())) continue;
                if (kind == "district" && secondaryText?.Contains(city!, StringComparison.OrdinalIgnoreCase) != true) continue;
                results.Add(new LocationSuggestion(placeId, mainText.Trim(), secondaryText?.Trim(),
                    string.IsNullOrWhiteSpace(secondaryText) ? mainText.Trim() : $"{mainText.Trim()}, {secondaryText.Trim()}"));
                if (results.Count == 6) break;
            }
        }

        var unique = results.DistinctBy(item => item.Id).ToArray();
        _cache.Set(cacheKey, unique, TimeSpan.FromMinutes(20));
        return unique;
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchVeterinariansAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var body = new
        {
            includedTypes = new[] { "veterinary_care" },
            maxResultCount = 20,
            rankPreference = "DISTANCE",
            languageCode = "tr",
            regionCode = "TR",
            locationBias = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = radiusMeters
                }
            }
        };

        return await SendSearchAsync(
            "v1/places:searchNearby", body, latitude, longitude, cancellationToken);
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchVeterinariansByAreaAsync(
        string? city,
        string? district,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var area = string.Join(", ", new[] { district?.Trim(), city?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(area))
            throw new ArgumentException("Şehir veya ilçe bilgilerinden en az biri gereklidir.");

        var cacheKey = $"google-vets-area:{area.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NearbyVeterinarian>? cached) && cached is not null)
            return cached;

        var body = new
        {
            textQuery = $"{area} veteriner klinikleri",
            includedType = "veterinary_care",
            maxResultCount = 20,
            languageCode = "tr",
            regionCode = "TR"
        };

        var results = await SendSearchAsync(
            "v1/places:searchText", body, null, null, cancellationToken);
        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));
        return results;
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchGroomersAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var body = new
        {
            textQuery = "pet kuaförü kedi köpek tıraşı",
            includedType = "pet_care",
            strictTypeFiltering = true,
            maxResultCount = 20,
            languageCode = "tr",
            regionCode = "TR",
            locationBias = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = radiusMeters
                }
            }
        };

        return await SendSearchAsync(
            "v1/places:searchText", body, latitude, longitude, cancellationToken);
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchGroomersByAreaAsync(
        string? city,
        string? district,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var area = string.Join(", ", new[] { district?.Trim(), city?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(area))
            throw new ArgumentException("Şehir veya ilçe bilgilerinden en az biri gereklidir.");

        var cacheKey = $"google-groomers-area:{area.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NearbyVeterinarian>? cached) && cached is not null)
            return cached;

        var body = new
        {
            textQuery = $"{area} pet kuaförü kedi köpek tıraşı",
            includedType = "pet_care",
            strictTypeFiltering = true,
            maxResultCount = 20,
            languageCode = "tr",
            regionCode = "TR"
        };

        var results = await SendSearchAsync(
            "v1/places:searchText", body, null, null, cancellationToken);
        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));
        return results;
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchPetHotelsAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var body = new
        {
            textQuery = "pet oteli köpek oteli kedi pansiyonu",
            includedType = "pet_boarding_service",
            strictTypeFiltering = true,
            maxResultCount = 20,
            languageCode = "tr",
            regionCode = "TR",
            locationBias = new
            {
                circle = new
                {
                    center = new { latitude, longitude },
                    radius = radiusMeters
                }
            }
        };

        var results = await SendSearchAsync(
            "v1/places:searchText", body, latitude, longitude, cancellationToken);
        return results.Where(place => place.DistanceMeters <= radiusMeters).ToArray();
    }

    public async Task<IReadOnlyList<NearbyVeterinarian>> SearchPetHotelsByAreaAsync(
        string? city,
        string? district,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Google Places API anahtarı yapılandırılmamış.");

        var area = string.Join(", ", new[] { district?.Trim(), city?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(area))
            throw new ArgumentException("Şehir veya ilçe bilgilerinden en az biri gereklidir.");

        var cacheKey = $"google-pet-hotels-area:{area.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NearbyVeterinarian>? cached) && cached is not null)
            return cached;

        var body = new
        {
            textQuery = $"{area} pet oteli köpek oteli kedi pansiyonu",
            includedType = "pet_boarding_service",
            strictTypeFiltering = true,
            maxResultCount = 20,
            languageCode = "tr",
            regionCode = "TR"
        };

        var results = await SendSearchAsync(
            "v1/places:searchText", body, null, null, cancellationToken);
        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));
        return results;
    }

    private async Task<IReadOnlyList<NearbyVeterinarian>> SendSearchAsync(
        string endpoint,
        object body,
        double? originLatitude,
        double? originLongitude,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(body)
        };
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
                    ? displayNameText.GetString() ?? "Veteriner kliniği"
                    : "Veteriner kliniği";

                results.Add(new NearbyVeterinarian(
                    place.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N"),
                    displayName,
                    place.TryGetProperty("formattedAddress", out var address) ? address.GetString() : null,
                    originLatitude.HasValue && originLongitude.HasValue
                        ? CalculateDistanceMeters(originLatitude.Value, originLongitude.Value, placeLatitude, placeLongitude)
                        : null,
                    place.TryGetProperty("rating", out var rating) ? rating.GetDouble() : null,
                    place.TryGetProperty("userRatingCount", out var ratingCount) ? ratingCount.GetInt32() : null,
                    place.TryGetProperty("currentOpeningHours", out var hours)
                        && hours.TryGetProperty("openNow", out var openNow) ? openNow.GetBoolean() : null,
                    place.TryGetProperty("googleMapsUri", out var mapsUri) ? mapsUri.GetString() : null,
                    placeLatitude,
                    placeLongitude));
            }
        }

        return results.OrderBy(place => place.DistanceMeters ?? int.MaxValue).ToArray();
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

    private static string? ReadNestedText(JsonElement parent, params string[] path)
    {
        var current = parent;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current)) return null;
        }
        return current.TryGetProperty("text", out var text) ? text.GetString() : null;
    }
}

public sealed record NearbyVeterinarian(
    string Id,
    string Name,
    string? Address,
    int? DistanceMeters,
    double? Rating,
    int? UserRatingCount,
    bool? OpenNow,
    string? GoogleMapsUri,
    double Latitude,
    double Longitude);

public sealed record LocationSuggestion(string Id, string Name, string? SecondaryText, string Label);
