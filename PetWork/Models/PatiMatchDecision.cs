namespace PetWork.Models;

public sealed class PatiMatchDecision
{
    public int Id { get; set; }
    public int SourcePetId { get; set; }
    public Pet SourcePet { get; set; } = null!;
    public int TargetPetId { get; set; }
    public Pet TargetPet { get; set; } = null!;
    public bool IsLike { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

