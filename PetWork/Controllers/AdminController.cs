using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;

namespace PetWork.Controllers;

public class AdminController : Controller
{
    private readonly PetWorkDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminController(PetWorkDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private bool IsAdmin()
    {
        var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");
        return userId.HasValue && _context.Users.Any(user => user.Id == userId.Value && user.IsAdmin);
    }

    public IActionResult Index()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        ViewBag.UsersCount = _context.Users.Count();
        ViewBag.RecipesCount = _context.Recipes.Count();
        ViewBag.DiseasesCount = _context.Diseases.Count();
        ViewBag.GuidesCount = _context.Guides.Count();
        ViewBag.QuestionsCount = _context.Questions.Count();
        ViewBag.BlogsCount = _context.BlogPosts.Count();

        return View();
    }

    public IActionResult Users()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        ViewBag.CurrentUserId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");
        return View(_context.Users.OrderBy(user => user.Username).ToList());
    }

    public IActionResult Recipes()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        return View(_context.Recipes.Include(recipe => recipe.User).OrderByDescending(recipe => recipe.PublishDate).ToList());
    }

    public IActionResult Diseases()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        return View(_context.Diseases.OrderBy(disease => disease.Name).ToList());
    }

    public IActionResult Guides()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        return View(_context.Guides.Include(guide => guide.User).OrderByDescending(guide => guide.PublishDate).ToList());
    }

    public IActionResult Questions()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        return View(_context.Questions.Include(question => question.User).OrderByDescending(question => question.CreatedDate).ToList());
    }

    public IActionResult Blogs()
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        return View(_context.BlogPosts.Include(blog => blog.User).OrderByDescending(blog => blog.PublishDate).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id, string type)
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        var redirectAction = type switch
        {
            "recipe" => nameof(Recipes),
            "disease" => nameof(Diseases),
            "guide" => nameof(Guides),
            "question" => nameof(Questions),
            "blog" => nameof(Blogs),
            "user" => nameof(Users),
            _ => nameof(Index)
        };

        try
        {
            switch (type)
            {
                case "recipe":
                    RemoveEntity(_context.Recipes.Find(id), _context.Recipes, "Tarif silindi.");
                    break;
                case "disease":
                    RemoveEntity(_context.Diseases.Find(id), _context.Diseases, "Hastalık kaydı silindi.");
                    break;
                case "guide":
                    RemoveEntity(_context.Guides.Find(id), _context.Guides, "Rehber silindi.");
                    break;
                case "question":
                    RemoveEntity(_context.Questions.Find(id), _context.Questions, "Soru ve bağlı cevapları silindi.");
                    break;
                case "blog":
                    RemoveEntity(_context.BlogPosts.Find(id), _context.BlogPosts, "Blog yazısı silindi.");
                    break;
                case "user":
                    DeleteUser(id);
                    break;
                default:
                    TempData["ErrorMessage"] = "Geçersiz silme isteği.";
                    break;
            }
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Kayıt, bağlı veriler nedeniyle silinemedi.";
        }

        return RedirectToAction(redirectAction);
    }

    private void RemoveEntity<TEntity>(TEntity? entity, DbSet<TEntity> entities, string successMessage)
        where TEntity : class
    {
        if (entity is null)
        {
            TempData["ErrorMessage"] = "Silinecek kayıt bulunamadı.";
            return;
        }

        entities.Remove(entity);
        _context.SaveChanges();
        TempData["SuccessMessage"] = successMessage;
    }

    private void DeleteUser(int id)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");
        if (currentUserId == id)
        {
            TempData["ErrorMessage"] = "Oturum açtığınız admin hesabını silemezsiniz.";
            return;
        }

        var user = _context.Users.Find(id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "Silinecek kullanıcı bulunamadı.";
            return;
        }

        if (user.IsAdmin && _context.Users.Count(candidate => candidate.IsAdmin) <= 1)
        {
            TempData["ErrorMessage"] = "Sistemdeki son admin hesabı silinemez.";
            return;
        }

        using var transaction = _context.Database.BeginTransaction();
        _context.Answers.RemoveRange(_context.Answers.Where(answer => answer.UserId == id));
        _context.Recipes.RemoveRange(_context.Recipes.Where(recipe => recipe.UserId == id));
        _context.Questions.RemoveRange(_context.Questions.Where(question => question.UserId == id));
        _context.Users.Remove(user);
        _context.SaveChanges();
        transaction.Commit();

        TempData["SuccessMessage"] = "Kullanıcı ve bağlı içerikleri silindi.";
    }
}
