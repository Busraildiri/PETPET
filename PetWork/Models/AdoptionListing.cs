using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class AdoptionListing
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(50)] public string PetName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Species { get; set; } = string.Empty;
    [StringLength(100)] public string? Breed { get; set; }
    public int? AgeYears { get; set; }
    [StringLength(20)] public string? Gender { get; set; }
    [Required, StringLength(80)] public string City { get; set; } = string.Empty;
    [StringLength(80)] public string? District { get; set; }
    [Required, StringLength(500)] public string HealthInfo { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Story { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ImagePath { get; set; } = string.Empty;
    public bool AllowInAppMessages { get; set; } = true;
    [StringLength(32)] public string? ContactPhone { get; set; }
    [StringLength(254)] public string? ContactEmail { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public List<AdoptionApplication> Applications { get; set; } = [];
    public List<AdoptionListingReport> Reports { get; set; } = [];
}
