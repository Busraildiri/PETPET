using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers;

public class HomeController : Controller
{
    private static readonly string[] DemoUsernames = { "kediSever", "goldenSahibi", "kusSever" };
    private readonly ILogger<HomeController> _logger;
    private readonly PetWorkDbContext _context;

    public HomeController(ILogger<HomeController> logger, PetWorkDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var viewModel = new HomeViewModel
        {
            FeaturedQuestions = await _context.Questions
                .AsNoTracking()
                .Where(question => question.User == null || !DemoUsernames.Contains(question.User.Username))
                .Include(question => question.User)
                .Include(question => question.Answers.Where(answer => !DemoUsernames.Contains(answer.User.Username)))
                .OrderByDescending(question => question.CreatedDate)
                .Take(6)
                .ToListAsync(),
            RecentBlogs = await _context.BlogPosts
                .AsNoTracking()
                .OrderByDescending(post => post.PublishedDate)
                .Take(3)
                .ToListAsync(),
            MemberCount = await _context.Users.CountAsync(user => !DemoUsernames.Contains(user.Username)),
            QuestionCount = await _context.Questions.CountAsync(question => question.User == null || !DemoUsernames.Contains(question.User.Username)),
            AnswerCount = await _context.Answers.CountAsync(answer => !DemoUsernames.Contains(answer.User.Username)),
            PetCount = await _context.Pets.CountAsync(pet => !DemoUsernames.Contains(pet.User.Username))
        };

        return View(viewModel);
    }

    public IActionResult Privacy() => View();

    public IActionResult About() => View();

    [HttpGet("/Petim")]
    public IActionResult Petim() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
