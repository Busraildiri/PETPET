using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetWork.Services;

public sealed class ConfigurableTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ManualReviewTranslationService _fallback = new();

    public ConfigurableTranslationService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage,
        CancellationToken cancellationToken)
    {
        var endpoint = _configuration["ExternalContent:Translation:Endpoint"];
        if (string.IsNullOrWhiteSpace(text) || sourceLanguage.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase))
            return new(true, text, "NoTranslationRequired", "1");

        if (string.IsNullOrWhiteSpace(endpoint))
            return await TranslateWithMyMemoryAsync(text, sourceLanguage, targetLanguage, cancellationToken);

        try
        {
            var request = new TranslationRequest
            {
                Text = text,
                Source = sourceLanguage,
                Target = targetLanguage,
                ApiKey = _configuration["ExternalContent:Translation:ApiKey"]
            };
            using var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TranslationResponse>(cancellationToken);
            return string.IsNullOrWhiteSpace(result?.TranslatedText)
                ? new(false, text, "ConfiguredHttp", "1", "Çeviri sağlayıcısı boş sonuç döndürdü.")
                : new(true, result.TranslatedText, "ConfiguredHttp", "1");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, text, "ConfiguredHttp", "1", "Çeviri servisine ulaşılamadı; manuel inceleme gerekiyor.");
        }
    }

    private async Task<TranslationResult> TranslateWithMyMemoryAsync(string text, string sourceLanguage,
        string targetLanguage, CancellationToken cancellationToken)
    {
        try
        {
            var translated = new List<string>();
            var preferGoogleForLongText = Encoding.UTF8.GetByteCount(text) > 420;
            var myMemoryAvailable = !preferGoogleForLongText;
            var chunkSize = preferGoogleForLongText ? 1400 : 420;
            foreach (var chunk in SplitIntoChunks(text, chunkSize))
            {
                string? translatedChunk = null;
                if (myMemoryAvailable)
                {
                    var url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(chunk) +
                              "&langpair=" + Uri.EscapeDataString($"{sourceLanguage}|{targetLanguage}") + "&mt=1";
                    var result = await _httpClient.GetFromJsonAsync<MyMemoryResponse>(url, cancellationToken);
                    translatedChunk = WebUtility.HtmlDecode(result?.ResponseData?.TranslatedText);
                    if (result?.ResponseStatus != 200 || IsQuotaMessage(translatedChunk))
                    {
                        myMemoryAvailable = false;
                        translatedChunk = null;
                    }
                }

                translatedChunk ??= await TranslateChunkWithGoogleAsync(chunk, sourceLanguage, targetLanguage, cancellationToken);
                if (string.IsNullOrWhiteSpace(translatedChunk))
                    return await _fallback.TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
                translated.Add(translatedChunk);
            }
            return new(true, ApplyGlossary(string.Join(" ", translated)),
                myMemoryAvailable ? "MyMemory" : "GoogleTranslate", "REST");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(false, text, "MyMemory", "REST", "Çeviri servisine ulaşılamadı; içerik yayınlanmadı.");
        }
    }

    private async Task<string?> TranslateChunkWithGoogleAsync(string text, string sourceLanguage,
        string targetLanguage, CancellationToken cancellationToken)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&sl=" +
                  Uri.EscapeDataString(sourceLanguage) + "&tl=" + Uri.EscapeDataString(targetLanguage) +
                  "&q=" + Uri.EscapeDataString(text);
        var json = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() == 0) return null;
        var segments = json[0];
        if (segments.ValueKind != JsonValueKind.Array) return null;
        var result = new StringBuilder();
        foreach (var segment in segments.EnumerateArray())
            if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0 &&
                segment[0].ValueKind == JsonValueKind.String)
                result.Append(segment[0].GetString());
        await Task.Delay(120, cancellationToken);
        return result.ToString();
    }

    private static bool IsQuotaMessage(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("AVAILABLE FREE TRANSLATIONS", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SplitIntoChunks(string text, int maxUtf8Bytes)
    {
        var current = new StringBuilder();
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Encoding.UTF8.GetByteCount(current + (current.Length == 0 ? "" : " ") + word) > maxUtf8Bytes)
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                if (Encoding.UTF8.GetByteCount(word) > maxUtf8Bytes)
                {
                    foreach (var part in word.Chunk(100)) yield return new string(part);
                    continue;
                }
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static string ApplyGlossary(string text) => text
        .Replace("evcil hayvan ebeveyni", "hayvan dostu", StringComparison.OrdinalIgnoreCase)
        .Replace("çöp kutusu", "kedi tuvaleti", StringComparison.OrdinalIgnoreCase)
        .Replace("kısırlaştırma", "kısırlaştırma", StringComparison.OrdinalIgnoreCase);

    private sealed class TranslationRequest
    {
        [JsonPropertyName("q")] public string Text { get; set; } = string.Empty;
        [JsonPropertyName("source")] public string Source { get; set; } = "en";
        [JsonPropertyName("target")] public string Target { get; set; } = "tr";
        [JsonPropertyName("format")] public string Format { get; set; } = "text";
        [JsonPropertyName("api_key")] public string? ApiKey { get; set; }
    }
    private sealed class TranslationResponse { [JsonPropertyName("translatedText")] public string? TranslatedText { get; set; } }
    private sealed class MyMemoryResponse
    {
        [JsonPropertyName("responseData")] public MyMemoryData? ResponseData { get; set; }
        [JsonPropertyName("responseStatus")] public int ResponseStatus { get; set; }
    }
    private sealed class MyMemoryData { [JsonPropertyName("translatedText")] public string? TranslatedText { get; set; } }
}
