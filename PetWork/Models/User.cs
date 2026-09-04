using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Username { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        
        [Required]
        public string PasswordHash { get; set; }
        
        public string? Bio { get; set; }
        
        public string ProfileImage { get; set; } = "img/user-profile.jpg";
        
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
        
        public int ExperiencePoints { get; set; } = 0;
        
        public bool IsAdmin { get; set; } = false;
        
        public List<Pet>? Pets { get; set; }
        
        public List<Question>? Questions { get; set; }
        
        public List<Answer>? Answers { get; set; }
        
        public List<Badge>? Badges { get; set; }
    }
} 