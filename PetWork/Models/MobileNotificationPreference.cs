using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class MobileNotificationPreference
{
    [Key] public int UserId { get; set; }
    public User User { get; set; } = null!;
    public bool CommunityNotifications { get; set; }
    public bool LostPetNotifications { get; set; }
    public bool MatchNotifications { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
