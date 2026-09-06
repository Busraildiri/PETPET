using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using PetWork.Models;

namespace PetWork.Services;

public sealed class UsdaFoodDataProvider : IExternalContentProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string? _apiKey;

    public UsdaFoodDataProvider(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _apiKey = configuration["ExternalContent:Usda:ApiKey"];
    }

    public string Name => "USDA FoodData Central";

    public async Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(ExternalContentSearchRequest request, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var pageSize = Math.Clamp(request.PageSize, 1, 25);
        var cacheKey = $"usda:search:{request.Query}:{pageSize}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ExternalContentCandidate>? cached) && cached is not null) return cached;

        var payload = await _httpClient.GetFromJsonAsync<FoodSearchResponse>(
            $"foods/search?api_key={Uri.EscapeDataString(_apiKey!)}&query={Uri.EscapeDataString(request.Query)}&pageSize={pageSize}", cancellationToken)
            ?? new FoodSearchResponse();
        var result = payload.Foods.Select(food => new ExternalContentCandidate(
            Name, ExternalContentTypes.FoodData, food.FdcId.ToString(), food.Description, SourceUrl(food.FdcId),
            "U.S. Department of Agriculture", null, LicenseCode: "CC0-1.0")).ToList();
        _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
        return result;
    }

    public async Task<ExternalContentItem?> FetchAsync(string externalId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!long.TryParse(externalId, out var fdcId)) return null;
        var food = await _httpClient.GetFromJsonAsync<FoodDetails>(
            $"food/{fdcId}?api_key={Uri.EscapeDataString(_apiKey!)}", cancellationToken);
        if (food is null) return null;

        var summary = new StringBuilder().AppendLine(food.Description);
        if (!string.IsNullOrWhiteSpace(food.DataType)) summary.AppendLine($"Veri türü: {food.DataType}");
        if (!string.IsNullOrWhiteSpace(food.FoodCategory?.Description)) summary.AppendLine($"Kategori: {food.FoodCategory.Description}");
        summary.AppendLine().AppendLine("100 g için bildirilen besin değerleri:");
        foreach (var item in food.FoodNutrients.Where(x => x.Amount.HasValue && x.Nutrient is not null).OrderBy(x => x.Nutrient!.Rank).Take(40))
            summary.AppendLine($"- {item.Nutrient!.Name}: {item.Amount:0.##} {item.Nutrient.UnitName}");

        return new ExternalContentItem(Name, ExternalContentTypes.FoodData, externalId, SourceUrl(fdcId),
            $"https://api.nal.usda.gov/fdc/v1/food/{fdcId}", food.Description, summary.ToString(),
            "U.S. Department of Agriculture", "https://fdc.nal.usda.gov/", "en", "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/", null, null, externalId, [], []);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("USDA API anahtarı User Secrets içinde ExternalContent:Usda:ApiKey alanına eklenmemiş.");
    }

    private static string SourceUrl(long id) => $"https://fdc.nal.usda.gov/fdc-app.html#/food-details/{id}/nutrients";
    private sealed class FoodSearchResponse { [JsonPropertyName("foods")] public List<FoodSummary> Foods { get; set; } = []; }
    private sealed class FoodSummary
    {
        [JsonPropertyName("fdcId")] public long FdcId { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    }
    private sealed class FoodDetails
    {
        [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("dataType")] public string? DataType { get; set; }
        [JsonPropertyName("foodCategory")] public FoodCategory? FoodCategory { get; set; }
        [JsonPropertyName("foodNutrients")] public List<FoodNutrient> FoodNutrients { get; set; } = [];
    }
    private sealed class FoodCategory { [JsonPropertyName("description")] public string? Description { get; set; } }
    private sealed class FoodNutrient
    {
        [JsonPropertyName("nutrient")] public Nutrient? Nutrient { get; set; }
        [JsonPropertyName("amount")] public decimal? Amount { get; set; }
    }
    private sealed class Nutrient
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("unitName")] public string UnitName { get; set; } = string.Empty;
        [JsonPropertyName("rank")] public int Rank { get; set; }
    }
}
