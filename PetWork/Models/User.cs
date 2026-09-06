using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
        [JsonIgnore]
        public string Email { get; set; }
        
        [Required]
        [JsonIgnore]
        public string PasswordHash { get; set; }
        
        public string? Bio { get; set; }
        
        public string ProfileImage { get; set; } = "img/user-profile.jpg";
        
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
        
        public int ExperiencePoints { get; set; } = 0;
        
        [JsonIgnore]
        public bool IsAdmin { get; set; } = false;
        
        [JsonIgnore]
        public List<Pet>? Pets { get; set; }
        
        [JsonIgnore]
        public List<Question>? Questions { get; set; }
        
        [JsonIgnore]
        public List<Answer>? Answers { get; set; }
        
        [JsonIgnore]
        public List<Badge>? Badges { get; set; }

        public List<SocialPost>? SocialPosts { get; set; }
        public List<SocialComment>? SocialComments { get; set; }
        public List<SocialPostReport>? SocialPostReports { get; set; }
    }
} 
