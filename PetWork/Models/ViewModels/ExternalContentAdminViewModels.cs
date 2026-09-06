using System.ComponentModel.DataAnnotations;

namespace PetWork.Models.ViewModels;

public sealed class ExternalContentListViewModel
{
    public List<ExternalContentSource> Items { get; set; } = [];
    public IReadOnlyList<string> Providers { get; set; } = [];
    public string? Provider { get; set; }
    public string? Status { get; set; }
    public string? ContentType { get; set; }
    public string? Query { get; set; }
    public string? TargetContentType { get; set; }
    public List<Services.ExternalContentCandidate> SearchResults { get; set; } = [];
}

public sealed class ExternalContentEditViewModel
{
    public int Id { get; set; }
    [Required, StringLength(500)] public string Title { get; set; } = string.Empty;
    [Required] public string Text { get; set; } = string.Empty;
    [StringLength(1000)] public string? Tags { get; set; }
    [StringLength(100)] public string? Category { get; set; }
    public ExternalContentSource Source { get; set; } = null!;
}

public sealed class ExternalContentApprovalViewModel
{
    public int Id { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Lisans kontrolünü onaylamalısınız.")]
    public bool LicenseConfirmed { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Kaynak ve atıf kontrolünü onaylamalısınız.")]
    public bool AttributionConfirmed { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "İçerik güvenliği kontrolünü onaylamalısınız.")]
    public bool SafetyConfirmed { get; set; }
}
