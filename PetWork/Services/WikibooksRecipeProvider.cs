using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using PetWork.Models;

namespace PetWork.Services;

public sealed class WikibooksRecipeProvider : IExternalContentProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    public WikibooksRecipeProvider(HttpClient httpClient, IMemoryCache cache) { _httpClient = httpClient; _cache = cache; }
    public string Name => "Wikibooks";

    public async Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(ExternalContentSearchRequest request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.PageSize, 1, 20);
        var key = $"wikibooks:recipe:{request.Query}:{limit}";
        if (_cache.TryGetValue(key, out IReadOnlyList<ExternalContentCandidate>? cached) && cached is not null) return cached;
        var url = $"w/api.php?action=query&generator=search&gsrsearch={Uri.EscapeDataString(request.Query)}&gsrnamespace=102&gsrlimit={limit}" +
                  "&prop=info&inprop=url&format=json&formatversion=2";
        var payload = await _httpClient.GetFromJsonAsync<Response>(url, cancellationToken);
        var result = payload?.Query?.Pages.Select(p => new ExternalContentCandidate(Name, ExternalContentTypes.Recipe,
            p.PageId.ToString(), p.Title.Replace("Cookbook:", "", StringComparison.OrdinalIgnoreCase),
            p.FullUrl ?? $"https://en.wikibooks.org/?curid={p.PageId}", "Wikibooks contributors", null,
            LicenseCode: "CC BY-SA 4.0")).ToList() ?? [];
        _cache.Set(key, result, TimeSpan.FromHours(1));
        return result;
    }

    public async Task<ExternalContentItem?> FetchAsync(string externalId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(externalId, out var id)) return null;
        var url = $"w/api.php?action=query&pageids={id}&prop=extracts|info|revisions&explaintext=1&inprop=url" +
                  "&rvprop=ids|timestamp&format=json&formatversion=2";
        var payload = await _httpClient.GetFromJsonAsync<Response>(url, cancellationToken);
        var page = payload?.Query?.Pages.SingleOrDefault();
        if (page is null || string.IsNullOrWhiteSpace(page.Extract)) return null;
        var revision = page.Revisions?.FirstOrDefault();
        return new ExternalContentItem(Name, ExternalContentTypes.Recipe, externalId,
            page.FullUrl ?? $"https://en.wikibooks.org/?curid={id}",
            $"https://en.wikibooks.org/w/api.php?action=query&pageids={id}",
            page.Title.Replace("Cookbook:", "", StringComparison.OrdinalIgnoreCase), page.Extract,
            "Wikibooks contributors", "https://en.wikibooks.org/wiki/Wikibooks:About", "en",
            "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/", null,
            revision?.Timestamp, revision?.RevisionId.ToString(), ["dog", "treat"], []);
    }

    private sealed class Response { [JsonPropertyName("query")] public Query? Query { get; set; } }
    private sealed class Query { [JsonPropertyName("pages")] public List<Page> Pages { get; set; } = []; }
    private sealed class Page
    {
        [JsonPropertyName("pageid")] public long PageId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("fullurl")] public string? FullUrl { get; set; }
        [JsonPropertyName("extract")] public string? Extract { get; set; }
        [JsonPropertyName("revisions")] public List<Revision>? Revisions { get; set; }
    }
    private sealed class Revision
    {
        [JsonPropertyName("revid")] public long RevisionId { get; set; }
        [JsonPropertyName("timestamp")] public DateTime? Timestamp { get; set; }
    }
}
