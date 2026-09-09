using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class AdoptionListingReport
{
    public int Id { get; set; }
    public int AdoptionListingId { get; set; }
    public AdoptionListing AdoptionListing { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
