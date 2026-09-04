using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly PetWorkDbContext _context;

    public HomeController(ILogger<HomeController> logger, PetWorkDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public IActionResult Index()
    {
        // Veritabanından en çok cevaplanan soruları çek
        var featuredQuestions = _context.Questions
            .Include(q => q.User)
            .Include(q => q.Answers)
            .OrderByDescending(q => q.Answers.Count)
            .Take(3)
            .ToList();

        // Veritabanından en son eklenen blogları çek
        var recentBlogs = _context.BlogPosts
            .OrderByDescending(b => b.PublishedDate)
            .Take(3)
            .ToList();

        // Eğer veri yoksa, varsayılan veriler kullanılabilir
        if (featuredQuestions.Count == 0)
        {
            featuredQuestions = new List<Question>
            {
                new Question
                {
                    Id = 1,
                    Title = "Kedim çok tüy döküyor, ne yapmalıyım?",
                    Content = "3 yaşında British Shorthair kedim var ve son zamanlarda çok fazla tüy dökmeye başladı. Normal mi yoksa veterinere götürmeli miyim?",
                    CreatedDate = DateTime.Now.AddDays(-2),
                    Category = "Kedi Bakımı",
                    ViewCount = 42,
                    User = new User { Username = "ayse_kedi", ProfileImage = "img/user1.jpg" },
                    Answers = new List<Answer> { new Answer(), new Answer() }
                },
                new Question
                {
                    Id = 2,
                    Title = "Golden Retriever köpeğim için en iyi mama önerileri",
                    Content = "1 yaşında Golden Retriever köpeğim var. Sizce hangi mama markaları en sağlıklı ve besleyici?",
                    CreatedDate = DateTime.Now.AddDays(-5),
                    Category = "Köpek Beslenmesi",
                    ViewCount = 75,
                    User = new User { Username = "mehmet_dog", ProfileImage = "img/user2.jpg" },
                    Answers = new List<Answer> { new Answer(), new Answer(), new Answer(), new Answer() }
                },
                new Question
                {
                    Id = 3,
                    Title = "Papağanım tüylerini yoluyor, ne yapabilirim?",
                    Content = "2 yaşındaki sultan papağanım son zamanlarda tüylerini yolmaya başladı. Stres kaynaklı olabilir mi?",
                    CreatedDate = DateTime.Now.AddHours(-12),
                    Category = "Kuş Bakımı",
                    ViewCount = 28,
                    User = new User { Username = "zeynep_kuş", ProfileImage = "img/user-profile.jpg" },
                    Answers = new List<Answer> { new Answer() }
                }
            };
        }

        if (recentBlogs.Count == 0)
        {
            recentBlogs = new List<BlogPost>
            {
                new BlogPost
                {
                    Id = 1,
                    Title = "Evcil Hayvanlar İçin Evde Yapabileceğiniz Doğal Oyuncaklar",
                    Content = "Kediniz veya köpeğiniz için evde kolayca hazırlayabileceğiniz oyuncak fikirleri...",
                    Category = "Aktiviteler",
                    FeaturedImage = "img/recipes/blog1.jpeg",
                    PublishedDate = DateTime.Now.AddDays(-3)
                },
                new BlogPost
                {
                    Id = 2,
                    Title = "Veteriner Kontrollerini Asla İhmal Etmeyin",
                    Content = "Evcil hayvanınızın sağlığı için düzenli veteriner kontrollerinin önemi...",
                    Category = "Sağlık",
                    FeaturedImage = "img/recipes/vet1.jpeg",
                    PublishedDate = DateTime.Now.AddDays(-7)
                },
                new BlogPost
                {
                    Id = 3,
                    Title = "Kedilerde Davranış Problemleri ve Çözümleri",
                    Content = "Kedilerde görülen yaygın davranış problemleri ve bunlarla başa çıkma yöntemleri...",
                    Category = "Davranış",
                    FeaturedImage = "img/blog1.jpg",
                    PublishedDate = DateTime.Now.AddDays(-10)
                }
            };
        }

        var viewModel = new HomeViewModel
        {
            FeaturedQuestions = featuredQuestions,
            RecentBlogs = recentBlogs
        };
        
        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
