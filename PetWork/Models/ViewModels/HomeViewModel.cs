namespace PetWork.Models
{
    public class HomeViewModel
    {
        public List<Question> FeaturedQuestions { get; set; } = new List<Question>();
        public List<BlogPost> RecentBlogs { get; set; } = new List<BlogPost>();
    }
} 