namespace PetWork.Models;

public sealed class SocialPostSave
{
    public int Id { get; set; }
    public int SocialPostId { get; set; }
    public SocialPost SocialPost { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
