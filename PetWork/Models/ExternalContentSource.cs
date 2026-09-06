using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public class ExternalContentSource
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ContentType { get; set; } = ExternalContentTypes.Question;

    public int? LocalContentId { get; set; }

    [Required, StringLength(80)]
    public string Provider { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ExternalId { get; set; } = string.Empty;

    public int? ParentSourceId { get; set; }
    public ExternalContentSource? ParentSource { get; set; }
    public List<ExternalContentSource> Children { get; set; } = [];

    [Required, StringLength(2000)]
    public string SourceUrl { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? ApiUrl { get; set; }

    [Required, StringLength(500)]
    public string SourceTitle { get; set; } = string.Empty;

    [StringLength(300)]
    public string? SourceAuthorName { get; set; }

    [StringLength(2000)]
    public string? SourceAuthorUrl { get; set; }

    [Required, StringLength(10)]
    public string SourceLanguage { get; set; } = "en";

    [Required, StringLength(80)]
    public string LicenseCode { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string LicenseUrl { get; set; } = string.Empty;

    public DateTime? OriginalPublishedAt { get; set; }
    public DateTime? SourceUpdatedAt { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? SourceRevision { get; set; }

    [Required, StringLength(64)]
    public string OriginalContentHash { get; set; } = string.Empty;

    [Required]
    public string OriginalText { get; set; } = string.Empty;

    [StringLength(500)]
    public string? TranslatedTitle { get; set; }

    public string? TranslatedText { get; set; }

    [StringLength(1000)]
    public string? Tags { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? TranslationProvider { get; set; }

    [StringLength(100)]
    public string? TranslationVersion { get; set; }

    public DateTime? TranslatedAt { get; set; }
    public bool WasTranslated { get; set; }
    public bool WasModified { get; set; }

    [Required, StringLength(1000)]
    public string AttributionText { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string ReviewStatus { get; set; } = ExternalContentReviewStatuses.Pending;

    [Required, StringLength(30)]
    public string RiskLevel { get; set; } = ExternalContentRiskLevels.Low;

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }

    [StringLength(1000)]
    public string? RejectionReason { get; set; }

    public bool IsSourceAvailable { get; set; } = true;
    public List<ContentImportAudit> AuditEntries { get; set; } = [];
}

public static class ExternalContentTypes
{
    public const string Question = "Question";
    public const string Answer = "Answer";
    public const string Recipe = "Recipe";
    public const string Disease = "Disease";
    public const string Guide = "Guide";
    public const string BlogPost = "BlogPost";
    public const string FoodData = "FoodData";
}

public static class ExternalContentReviewStatuses
{
    public const string Pending = "Pending";
    public const string NeedsReview = "NeedsReview";
    public const string PublishedUnreviewed = "PublishedUnreviewed";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Archived = "Archived";
}

public static class ExternalContentRiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}
