using System.Collections.Generic;

namespace PetWork.Models
{
    public class QuestionsViewModel
    {
        public List<Question> Questions { get; set; } = new List<Question>();
        public List<string> Categories { get; set; } = new List<string>();
        public string? SelectedCategory { get; set; }
        public string SortBy { get; set; } = "newest";
    }
} 