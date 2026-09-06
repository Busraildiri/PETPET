using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using PetWork.Models;

namespace PetWork.Services;

public sealed class StackExchangeContentProvider : IExternalContentProvider
{
    private const string ApiVersion = "2.3";
    private const string Site = "pets";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly string? _apiKey;
    private static DateTimeOffset _nextAllowedRequest = DateTimeOffset.MinValue;

    public StackExchangeContentProvider(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _apiKey = configuration["ExternalContent:StackExchange:ApiKey"];
    }

    public string Name => "StackExchange";

    public async Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(
        ExternalContentSearchRequest request,
        CancellationToken cancellationToken)
    {
        EnsureBackoffElapsed();
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var cacheKey = $"stackexchange:search:{request.Query}:{request.Tags}:{pageSize}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ExternalContentCandidate>? cached) && cached is not null)
            return cached;

        var url = $"{ApiVersion}/search/advanced?site={Site}&filter=withbody&sort=votes&min=1&answers=1&pagesize={pageSize}" +
                  $"&q={Uri.EscapeDataString(request.Query)}";
        if (!string.IsNullOrWhiteSpace(request.Tags))
            url += $"&tagged={Uri.EscapeDataString(request.Tags)}";
        url = AddApiKey(url);

        var response = await GetAsync<StackExchangeQuestion>(url, cancellationToken);
        foreach (var question in response.Items)
            _cache.Set(QuestionCacheKey(question.QuestionId), question, TimeSpan.FromMinutes(15));

        var result = response.Items
            .Where(question => question.Score > 0 && question.AnswerCount > 0)
            .OrderByDescending(question => question.AcceptedAnswerId.HasValue)
            .ThenByDescending(question => question.Score)
            .ThenByDescending(question => question.ViewCount)
            .Select(question => new ExternalContentCandidate(
            Name,
            ExternalContentTypes.Question,
            question.QuestionId.ToString(),
            question.Title,
            question.Link,
            question.Owner?.DisplayName,
            FromUnixTime(question.CreationDate),
            question.Score,
            question.ViewCount,
            question.ContentLicense)).ToList();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<ExternalContentItem?> FetchAsync(string externalId, CancellationToken cancellationToken)
    {
        EnsureBackoffElapsed();
        if (!long.TryParse(externalId, out var questionId))
            return null;

        var questionUrl = AddApiKey($"{ApiVersion}/questions/{questionId}?site={Site}&filter=withbody");
        StackExchangeQuestion? question;
        if (!_cache.TryGetValue(QuestionCacheKey(questionId), out question) || question is null)
        {
            var questionResponse = await GetAsync<StackExchangeQuestion>(questionUrl, cancellationToken);
            question = questionResponse.Items.SingleOrDefault();
        }
        if (question is null)
            return null;

        var answersUrl = AddApiKey($"{ApiVersion}/questions/{questionId}/answers?site={Site}&filter=withbody&sort=votes&pagesize=10");
        var answerResponse = await GetAsync<StackExchangeAnswer>(answersUrl, cancellationToken);
        var answers = answerResponse.Items
            .OrderByDescending(answer => answer.IsAccepted)
            .ThenByDescending(answer => answer.Score)
            .Take(1)
            .Select(answer => new ExternalContentItem(
                Name,
                ExternalContentTypes.Answer,
                answer.AnswerId.ToString(),
                $"{question.Link}#answer-{answer.AnswerId}",
                $"https://api.stackexchange.com/{ApiVersion}/answers/{answer.AnswerId}?site={Site}&filter=withbody",
                $"{question.Title} — yanıt",
                answer.Body,
                answer.Owner?.DisplayName,
                answer.Owner?.Link,
                "en",
                answer.ContentLicense ?? question.ContentLicense ?? "CC BY-SA 4.0",
                "https://creativecommons.org/licenses/by-sa/4.0/",
                FromUnixTime(answer.CreationDate),
                FromUnixTime(answer.LastEditDate ?? answer.CreationDate),
                (answer.LastEditDate ?? answer.CreationDate).ToString(),
                [],
                [])).ToList();

        return new ExternalContentItem(
            Name,
            ExternalContentTypes.Question,
            question.QuestionId.ToString(),
            question.Link,
            questionUrl,
            question.Title,
            question.Body,
            question.Owner?.DisplayName,
            question.Owner?.Link,
            "en",
            question.ContentLicense ?? "CC BY-SA 4.0",
            "https://creativecommons.org/licenses/by-sa/4.0/",
            FromUnixTime(question.CreationDate),
            FromUnixTime(question.LastEditDate ?? question.CreationDate),
            (question.LastEditDate ?? question.CreationDate).ToString(),
            question.Tags,
            answers);
    }

    private async Task<StackExchangeResponse<T>> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            StackExchangeError? error = null;
            try { error = JsonSerializer.Deserialize<StackExchangeError>(json); }
            catch (JsonException) { }
            if (error?.ErrorName == "throttle_violation")
                throw new StackExchangeThrottleException(error.ErrorMessage ?? "Stack Exchange API kotası doldu.");
        }
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StackExchangeResponse<T>>(cancellationToken)
            ?? throw new InvalidOperationException("Stack Exchange boş bir cevap döndürdü.");
        if (payload.Backoff is > 0)
            _nextAllowedRequest = DateTimeOffset.UtcNow.AddSeconds(payload.Backoff.Value);
        return payload;
    }

    private void EnsureBackoffElapsed()
    {
        if (_nextAllowedRequest > DateTimeOffset.UtcNow)
            throw new InvalidOperationException($"Stack Exchange tekrar denemesi {_nextAllowedRequest:O} sonrasına ertelendi.");
    }

    private static DateTime? FromUnixTime(long? value) =>
        value.HasValue ? DateTimeOffset.FromUnixTimeSeconds(value.Value).UtcDateTime : null;

    private static string QuestionCacheKey(long questionId) => $"stackexchange:question:{questionId}";

    private string AddApiKey(string url) => string.IsNullOrWhiteSpace(_apiKey)
        ? url
        : $"{url}&key={Uri.EscapeDataString(_apiKey)}";

    private sealed class StackExchangeResponse<T>
    {
        [JsonPropertyName("items")] public List<T> Items { get; set; } = [];
        [JsonPropertyName("backoff")] public int? Backoff { get; set; }
    }

    private sealed class StackExchangeError
    {
        [JsonPropertyName("error_name")] public string? ErrorName { get; set; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    }

    private sealed class StackExchangeQuestion
    {
        [JsonPropertyName("question_id")] public long QuestionId { get; set; }
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
        [JsonPropertyName("link")] public string Link { get; set; } = string.Empty;
        [JsonPropertyName("owner")] public StackExchangeOwner? Owner { get; set; }
        [JsonPropertyName("creation_date")] public long CreationDate { get; set; }
        [JsonPropertyName("last_edit_date")] public long? LastEditDate { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("view_count")] public int ViewCount { get; set; }
        [JsonPropertyName("answer_count")] public int AnswerCount { get; set; }
        [JsonPropertyName("accepted_answer_id")] public long? AcceptedAnswerId { get; set; }
        [JsonPropertyName("content_license")] public string? ContentLicense { get; set; }
        [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    }

    private sealed class StackExchangeAnswer
    {
        [JsonPropertyName("answer_id")] public long AnswerId { get; set; }
        [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
        [JsonPropertyName("owner")] public StackExchangeOwner? Owner { get; set; }
        [JsonPropertyName("creation_date")] public long CreationDate { get; set; }
        [JsonPropertyName("last_edit_date")] public long? LastEditDate { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("is_accepted")] public bool IsAccepted { get; set; }
        [JsonPropertyName("content_license")] public string? ContentLicense { get; set; }
    }

    private sealed class StackExchangeOwner
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
        [JsonPropertyName("link")] public string? Link { get; set; }
    }
}

public sealed class StackExchangeThrottleException(string message) : Exception(message);
