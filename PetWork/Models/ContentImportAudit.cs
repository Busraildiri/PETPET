using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public class ContentImportAudit
{
    public long Id { get; set; }
    public int ExternalContentSourceId { get; set; }
    public ExternalContentSource ExternalContentSource { get; set; } = null!;

    [Required, StringLength(80)]
    public string EventType { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Outcome { get; set; } = "Success";

    [StringLength(2000)]
    public string? Details { get; set; }

    public int? PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
