using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class MobilePushToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(300)] public string Token { get; set; } = string.Empty;
    [StringLength(20)] public string? Platform { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
