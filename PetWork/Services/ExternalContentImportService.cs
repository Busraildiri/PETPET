using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class ExternalContentImportService : IExternalContentImportService
{
    private readonly PetWorkDbContext _db;
    private readonly Dictionary<string, IExternalContentProvider> _providers;
    private readonly IContentLicensePolicy _licenses;
    private readonly IContentSanitizer _sanitizer;
    private readonly IContentSafetyReviewService _safety;
    private readonly ITranslationService _translation;

    public ExternalContentImportService(PetWorkDbContext db, IEnumerable<IExternalContentProvider> providers,
        IContentLicensePolicy licenses, IContentSanitizer sanitizer,
        IContentSafetyReviewService safety, ITranslationService translation)
    {
        _db = db;
        _providers = providers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _licenses = licenses;
        _sanitizer = sanitizer;
        _safety = safety;
        _translation = translation;
    }

    public IReadOnlyList<string> Providers => _providers.Keys.OrderBy(x => x).ToList();

    public Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(string provider, ExternalContentSearchRequest request,
        CancellationToken cancellationToken) => GetProvider(provider).SearchAsync(request, cancellationToken);

    public async Task<ExternalContentSource> ImportAsync(string provider, string externalId, int adminUserId,
        CancellationToken cancellationToken, string? targetContentType = null)
    {
        var item = await GetProvider(provider).FetchAsync(externalId, cancellationToken)
            ?? throw new InvalidOperationException("Kaynak içerik bulunamadı.");
        if (!string.IsNullOrWhiteSpace(targetContentType) && item.Provider == "Wikimedia")
            item = item with { ContentType = targetContentType, Children = [] };
        if (!_licenses.IsAllowed(item.Provider, item.LicenseCode))
            throw new InvalidOperationException($"{item.LicenseCode} lisansı içe aktarma politikasında izinli değil.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var source = await UpsertAsync(item, null, adminUserId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var child in item.Children)
            await UpsertAsync(child, source.Id, adminUserId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return source;
    }

    public async Task<ExternalContentSource> RefreshAsync(int sourceId, int adminUserId, CancellationToken cancellationToken)
    {
        var source = await _db.ExternalContentSources.FindAsync([sourceId], cancellationToken)
            ?? throw new InvalidOperationException("İçerik kaydı bulunamadı.");
        var current = await GetProvider(source.Provider).FetchAsync(source.ExternalId, cancellationToken);
        if (current is null)
        {
            source.IsSourceAvailable = false;
            source.ReviewStatus = ExternalContentReviewStatuses.NeedsReview;
            source.LastCheckedAt = Now();
            AddAudit(source, "SourceRemoved", "NeedsReview", "Kaynak API içeriği artık döndürmüyor; otomatik silme yapılmadı.", adminUserId);
            await _db.SaveChangesAsync(cancellationToken);
            return source;
        }
        return await ImportAsync(source.Provider, source.ExternalId, adminUserId, cancellationToken);
    }

    private async Task<ExternalContentSource> UpsertAsync(ExternalContentItem item, int? parentSourceId, int adminUserId,
        CancellationToken cancellationToken)
    {
        var title = _sanitizer.ToSafePlainText(item.Title);
        var text = LimitForReadableImport(_sanitizer.ToSafePlainText(item.Body));
        EnsureHttpsUrl(item.SourceUrl, "Kaynak bağlantısı");
        EnsureHttpsUrl(item.LicenseUrl, "Lisans bağlantısı");
        var hash = Hash($"{title}\n{text}\n{item.Revision}");
        var existing = await _db.ExternalContentSources.SingleOrDefaultAsync(x =>
            x.Provider == item.Provider && x.ExternalId == item.ExternalId && x.ContentType == item.ContentType,
            cancellationToken);

        if (existing is not null && existing.OriginalContentHash == hash &&
            ((existing.WasTranslated && IsUsableTranslation(existing.TranslatedTitle) &&
              IsUsableTranslation(existing.TranslatedText)) ||
             item.SourceLanguage.Equals("tr", StringComparison.OrdinalIgnoreCase)))
        {
            existing.LastCheckedAt = Now();
            existing.IsSourceAvailable = true;
            AddAudit(existing, "SourceChecked", "Unchanged", "Kaynakta değişiklik bulunmadı.", adminUserId);
            return existing;
        }

        var titleTranslation = await _translation.TranslateAsync(title, item.SourceLanguage, "tr", cancellationToken);
        var bodyTranslation = await _translation.TranslateAsync(text, item.SourceLanguage, "tr", cancellationToken);
        var translated = titleTranslation.Succeeded && bodyTranslation.Succeeded &&
                         IsUsableTranslation(titleTranslation.Text) && IsUsableTranslation(bodyTranslation.Text);
        var entity = existing ?? new ExternalContentSource
        {
            Provider = item.Provider,
            ExternalId = item.ExternalId,
            ContentType = item.ContentType,
            ImportedAt = Now()
        };

        var wasPublished = entity.ReviewStatus == ExternalContentReviewStatuses.Approved;
        entity.ParentSourceId = parentSourceId ?? entity.ParentSourceId;
        entity.SourceUrl = item.SourceUrl;
        entity.ApiUrl = item.ApiUrl;
        entity.SourceTitle = title;
        entity.SourceAuthorName = item.AuthorName;
        entity.SourceAuthorUrl = item.AuthorUrl;
        entity.SourceLanguage = item.SourceLanguage;
        entity.LicenseCode = item.LicenseCode;
        entity.LicenseUrl = item.LicenseUrl;
        entity.OriginalPublishedAt = AsDatabaseTime(item.PublishedAt);
        entity.SourceUpdatedAt = AsDatabaseTime(item.UpdatedAt);
        entity.LastCheckedAt = Now();
        entity.SourceRevision = item.Revision;
        entity.OriginalContentHash = hash;
        entity.OriginalText = text;
        entity.TranslatedTitle = titleTranslation.Text;
        entity.TranslatedText = bodyTranslation.Text;
        entity.Tags = string.Join(",", item.Tags);
        entity.Category ??= MapCategory(item.Tags);
        entity.TranslationProvider = bodyTranslation.Provider;
        entity.TranslationVersion = bodyTranslation.Version;
        entity.TranslatedAt = translated ? Now() : null;
        entity.WasTranslated = translated;
        entity.AttributionText = BuildAttribution(item, translated);
        entity.RiskLevel = _safety.Classify(title, text, item.ContentType);
        entity.ReviewStatus = wasPublished ? ExternalContentReviewStatuses.NeedsReview : ExternalContentReviewStatuses.Pending;
        entity.IsSourceAvailable = true;

        if (existing is null) _db.ExternalContentSources.Add(entity);
        AddAudit(entity, existing is null ? "Imported" : "SourceChanged", "Success",
            translated ? "İçerik alındı ve Türkçeye çevrildi." : "İçerik alındı; çeviri için manuel inceleme gerekiyor.", adminUserId);
        AddAudit(entity, "LicenseChecked", "Allowed", $"İzin verilen lisans: {item.LicenseCode}", adminUserId);
        if (entity.RiskLevel != ExternalContentRiskLevels.Low)
            AddAudit(entity, "SafetyFlagged", entity.RiskLevel, "İçerik yayın öncesi güvenlik incelemesi gerektiriyor.", adminUserId);
        return entity;
    }

    private static string LimitForReadableImport(string text, int maxLength = 2400)
    {
        if (text.Length <= maxLength) return text;
        var cutAt = text.LastIndexOf(' ', maxLength);
        if (cutAt < maxLength / 2) cutAt = maxLength;
        return text[..cutAt].TrimEnd() + "…";
    }

    private static bool IsUsableTranslation(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        !text.Contains("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("AVAILABLE FREE TRANSLATIONS", StringComparison.OrdinalIgnoreCase);

    private IExternalContentProvider GetProvider(string name) => _providers.TryGetValue(name, out var provider)
        ? provider : throw new InvalidOperationException("Bilinmeyen dış içerik sağlayıcısı.");

    private void AddAudit(ExternalContentSource source, string eventType, string outcome, string details, int userId) =>
        _db.ContentImportAudits.Add(new ContentImportAudit
        {
            ExternalContentSource = source, EventType = eventType, Outcome = outcome, Details = details,
            PerformedByUserId = userId, CreatedAt = Now()
        });

    private static string BuildAttribution(ExternalContentItem item, bool translated) =>
        $"“{item.Title}” — {item.AuthorName ?? "kaynak yazar"}, {item.Provider}, {item.LicenseCode}." +
        (translated ? " Türkçeye otomatik çevrildi; PetWork editörleri tarafından incelenir." : " PetWork editörleri tarafından düzenlenir.");

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string MapCategory(IReadOnlyList<string> tags)
    {
        if (tags.Any(x => x.Contains("cat", StringComparison.OrdinalIgnoreCase))) return "Kedi";
        if (tags.Any(x => x.Contains("dog", StringComparison.OrdinalIgnoreCase))) return "Köpek";
        if (tags.Any(x => x.Contains("bird", StringComparison.OrdinalIgnoreCase))) return "Kuş";
        return "Genel";
    }
    private static void EnsureHttpsUrl(string value, string field)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{field} güvenli bir HTTPS adresi değil.");
    }
    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    private static DateTime? AsDatabaseTime(DateTime? value) => value.HasValue
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified) : null;
}
