using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;
using Microsoft.AspNetCore.Http;

namespace PetWork.Controllers
{
    public class ProfileController : Controller
    {
        private readonly PetWorkDbContext _context;
        private readonly ExperienceService _experienceService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProfileController(PetWorkDbContext context, ExperienceService experienceService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _experienceService = experienceService;
            _httpContextAccessor = httpContextAccessor;
        }
        
        // Kullanıcı profilini görüntüleme
        public async Task<IActionResult> Index()
        {
            var userId = _httpContextAccessor.HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account");
                
            var user = await _context.Users
                .Include(u => u.Badges)
                .Include(u => u.Pets)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);
                
            if (user == null)
                return NotFound();
                
            // Kullanıcının aktivitelerini getir
            var questions = await _context.Questions.CountAsync(q => q.UserId == userId.Value);
            var answers = await _context.Answers.CountAsync(a => a.UserId == userId.Value);
            var recipes = await _context.Recipes.CountAsync(r => r.UserId == userId.Value);
            var guides = await _context.Guides.CountAsync(g => g.UserId == userId.Value);
            
            ViewBag.Questions = questions;
            ViewBag.Answers = answers;
            ViewBag.Recipes = recipes;
            ViewBag.Guides = guides;
            
            return View(user);
        }
        
        // Rozet sayfası
        public async Task<IActionResult> Badges()
        {
            var userId = _httpContextAccessor.HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account");
                
            var user = await _context.Users
                .Include(u => u.Badges)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);
                
            if (user == null)
                return NotFound();
                
            // Tüm mevcut rozetleri getir
            var allBadges = await _context.Badges.ToListAsync();
            
            ViewBag.AllBadges = allBadges;
            
            return View(user);
        }
        
        // Kullanıcı bilgilerini güncelleme sayfası
        public async Task<IActionResult> Edit()
        {
            var userId = _httpContextAccessor.HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account");
                
            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound();
                
            return View(user);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User model)
        {
            var userId = _httpContextAccessor.HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account");
                
            if (userId.Value != model.Id)
                return Unauthorized();
                
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound();
                    
                // Güvenlik nedeniyle sadece belirli alanları güncelle
                user.Username = model.Username;
                user.Email = model.Email;
                user.Bio = model.Bio;
                
                // Profil tamamlama kontrolü - ilk kez bio ekleniyorsa puan ver
                if (!string.IsNullOrEmpty(model.Bio) && string.IsNullOrEmpty(user.Bio))
                {
                    await _experienceService.AddExperienceAsync(userId.Value, ExperienceService.ExperiencePoints.ProfileCompletion, "Profil bilgilerini tamamladı");
                }
                
                await _context.SaveChangesAsync();
                
                return RedirectToAction(nameof(Index));
            }
            
            return View(model);
        }
        
        // Evcil hayvan ekleme sayfası
        public IActionResult AddPet()
        {
            var userId = _httpContextAccessor.HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account");
                
            return View(new Pet { UserId = userId.Value });
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPet(Pet model)
        {
            var userId = _httpContextAccessor.HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login", "Account");
                
            if (userId.Value != model.UserId)
                return Unauthorized();
                
            if (ModelState.IsValid)
            {
                _context.Pets.Add(model);
                await _context.SaveChangesAsync();
                
                // Evcil hayvan eklediği için kullanıcıya puan ver
                await _experienceService.AddExperienceAsync(userId.Value, ExperienceService.ExperiencePoints.AddPet, "Evcil hayvan ekledi");
                
                return RedirectToAction(nameof(Index));
            }
            
            return View(model);
        }
    }
} 