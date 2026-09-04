using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using Microsoft.AspNetCore.Http;

namespace PetWork.Controllers
{
    public class AdminController : Controller
    {
        private readonly PetWorkDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminController(PetWorkDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Admin olmayan kullanıcılar için güvenlik kontrolü
        private bool IsAdmin()
        {
            var session = _httpContextAccessor.HttpContext.Session;
            var userId = session.GetInt32("UserId");
            
            if (!userId.HasValue)
                return false;
                
            var user = _context.Users.FirstOrDefault(u => u.Id == userId.Value);
            return user != null && user.IsAdmin;
        }
        
        // Admin paneli ana sayfası
        public IActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            ViewBag.UsersCount = _context.Users.Count();
            ViewBag.RecipesCount = _context.Recipes.Count();
            ViewBag.DiseasesCount = _context.Diseases.Count();
            ViewBag.GuidesCount = _context.Guides.Count();
            ViewBag.QuestionsCount = _context.Questions.Count();
            
            return View();
        }
        
        // Kullanıcı yönetimi
        public IActionResult Users()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            var users = _context.Users.ToList();
            return View(users);
        }
        
        // Tarif yönetimi
        public IActionResult Recipes()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            var recipes = _context.Recipes.Include(r => r.User).ToList();
            return View(recipes);
        }
        
        // Hastalık bilgisi yönetimi
        public IActionResult Diseases()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            var diseases = _context.Diseases.ToList();
            return View(diseases);
        }
        
        // Rehber yönetimi
        public IActionResult Guides()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            var guides = _context.Guides.Include(g => g.User).ToList();
            return View(guides);
        }
        
        // Soru yönetimi
        public IActionResult Questions()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            var questions = _context.Questions.Include(q => q.User).ToList();
            return View(questions);
        }
        
        // İçerik silme
        [HttpPost]
        public IActionResult Delete(int id, string type)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Account");
                
            switch (type)
            {
                case "recipe":
                    var recipe = _context.Recipes.Find(id);
                    if (recipe != null)
                    {
                        _context.Recipes.Remove(recipe);
                        _context.SaveChanges();
                    }
                    return RedirectToAction("Recipes");
                    
                case "disease":
                    var disease = _context.Diseases.Find(id);
                    if (disease != null)
                    {
                        _context.Diseases.Remove(disease);
                        _context.SaveChanges();
                    }
                    return RedirectToAction("Diseases");
                    
                case "guide":
                    var guide = _context.Guides.Find(id);
                    if (guide != null)
                    {
                        _context.Guides.Remove(guide);
                        _context.SaveChanges();
                    }
                    return RedirectToAction("Guides");
                    
                case "question":
                    var question = _context.Questions.Find(id);
                    if (question != null)
                    {
                        _context.Questions.Remove(question);
                        _context.SaveChanges();
                    }
                    return RedirectToAction("Questions");
                    
                case "user":
                    var user = _context.Users.Find(id);
                    if (user != null)
                    {
                        _context.Users.Remove(user);
                        _context.SaveChanges();
                    }
                    return RedirectToAction("Users");
                    
                default:
                    return RedirectToAction("Index");
            }
        }
    }
} 