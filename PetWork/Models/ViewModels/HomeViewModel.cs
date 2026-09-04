namespace PetWork.Models
{
    public class HomeViewModel
    {
        public List<Question> FeaturedQuestions { get; set; } = new List<Question>();
        public List<BlogPost> RecentBlogs { get; set; } = new List<BlogPost>();
        public int MemberCount { get; set; }
        public int QuestionCount { get; set; }
        public int AnswerCount { get; set; }
        public int PetCount { get; set; }
    }
}
