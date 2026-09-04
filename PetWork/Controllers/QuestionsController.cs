using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PetWork.Controllers
{
    public class QuestionsController : Controller
    {
        private static readonly string[] DemoUsernames = { "kediSever", "goldenSahibi", "kusSever" };
        private readonly PetWorkDbContext _context;
        private readonly ILogger<QuestionsController> _logger;

        public QuestionsController(PetWorkDbContext context, ILogger<QuestionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index(string? category = null, string? sortBy = "newest")
        {
            // Veritabanından soruları çek
            var questions = _context.Questions
                .Include(q => q.User)
                .Include(q => q.Answers.Where(answer => !DemoUsernames.Contains(answer.User.Username)))
                .Where(q => q.User == null || !DemoUsernames.Contains(q.User.Username))
                .AsQueryable();
            
            // Kategori filtresi uygula
            if (!string.IsNullOrEmpty(category))
            {
                questions = questions.Where(q => q.Category == category);
            }
            
            // Sıralama uygula
            switch (sortBy)
            {
                case "oldest":
                    questions = questions.OrderBy(q => q.CreatedDate);
                    break;
                case "mostviewed":
                    questions = questions.OrderByDescending(q => q.ViewCount);
                    break;
                case "mostanswered":
                    questions = questions.OrderByDescending(q => q.Answers.Count);
                    break;
                default: // newest
                    questions = questions.OrderByDescending(q => q.CreatedDate);
                    break;
            }

            // Kategorileri veritabanından al
            var categories = _context.Questions
                .Where(q => q.Category != null)
                .Select(q => q.Category)
                .Distinct()
                .ToList();

            // Veri yoksa varsayılan kategoriler
            if (categories.Count == 0)
            {
                categories = new List<string>
                {
                    "Kedi Sağlığı",
                    "Köpek Sağlığı",
                    "Kedi Beslenmesi",
                    "Köpek Beslenmesi",
                    "Kuş Bakımı",
                    "Kemirgen Bakımı",
                    "Eğitim",
                    "Davranış Problemleri",
                    "Yas ve Kayıp",
                    "Sosyalleşme ve Arkadaşlık",
                    "Genel"
                };
            }
            
            var viewModel = new QuestionsViewModel
            {
                Questions = questions.ToList(),
                Categories = categories,
                SelectedCategory = category,
                SortBy = sortBy
            };
            
            return View(viewModel);
        }
        
        public IActionResult Details(int id)
        {
            // Veritabanından soruyu ID'ye göre bul
            var question = _context.Questions
                .Include(q => q.User)
                .Where(q => q.User == null || !DemoUsernames.Contains(q.User.Username))
                .Include(q => q.Answers.Where(answer => !DemoUsernames.Contains(answer.User.Username)))
                    .ThenInclude(a => a.User)
                .FirstOrDefault(q => q.Id == id);
            
            if (question == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            question.ViewCount++;
            _context.SaveChanges();
            
            return View(question);
        }
        
        public IActionResult Ask(string? category = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Ask", "Questions") });
            }
            
            return View(new Question { Category = category });
        }
        
        [HttpPost]
        public IActionResult Ask(Question question)
        {
            try
            {
                _logger.LogInformation("Ask metoduna POST isteği geldi: {0}", JsonSerializer.Serialize(question));
                
                // User property için validasyon hatasını temizle
                if (ModelState.ContainsKey("User"))
                {
                    ModelState.Remove("User");
                }
                
                if (ModelState.IsValid)
                {
                    try
                    {
                        // Session'dan kullanıcı bilgisini al
                        var userId = HttpContext.Session.GetInt32("UserId");
                        _logger.LogInformation("Session UserId: {0}", userId);
                        
                        if (!userId.HasValue)
                        {
                            TempData["ErrorMessage"] = "Soru sormak için giriş yapmalısınız.";
                            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Ask", "Questions") });
                        }

                        // Debug bilgisi
                        _logger.LogInformation($"Soru ekleme: UserId={userId.Value}, Title={question.Title}");

                        // Soruyu hazırla
                        question.UserId = userId.Value;
                        question.CreatedDate = DateTime.Now;
                        question.ViewCount = 0;
                        
                        // İlişkili User nesnesini yükle
                        var user = _context.Users.Find(userId.Value);
                        if (user != null)
                        {
                            question.User = user;
                        }

                        // Soruyu veritabanına ekle
                        _context.Questions.Add(question);
                        
                        // Database durumunu logla
                        _logger.LogInformation("Veritabanı değişiklikleri: {0}", 
                            string.Join(", ", _context.ChangeTracker.Entries()
                                .Where(e => e.State == EntityState.Added)
                                .Select(e => e.Entity.GetType().Name)));
                        
                        int result = _context.SaveChanges();

                        // Başarılı mı kontrol et
                        if (result > 0)
                        {
                            _logger.LogInformation("Soru başarıyla eklendi, ID: {0}", question.Id);
                            TempData["SuccessMessage"] = "Sorunuz başarıyla eklendi!";
                            return RedirectToAction("Details", new { id = question.Id });
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Sorununuz eklenirken bir hata oluştu. Lütfen tekrar deneyin.";
                            _logger.LogWarning("Soru eklenemedi. SaveChanges 0 döndü.");
                        }
                    }
                    catch (Exception ex)
                    {
                        TempData["ErrorMessage"] = "Soru eklenirken bir hata oluştu: " + ex.Message;
                        _logger.LogError(ex, "Soru eklenirken hata");
                    }
                }
                else
                {
                    // Validasyon hatalarını logla
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogWarning($"Validasyon hatası {state.Key}: {error.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Beklenmeyen bir hata oluştu: " + ex.Message;
                _logger.LogError(ex, "Soru işleme sırasında beklenmeyen hata");
            }
            
            return View(question);
        }
        
        [HttpPost]
        public IActionResult Answer(int id, Answer answer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Session'dan kullanıcı bilgisini al
                    var userId = HttpContext.Session.GetInt32("UserId");
                    if (userId.HasValue)
                    {
                        answer.UserId = userId.Value;
                        answer.QuestionId = id;
                        answer.CreatedDate = DateTime.Now;
                        answer.IsAccepted = false;
                        answer.UpVotes = 0;
                        answer.DownVotes = 0;

                        _context.Answers.Add(answer);
                        _context.SaveChanges();
                        
                        TempData["SuccessMessage"] = "Cevabınız başarıyla eklendi!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Cevap vermek için giriş yapmalısınız.";
                        return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Questions", new { id }) });
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Cevap eklenirken bir hata oluştu: " + ex.Message;
                    _logger.LogError(ex, "Cevap eklenirken hata");
                }
            }
            
            return RedirectToAction("Details", new { id });
        }
    }
} 
