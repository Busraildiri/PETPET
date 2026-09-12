using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class MobileSupportReport
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(40)] public string Category { get; set; } = string.Empty;
    [Required, StringLength(3000)] public string Description { get; set; } = string.Empty;
    [StringLength(500)] public string? ScreenshotPath { get; set; }
    [Required, StringLength(24)] public string TrackingNumber { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
