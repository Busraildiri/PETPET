using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class LostPetListing
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required, StringLength(10)] public string Kind { get; set; } = "lost";
    [Required, StringLength(50)] public string PetName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Species { get; set; } = string.Empty;
    [StringLength(100)] public string? Breed { get; set; }
    [Required, StringLength(1000)] public string DistinguishingFeatures { get; set; } = string.Empty;
    public DateTime EventAt { get; set; }
    [Required, StringLength(80)] public string City { get; set; } = string.Empty;
    [Required, StringLength(80)] public string District { get; set; } = string.Empty;
    [StringLength(120)] public string? Neighborhood { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [StringLength(500)] public string? CollarOrMicrochip { get; set; }
    [StringLength(1500)] public string? Notes { get; set; }
    [Required, StringLength(500)] public string ImagePath { get; set; } = string.Empty;
    public bool AllowInAppMessages { get; set; } = true;
    [StringLength(32)] public string? ContactPhone { get; set; }
    [StringLength(254)] public string? ContactEmail { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "active";
    [Required, StringLength(100)] public string SourceName { get; set; } = "Pet'im";
    [StringLength(1000)] public string? SourceUrl { get; set; }
    [StringLength(200)] public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<LostPetSighting> Sightings { get; set; } = [];
}
