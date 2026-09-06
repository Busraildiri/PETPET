using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class SocialComment
{
    public int Id { get; set; }
    public int SocialPostId { get; set; }
    public SocialPost SocialPost { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    [StringLength(1000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
}
