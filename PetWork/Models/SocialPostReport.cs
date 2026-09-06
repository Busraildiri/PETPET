using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class SocialPostReport
{
    public int Id { get; set; }
    public int SocialPostId { get; set; }
    public SocialPost SocialPost { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsResolved { get; set; }
}
