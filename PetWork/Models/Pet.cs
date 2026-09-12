using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class Pet
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        
        [Required]
        public string Type { get; set; }
        
        public string PetType { get; set; }
        
        public string? Breed { get; set; }
        
        public DateTime? DateOfBirth { get; set; }
        
        public int? Age { get; set; }
        
        public string? Gender { get; set; }
        
        public string ProfileImage { get; set; } = "img/pet-default.jpg";
        
        public string? Image { get; set; }
        
        public string? Description { get; set; }

        [StringLength(300)]
        public string? Character { get; set; }

        [StringLength(500)]
        public string? CareNotes { get; set; }

        [StringLength(30)]
        public string? ChildCompatibility { get; set; }

        [StringLength(30)]
        public string? OtherPetCompatibility { get; set; }

        public bool? IsVaccinated { get; set; }
        public bool? IsNeutered { get; set; }
        public bool? IsMicrochipped { get; set; }

        [StringLength(300)]
        public string? TagsCsv { get; set; }

        // Tür bazlı opsiyonel özellikler JSON olarak saklanır; yeni türler şema değişmeden eklenebilir.
        [StringLength(2000)]
        public string? ExtraAttributesJson { get; set; }
        
        public int UserId { get; set; }
        public User User { get; set; }
    }
} 
