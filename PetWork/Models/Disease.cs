using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class Disease
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        public string? Symptoms { get; set; }
        
        public string? Treatments { get; set; }
        
        public string? Treatment { get; set; }
        
        public string? Prevention { get; set; }
        
        public string? PetType { get; set; }
        
        public string? AnimalType { get; set; }
        
        public string FeaturedImage { get; set; } = "img/hero-health-v2.png";

        public string? Category { get; set; }

        public string? SeverityLevel { get; set; }

        public int ViewCount { get; set; } = 0;

        public DateTime PublishDate { get; set; } = DateTime.Now;
    }
} 
