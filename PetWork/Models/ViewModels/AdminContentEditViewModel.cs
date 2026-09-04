using System.ComponentModel.DataAnnotations;

namespace PetWork.Models.ViewModels;

public class AdminContentEditViewModel
{
    public int Id { get; set; }

    [Required]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? Category { get; set; }
    public string? PetType { get; set; }
    public string? AnimalType { get; set; }
    public string? ImageUrl { get; set; }
    public string? FeaturedImage { get; set; }
    public string? Ingredients { get; set; }
    public string? Instructions { get; set; }
    public string? DietType { get; set; }
    public string? Difficulty { get; set; }
    public string? PrepTime { get; set; }
    public int? PreparationTime { get; set; }
    public string? Symptoms { get; set; }
    public string? Treatments { get; set; }
    public string? Treatment { get; set; }
    public string? Prevention { get; set; }
    public string? SeverityLevel { get; set; }
    public string? Level { get; set; }
    public string? Tags { get; set; }
    public int ViewCount { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? PublishedAt { get; set; }
}
