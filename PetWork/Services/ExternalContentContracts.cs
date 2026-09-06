using PetWork.Models;

namespace PetWork.Services;

public sealed record ExternalContentSearchRequest(string Query, string? Tags = null, int PageSize = 20);

public sealed record ExternalContentCandidate(
    string Provider,
    string ContentType,
    string ExternalId,
    string Title,
    string SourceUrl,
    string? AuthorName,
    DateTime? PublishedAt,
    int Score = 0,
    int ViewCount = 0,
    string? LicenseCode = null);

public sealed record ExternalContentItem(
    string Provider,
    string ContentType,
    string ExternalId,
    string SourceUrl,
    string ApiUrl,
    string Title,
    string Body,
    string? AuthorName,
    string? AuthorUrl,
    string SourceLanguage,
    string LicenseCode,
    string LicenseUrl,
    DateTime? PublishedAt,
    DateTime? UpdatedAt,
    string? Revision,
    IReadOnlyList<string> Tags,
    IReadOnlyList<ExternalContentItem> Children);

public interface IExternalContentProvider
{
    string Name { get; }
    Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(
        ExternalContentSearchRequest request,
        CancellationToken cancellationToken);
    Task<ExternalContentItem?> FetchAsync(string externalId, CancellationToken cancellationToken);
}

public interface IContentLicensePolicy
{
    bool IsAllowed(string provider, string licenseCode);
}

public interface IContentSanitizer
{
    string ToSafePlainText(string? html);
}

public interface IContentSafetyReviewService
{
    string Classify(string title, string body, string contentType);
}

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken);
}

public sealed record TranslationResult(
    bool Succeeded,
    string Text,
    string Provider,
    string Version,
    string? Error = null);

public interface IExternalContentImportService
{
    IReadOnlyList<string> Providers { get; }
    Task<IReadOnlyList<ExternalContentCandidate>> SearchAsync(string provider, ExternalContentSearchRequest request, CancellationToken cancellationToken);
    Task<ExternalContentSource> ImportAsync(string provider, string externalId, int adminUserId, CancellationToken cancellationToken,
        string? targetContentType = null);
    Task<ExternalContentSource> RefreshAsync(int sourceId, int adminUserId, CancellationToken cancellationToken);
}

public interface IExternalContentPublishingService
{
    Task<bool> PublishAsync(int sourceId, int adminUserId, bool reviewed, CancellationToken cancellationToken);
    Task<int> PublishTreeAsync(int sourceId, int adminUserId, bool reviewed, CancellationToken cancellationToken);
}
