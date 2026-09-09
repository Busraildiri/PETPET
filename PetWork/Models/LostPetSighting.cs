using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class LostPetSighting
{
    public int Id { get; set; }
    public int LostPetListingId { get; set; }
    public LostPetListing LostPetListing { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(200)] public string LocationLabel { get; set; } = string.Empty;
    public DateTime SeenAt { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
