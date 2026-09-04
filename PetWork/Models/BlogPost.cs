using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class BlogPost
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public string Category { get; set; }
        
        public string FeaturedImage { get; set; } = "img/blog-default.jpg";
        
        public string ImageUrl { get; set; } = "img/blog-default.jpg";
        
        public DateTime PublishedDate { get; set; } = DateTime.Now;
        
        public DateTime PublishDate { get; set; } = DateTime.Now;
        
        public int ViewCount { get; set; } = 0;
        
        public int UserId { get; set; }
        public User User { get; set; }
    }
} 