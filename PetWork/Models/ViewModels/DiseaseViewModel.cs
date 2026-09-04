using System.Collections.Generic;

namespace PetWork.Models.ViewModels
{
    public class DiseaseViewModel
    {
        public List<Disease> Diseases { get; set; } = new List<Disease>();
        
        public List<string> AnimalTypes { get; set; } = new List<string>();
        
        public List<string> Categories { get; set; } = new List<string>();
        
        public List<string> SeverityLevels { get; set; } = new List<string>
        {
            "Düşük",
            "Orta",
            "Yüksek"
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
        
        public string? SelectedSeverityLevel { get; set; }
        
        public string? SelectedSortOption { get; set; } = "En Yeni";
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
    }
} 