using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class MobileContactPreference
{
    [Key] public int UserId { get; set; }
    public User User { get; set; } = null!;
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(254)] public string? ContactEmail { get; set; }
    public bool AllowPatiMatchSharing { get; set; }
    public bool AllowAdoptionSharing { get; set; }
    public bool AllowLostPetSharing { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
