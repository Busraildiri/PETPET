using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class Recipe
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        [Required]
        public string Ingredients { get; set; }
        
        [Required]
        public string Instructions { get; set; }

        public string Content { get; set; }
        
        public string? PetType { get; set; }
        
        public string? AnimalType { get; set; }
        
        public string? DietType { get; set; }
        
        public int PreparationTime { get; set; } = 30;
        
        public string FeaturedImage { get; set; } = "img/hero-recipes-v2.png";
        
        public string ImageUrl { get; set; } = "img/hero-recipes-v2.png";
        
        public string Difficulty { get; set; } = "Orta";
        
        public string PrepTime { get; set; } = "30 dakika";
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public DateTime PublishDate { get; set; } = DateTime.Now;
        
        public int ViewCount { get; set; } = 0;
        
        public int UserId { get; set; }
        public User? User { get; set; }
    }
} 
