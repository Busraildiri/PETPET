using System;
using System.ComponentModel.DataAnnotations;

namespace PetWork.Models
{
    public class Answer
    {
        public int Id { get; set; }
        
        [Required]
        public string Content { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public bool IsAccepted { get; set; } = false;
        
        public int UpVotes { get; set; } = 0;
        
        public int DownVotes { get; set; } = 0;
        
        public int QuestionId { get; set; }
        public Question Question { get; set; }
        
        public int UserId { get; set; }
        public User User { get; set; }
    }
} 