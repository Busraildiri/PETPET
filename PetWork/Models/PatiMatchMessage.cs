using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class PatiMatchMessage
{
    public long Id { get; set; }
    public int PetOneId { get; set; }
    public Pet PetOne { get; set; } = null!;
    public int PetTwoId { get; set; }
    public Pet PetTwo { get; set; } = null!;
    public int SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;

    [Required, StringLength(1000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
