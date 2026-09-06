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
using System.Data;
using PetWork.Services;

namespace PetWork.Controllers
{
    public class QuestionsController : Controller
    {
        private static readonly string[] DemoUsernames = { "kediSever", "goldenSahibi", "kusSever" };
        private readonly PetWorkDbContext _context;
        private readonly ILogger<QuestionsController> _logger;
        private readonly ExperienceService _experienceService;

        public QuestionsController(PetWorkDbContext context, ILogger<QuestionsController> logger,
            ExperienceService experienceService)
        {
            _context = context;
            _logger = logger;
            _experienceService = experienceService;
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
            ViewBag.ExternalSource = _context.ExternalContentSources.AsNoTracking().FirstOrDefault(x =>
                x.ContentType == ExternalContentTypes.Question && x.LocalContentId == question.Id &&
                x.ReviewStatus != ExternalContentReviewStatuses.Rejected && x.ReviewStatus != ExternalContentReviewStatuses.Archived);
            ViewBag.ExternalAnswerSources = _context.ExternalContentSources.AsNoTracking().Where(x =>
                x.ContentType == ExternalContentTypes.Answer && x.LocalContentId != null &&
                x.ReviewStatus != ExternalContentReviewStatuses.Rejected && x.ReviewStatus != ExternalContentReviewStatuses.Archived).ToDictionary(x => x.LocalContentId!.Value);
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.CanAcceptAnswer = currentUserId == question.UserId &&
                                      !(question.Answers?.Any(answer => answer.IsAccepted) ?? false);
            
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
        public async Task<IActionResult> Ask(Question question)
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
                        
                        int result = await _context.SaveChangesAsync();

                        // Başarılı mı kontrol et
                        if (result > 0)
                        {
                            await _experienceService.AddExperienceAsync(userId.Value,
                                ExperienceService.ExperiencePoints.AskQuestion, "Soru paylaştı");
                            _logger.LogInformation("Soru başarıyla eklendi, ID: {0}", question.Id);
                            TempData["SuccessMessage"] = $"Sorunuz başarıyla eklendi! +{ExperienceService.ExperiencePoints.AskQuestion} XP";
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
        public async Task<IActionResult> Answer(int id, Answer answer)
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
                        await _context.SaveChangesAsync();
                        await _experienceService.AddExperienceAsync(userId.Value,
                            ExperienceService.ExperiencePoints.AnswerQuestion, "Soruya cevap verdi");
                        
                        TempData["SuccessMessage"] = $"Cevabınız başarıyla eklendi! +{ExperienceService.ExperiencePoints.AnswerQuestion} XP";
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptAnswer(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Bir cevabı kabul etmek için giriş yapmalısınız.";
                return RedirectToAction("Login", "Account");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var answer = await _context.Answers
                    .Include(item => item.Question)
                    .SingleOrDefaultAsync(item => item.Id == id);
                if (answer is null) return NotFound();

                if (answer.Question.UserId != userId.Value)
                {
                    TempData["ErrorMessage"] = "Yalnızca soruyu paylaşan kişi cevap kabul edebilir.";
                    return RedirectToAction("Details", new { id = answer.QuestionId });
                }

                if (answer.UserId == userId.Value)
                {
                    TempData["ErrorMessage"] = "Kendi cevabınızı kabul ederek XP kazanamazsınız.";
                    return RedirectToAction("Details", new { id = answer.QuestionId });
                }

                if (answer.IsAccepted)
                {
                    TempData["SuccessMessage"] = "Bu cevap zaten kabul edilmiş.";
                    return RedirectToAction("Details", new { id = answer.QuestionId });
                }

                var alreadyAccepted = await _context.Answers.AnyAsync(item =>
                    item.QuestionId == answer.QuestionId && item.IsAccepted);
                if (alreadyAccepted)
                {
                    TempData["ErrorMessage"] = "Bu soru için daha önce bir cevap kabul edilmiş.";
                    return RedirectToAction("Details", new { id = answer.QuestionId });
                }

                answer.IsAccepted = true;
                await _context.SaveChangesAsync();
                await _experienceService.AddExperienceAsync(answer.UserId,
                    ExperienceService.ExperiencePoints.AcceptedAnswer, "Cevabı kabul edildi");
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Cevap kabul edildi. Cevap sahibi +{ExperienceService.ExperiencePoints.AcceptedAnswer} XP kazandı!";
                return RedirectToAction("Details", new { id = answer.QuestionId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Cevap {AnswerId} kabul edilirken hata oluştu.", id);
                TempData["ErrorMessage"] = "Cevap kabul edilirken bir hata oluştu.";
                return RedirectToAction("Index");
            }
        }
    }
}
