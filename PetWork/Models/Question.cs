using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class Question
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public string? Category { get; set; }
        
        public string? Tags { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public int ViewCount { get; set; } = 0;
        
        public int UserId { get; set; }
        public User? User { get; set; }
        
        public List<Answer>? Answers { get; set; }
    }
} 