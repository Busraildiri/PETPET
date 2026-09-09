namespace PetWork.Models;

public sealed class SocialCommentLike
{
    public int Id { get; set; }
    public int SocialCommentId { get; set; }
    public SocialComment SocialComment { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
