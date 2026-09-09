using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class ProductReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [Required, StringLength(100)] public string Brand { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ProductName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PetType { get; set; } = string.Empty;
    [Required, StringLength(1500)] public string Experience { get; set; } = string.Empty;
    public int TasteScore { get; set; }
    public int IngredientScore { get; set; }
    public int DigestionScore { get; set; }
    public int ValueScore { get; set; }
    [StringLength(500)] public string? ImagePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public List<ProductReviewReport> Reports { get; set; } = [];
}
