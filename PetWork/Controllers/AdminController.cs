using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models.ViewModels;

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

    [HttpGet]
    public IActionResult ResetPassword(int id)
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        var user = _context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new { candidate.Id, candidate.Username })
            .FirstOrDefault();

        if (user is null)
            return NotFound();

        return View(new AdminResetPasswordViewModel
        {
            UserId = user.Id,
            Username = user.Username
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(AdminResetPasswordViewModel model)
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        var user = _context.Users.Find(model.UserId);
        if (user is null)
            return NotFound();

        model.Username = user.Username;
        ModelState.Remove(nameof(model.Username));

        if (!ModelState.IsValid)
            return View(model);

        var hasher = new PasswordHasher<Models.User>();
        user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
        _context.SaveChanges();

        TempData["SuccessMessage"] = $"{user.Username} kullanıcısının şifresi sıfırlandı.";
        return RedirectToAction(nameof(Users));
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

    public IActionResult EditContent(string type, int id)
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        var model = GetContentForEditing(type, id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditContent(AdminContentEditViewModel model)
    {
        if (!IsAdmin())
            return RedirectToAction("Login", "Account");

        ValidateContent(model);
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var updated = UpdateContent(model);
            if (!updated)
                return NotFound();

            _context.SaveChanges();
            TempData["SuccessMessage"] = "İçerik güncellendi.";
            return RedirectToAction(nameof(EditContent), new { type = model.Type, id = model.Id });
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Değişiklikler veritabanına kaydedilemedi.");
            return View(model);
        }
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

    private AdminContentEditViewModel? GetContentForEditing(string type, int id)
    {
        switch (type)
        {
            case "recipe":
                var recipe = _context.Recipes.Include(item => item.User).FirstOrDefault(item => item.Id == id);
                return recipe is null ? null : new AdminContentEditViewModel
                {
                    Id = recipe.Id, Type = type, Title = recipe.Title, Description = recipe.Description,
                    Content = recipe.Content, PetType = recipe.PetType, AnimalType = recipe.AnimalType,
                    DietType = recipe.DietType, PreparationTime = recipe.PreparationTime,
                    FeaturedImage = recipe.FeaturedImage, ImageUrl = recipe.ImageUrl,
                    Difficulty = recipe.Difficulty, PrepTime = recipe.PrepTime,
                    Ingredients = recipe.Ingredients, Instructions = recipe.Instructions,
                    ViewCount = recipe.ViewCount, AuthorName = recipe.User?.Username, PublishedAt = recipe.PublishDate
                };
            case "disease":
                var disease = _context.Diseases.Find(id);
                return disease is null ? null : new AdminContentEditViewModel
                {
                    Id = disease.Id, Type = type, Title = disease.Name, Description = disease.Description,
                    Symptoms = disease.Symptoms, Treatments = disease.Treatments, Treatment = disease.Treatment,
                    Prevention = disease.Prevention, PetType = disease.PetType, AnimalType = disease.AnimalType,
                    FeaturedImage = disease.FeaturedImage, Category = disease.Category,
                    SeverityLevel = disease.SeverityLevel, ViewCount = disease.ViewCount, PublishedAt = disease.PublishDate
                };
            case "guide":
                var guide = _context.Guides.Include(item => item.User).FirstOrDefault(item => item.Id == id);
                return guide is null ? null : new AdminContentEditViewModel
                {
                    Id = guide.Id, Type = type, Title = guide.Title, Description = guide.Description,
                    Content = guide.Content, AnimalType = guide.AnimalType, Category = guide.Category,
                    Level = guide.Level, ViewCount = guide.ViewCount, AuthorName = guide.User?.Username,
                    PublishedAt = guide.PublishDate
                };
            case "question":
                var question = _context.Questions.Include(item => item.User).FirstOrDefault(item => item.Id == id);
                return question is null ? null : new AdminContentEditViewModel
                {
                    Id = question.Id, Type = type, Title = question.Title, Content = question.Content,
                    Category = question.Category, Tags = question.Tags, ViewCount = question.ViewCount,
                    AuthorName = question.User?.Username, PublishedAt = question.CreatedDate
                };
            case "blog":
                var blog = _context.BlogPosts.Include(item => item.User).FirstOrDefault(item => item.Id == id);
                return blog is null ? null : new AdminContentEditViewModel
                {
                    Id = blog.Id, Type = type, Title = blog.Title, Content = blog.Content,
                    Category = blog.Category, FeaturedImage = blog.FeaturedImage, ImageUrl = blog.ImageUrl,
                    ViewCount = blog.ViewCount, AuthorName = blog.User?.Username, PublishedAt = blog.PublishDate
                };
            default:
                return null;
        }
    }

    private void ValidateContent(AdminContentEditViewModel model)
    {
        if (model.Type is "recipe" or "disease" or "guide" && string.IsNullOrWhiteSpace(model.Description))
            ModelState.AddModelError(nameof(model.Description), "Açıklama zorunludur.");

        if (model.Type is "question" or "blog" or "guide" && string.IsNullOrWhiteSpace(model.Content))
            ModelState.AddModelError(nameof(model.Content), "İçerik zorunludur.");

        if (model.Type == "recipe" && string.IsNullOrWhiteSpace(model.Ingredients))
            ModelState.AddModelError(nameof(model.Ingredients), "Malzemeler zorunludur.");

        if (model.Type == "recipe" && string.IsNullOrWhiteSpace(model.Instructions))
            ModelState.AddModelError(nameof(model.Instructions), "Hazırlama adımları zorunludur.");
    }

    private bool UpdateContent(AdminContentEditViewModel model)
    {
        switch (model.Type)
        {
            case "recipe":
                var recipe = _context.Recipes.Find(model.Id);
                if (recipe is null) return false;
                recipe.Title = model.Title.Trim(); recipe.Description = model.Description!;
                recipe.Content = model.Content ?? string.Empty; recipe.PetType = model.PetType;
                recipe.AnimalType = model.AnimalType; recipe.DietType = model.DietType;
                recipe.PreparationTime = model.PreparationTime ?? 0; recipe.FeaturedImage = model.FeaturedImage ?? string.Empty;
                recipe.ImageUrl = model.ImageUrl ?? string.Empty; recipe.Difficulty = model.Difficulty ?? string.Empty;
                recipe.PrepTime = model.PrepTime ?? string.Empty; recipe.Ingredients = model.Ingredients!;
                recipe.Instructions = model.Instructions!;
                return true;
            case "disease":
                var disease = _context.Diseases.Find(model.Id);
                if (disease is null) return false;
                disease.Name = model.Title.Trim(); disease.Description = model.Description!;
                disease.Symptoms = model.Symptoms; disease.Treatments = model.Treatments;
                disease.Treatment = model.Treatment; disease.Prevention = model.Prevention;
                disease.PetType = model.PetType; disease.AnimalType = model.AnimalType;
                disease.FeaturedImage = model.FeaturedImage ?? string.Empty; disease.Category = model.Category;
                disease.SeverityLevel = model.SeverityLevel;
                return true;
            case "guide":
                var guide = _context.Guides.Find(model.Id);
                if (guide is null) return false;
                guide.Title = model.Title.Trim(); guide.Description = model.Description!;
                guide.Content = model.Content!; guide.AnimalType = model.AnimalType;
                guide.Category = model.Category; guide.Level = model.Level;
                return true;
            case "question":
                var question = _context.Questions.Find(model.Id);
                if (question is null) return false;
                question.Title = model.Title.Trim(); question.Content = model.Content!;
                question.Category = model.Category; question.Tags = model.Tags;
                return true;
            case "blog":
                var blog = _context.BlogPosts.Find(model.Id);
                if (blog is null) return false;
                blog.Title = model.Title.Trim(); blog.Content = model.Content!;
                blog.Category = model.Category ?? string.Empty; blog.FeaturedImage = model.FeaturedImage ?? string.Empty;
                blog.ImageUrl = model.ImageUrl ?? string.Empty;
                return true;
            default:
                ModelState.AddModelError(nameof(model.Type), "Geçersiz içerik türü.");
                return false;
        }
    }
}
