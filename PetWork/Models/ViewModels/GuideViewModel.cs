using System.Collections.Generic;

namespace PetWork.Models.ViewModels
{
    public class GuideViewModel
    {
        public List<Guide> Guides { get; set; } = new List<Guide>();
        
        public List<string> AnimalTypes { get; set; } = new List<string>();
        
        public List<string> Categories { get; set; } = new List<string>();
        
        public List<string> Levels { get; set; } = new List<string>
        {
            "Başlangıç",
            "Orta",
            "İleri"
        };
        
        public List<string> SortOptions { get; set; } = new List<string>
        {
            "En Yeni",
            "En Eski",
            "En Popüler",
            "En Az Popüler"
        };
        
        public string? SelectedAnimalType { get; set; }
        
        public string? SelectedCategory { get; set; }
        
        public string? SelectedLevel { get; set; }
        
        public string? SelectedSortOption { get; set; } = "En Yeni";
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
    }
} 