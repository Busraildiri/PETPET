using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class MobileNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(40)] public string Type { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Body { get; set; } = string.Empty;
    [StringLength(40)] public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
