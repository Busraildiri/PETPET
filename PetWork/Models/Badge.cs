using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace PetWork.Models
{
    public class Badge
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        [Required]
        public string Description { get; set; }
        
        [Required]
        public string IconUrl { get; set; }
        
        public List<User> Users { get; set; } = new List<User>();
    }
} 