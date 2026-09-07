using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class PatiMatchProfile
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;

    [Required, StringLength(20)]
    public string Purpose { get; set; } = "friendship";

    [Required, StringLength(80)]
    public string City { get; set; } = string.Empty;

    [StringLength(80)]
    public string? District { get; set; }

    [StringLength(120)]
    public string? PreferredTypesCsv { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
