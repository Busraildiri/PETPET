using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class ProductReviewReport
{
    public int Id { get; set; }
    public int ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
