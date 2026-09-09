using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class AdoptionApplication
{
    public int Id { get; set; }
    public int AdoptionListingId { get; set; }
    public AdoptionListing AdoptionListing { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(1000)] public string Message { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
