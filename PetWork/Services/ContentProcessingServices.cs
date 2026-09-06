using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using PetWork.Models;

namespace PetWork.Services;

public sealed class ContentLicensePolicy : IContentLicensePolicy
{
    public bool IsAllowed(string provider, string licenseCode) =>
        (provider, licenseCode) switch
        {
            ("StackExchange", var license) when license.StartsWith("CC BY-SA", StringComparison.OrdinalIgnoreCase) => true,
            ("USDA FoodData Central", "CC0-1.0") => true,
            ("Wikimedia", var license) when license.StartsWith("CC BY-SA", StringComparison.OrdinalIgnoreCase) => true,
            ("Wikibooks", var license) when license.StartsWith("CC BY-SA", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
}

public sealed class HtmlPlainTextSanitizer : IContentSanitizer
{
    private static readonly Regex ExcessBlankLines = new(@"(\r?\n){3,}", RegexOptions.Compiled);

    public string ToSafePlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var document = new HtmlDocument();
        document.LoadHtml(html);

        foreach (var node in document.DocumentNode.SelectNodes("//script|//style|//iframe|//object|//embed") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        foreach (var node in document.DocumentNode.SelectNodes("//br|//p|//div|//li|//h1|//h2|//h3|//pre|//blockquote") ?? Enumerable.Empty<HtmlNode>())
            node.AppendChild(document.CreateTextNode(Environment.NewLine));

        var text = WebUtility.HtmlDecode(document.DocumentNode.InnerText)
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Trim();
        return ExcessBlankLines.Replace(text, Environment.NewLine + Environment.NewLine);
    }
}

public sealed class ContentSafetyReviewService : IContentSafetyReviewService
{
    private static readonly string[] HighRiskTerms =
    [
        "dose", "dosage", "medicine", "medication", "drug", "treatment", "emergency",
        "poison", "toxic", "xylitol", "chocolate", "onion", "garlic", "grape", "raisin",
        "ilaç", "doz", "tedavi", "acil", "zehir", "toksik", "ksilitol", "çikolata",
        "soğan", "sarımsak", "üzüm", "kanlı ishal", "nöbet"
    ];

    private static readonly string[] MediumRiskTerms =
    [
        "symptom", "disease", "infection", "nutrition", "diet", "calorie", "protein",
        "belirti", "hastalık", "enfeksiyon", "beslenme", "diyet", "kalori", "protein"
    ];

    public string Classify(string title, string body, string contentType)
    {
        var text = $"{title} {body}";
        if (HighRiskTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return ExternalContentRiskLevels.High;
        if (MediumRiskTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return ExternalContentRiskLevels.Medium;
        return contentType is ExternalContentTypes.Disease or ExternalContentTypes.Recipe or ExternalContentTypes.FoodData
            ? ExternalContentRiskLevels.High
            : ExternalContentRiskLevels.Low;
    }
}

public sealed class ManualReviewTranslationService : ITranslationService
{
    public Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken) =>
        Task.FromResult(new TranslationResult(
            false,
            text,
            "ManualReview",
            "1",
            "Otomatik çeviri sağlayıcısı yapılandırılmadı."));
}
