using System.Collections.Generic;

namespace PetWork.Models.ViewModels
{
    public class RecipeViewModel
    {
        public List<Recipe> Recipes { get; set; } = new List<Recipe>();
        
        public List<string> AnimalTypes { get; set; } = new List<string>();
        
        public List<string> Difficulties { get; set; } = new List<string>();
        
        public List<string> SortOptions { get; set; } = new List<string>
        {
            "En Yeni",
            "En Eski",
            "En Popüler",
            "En Az Popüler"
        };
        
        public string? SelectedAnimalType { get; set; }
        
        public string? SelectedDifficulty { get; set; }
        
        public string? SelectedSortOption { get; set; } = "En Yeni";
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
    }
} 