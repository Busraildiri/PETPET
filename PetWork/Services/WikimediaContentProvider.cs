using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using PetWork.Models;

namespace PetWork.Services;

public sealed class WikimediaContentProvider : IExternalContentProvider
{
    private static readonly SemaphoreSlim ApiGate = new(1, 1);
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    public WikimediaContentProvider(HttpClient httpClient, IMemoryCache cache) { _httpClient = httpClient; _cache = cache; }
    public string Name => "Wikimedia";

    public async Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(ExternalContentSearchRequest request, CancellationToken cancellationToken)
    {
        var isTurkish = request.Query.StartsWith("tr:", StringComparison.OrdinalIgnoreCase);
        var query = isTurkish ? request.Query[3..] : request.Query;
        var limit = Math.Clamp(request.PageSize, 1, 20);
        var key = $"wikimedia:search:{request.Query}:{limit}";
        if (_cache.TryGetValue(key, out IReadOnlyList<ExternalContentCandidate>? cached) && cached is not null) return cached;
        var root = isTurkish ? "https://tr.wikipedia.org/" : "https://en.wikipedia.org/";
        var url = $"{root}w/api.php?action=query&generator=search&gsrsearch={Uri.EscapeDataString(query)}&gsrnamespace=0&gsrlimit={limit}" +
                  "&prop=info&inprop=url&format=json&formatversion=2";
        var payload = await GetWithRetryAsync<WikiResponse>(url, cancellationToken);
        var result = payload?.Query?.Pages.Select(page => new ExternalContentCandidate(Name, ExternalContentTypes.Guide,
            isTurkish ? $"tr:{page.PageId}" : page.PageId.ToString(), page.Title,
            page.FullUrl ?? $"{root}?curid={page.PageId}",
            isTurkish ? "Vikipedi katkıda bulunanları" : "Wikipedia contributors", null,
            LicenseCode: "CC BY-SA 4.0")).ToList() ?? [];
        _cache.Set(key, result, TimeSpan.FromMinutes(30));
        return result;
    }

    public async Task<ExternalContentItem?> FetchAsync(string externalId, CancellationToken cancellationToken)
    {
        var isTurkish = externalId.StartsWith("tr:", StringComparison.OrdinalIgnoreCase);
        var rawId = isTurkish ? externalId[3..] : externalId;
        if (!long.TryParse(rawId, out var pageId)) return null;
        var root = isTurkish ? "https://tr.wikipedia.org/" : "https://en.wikipedia.org/";
        var url = $"{root}w/api.php?action=query&pageids={pageId}&prop=extracts|info|revisions&explaintext=1&exintro=1&exsectionformat=plain" +
                  "&inprop=url&rvprop=ids|timestamp&format=json&formatversion=2";
        var payload = await GetWithRetryAsync<WikiResponse>(url, cancellationToken);
        var page = payload?.Query?.Pages.SingleOrDefault();
        if (page is null || string.IsNullOrWhiteSpace(page.Extract)) return null;
        var revision = page.Revisions?.FirstOrDefault();
        return new ExternalContentItem(Name, ExternalContentTypes.Guide, externalId,
            page.FullUrl ?? $"{root}?curid={pageId}",
            $"{root}w/api.php?action=query&pageids={pageId}", page.Title, page.Extract,
            isTurkish ? "Vikipedi katkıda bulunanları" : "Wikipedia contributors",
            isTurkish ? "https://tr.wikipedia.org/wiki/Vikipedi:Hakkında" : "https://en.wikipedia.org/wiki/Wikipedia:About",
            isTurkish ? "tr" : "en", "CC BY-SA 4.0",
            "https://creativecommons.org/licenses/by-sa/4.0/", null, revision?.Timestamp,
            revision?.RevisionId.ToString(), [], []);
    }

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken cancellationToken)
    {
        await ApiGate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
                    await Task.Delay(750, cancellationToken);
                    return result;
                }

                if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == 4)
                    response.EnsureSuccessStatusCode();

                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt * 5);
                await Task.Delay(retryAfter, cancellationToken);
            }
            return default;
        }
        finally
        {
            ApiGate.Release();
        }
    }

    private sealed class WikiResponse { [JsonPropertyName("query")] public WikiQuery? Query { get; set; } }
    private sealed class WikiQuery { [JsonPropertyName("pages")] public List<WikiPage> Pages { get; set; } = []; }
    private sealed class WikiPage
    {
        [JsonPropertyName("pageid")] public long PageId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("fullurl")] public string? FullUrl { get; set; }
        [JsonPropertyName("extract")] public string? Extract { get; set; }
        [JsonPropertyName("revisions")] public List<WikiRevision>? Revisions { get; set; }
    }
    private sealed class WikiRevision
    {
        [JsonPropertyName("revid")] public long RevisionId { get; set; }
        [JsonPropertyName("timestamp")] public DateTime? Timestamp { get; set; }
    }
}
