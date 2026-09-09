using System.ComponentModel.DataAnnotations;

namespace PetWork.Models;

public sealed class MobileMediaAsset
{
    public int Id { get; set; }
    [Required, StringLength(500)] public string StorageKey { get; set; } = string.Empty;
    [Required, StringLength(50)] public string ContentType { get; set; } = string.Empty;
    [Required] public byte[] Data { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
