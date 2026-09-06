using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class SocialPost
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    [StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Tags { get; set; }

    [StringLength(500)]
    public string? ImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public List<SocialComment> Comments { get; set; } = [];
    public List<SocialPostReport> Reports { get; set; } = [];
}
