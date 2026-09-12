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

        public bool IsEmailVerified { get; set; }
        
        [Required]
        [JsonIgnore]
        public string PasswordHash { get; set; }
        
        public string? Bio { get; set; }

        [StringLength(80)]
        public string? City { get; set; }

        [StringLength(80)]
        public string? Occupation { get; set; }

        [StringLength(80)]
        public string? LivingSituation { get; set; }

        public bool? HasChildren { get; set; }
        public bool? HasOtherPets { get; set; }

        public bool ShowBioToOthers { get; set; } = true;
        public bool ShowPetsToOthers { get; set; } = true;

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
        public List<MobileAuthSession>? MobileAuthSessions { get; set; }
        public List<PasswordResetToken>? PasswordResetTokens { get; set; }
    }
} 
