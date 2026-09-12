using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class PatiMatchContactShare
{
    public long Id { get; set; }
    public int PetOneId { get; set; }
    public Pet PetOne { get; set; } = null!;
    public int PetTwoId { get; set; }
    public Pet PetTwo { get; set; } = null!;
    public int SharedByUserId { get; set; }
    public User SharedByUser { get; set; } = null!;
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(254)] public string? ContactEmail { get; set; }
    public DateTime SharedAt { get; set; } = DateTime.Now;
    public DateTime? RevokedAt { get; set; }
}
