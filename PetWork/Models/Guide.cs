using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class Guide
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public string? AnimalType { get; set; }
        
        public string? Category { get; set; }
        
        public string? Level { get; set; }
        
        public int ViewCount { get; set; } = 0;
        
        public DateTime PublishDate { get; set; } = DateTime.Now;
        
        public int UserId { get; set; }
        public User User { get; set; }
    }
} 