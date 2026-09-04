using System.Collections.Generic;

namespace PetWork.Models.ViewModels
{
    public class BlogViewModel
    {
        public List<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();
        public List<string> Categories { get; set; } = new List<string>();
        public string? SelectedCategory { get; set; }
        public string SortBy { get; set; } = "newest";
    }
} 