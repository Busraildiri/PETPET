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
        
        public int UserId { get; set; }
        public User User { get; set; }
    }
} 