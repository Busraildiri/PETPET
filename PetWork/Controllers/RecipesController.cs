using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Models.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using PetWork.Services;
using PetWork.Security;

namespace PetWork.Controllers
{
    public class RecipesController : Controller
    {
        private readonly PetWorkDbContext _context;
        private readonly ILogger<RecipesController> _logger;
        private readonly ExperienceService _experienceService;
        private readonly SecureMediaStorageService _mediaStorage;

        public RecipesController(PetWorkDbContext context, ILogger<RecipesController> logger,
            ExperienceService experienceService, SecureMediaStorageService mediaStorage)
        {
            _context = context;
            _logger = logger;
            _experienceService = experienceService;
            _mediaStorage = mediaStorage;
        }

        public IActionResult Index(string? animalType = null, string? difficulty = null, string? sortOption = "En Yeni", DateTime? startDate = null, DateTime? endDate = null)
        {
            // Veritabanından tarifleri çek
            var recipes = _context.Recipes
                .Include(r => r.User)
                .AsQueryable();

            // Hayvan türü filtresi uygula
            if (!string.IsNullOrEmpty(animalType))
            {
                recipes = recipes.Where(r => r.AnimalType == animalType);
            }

            // Zorluk seviyesi filtresi uygula
            if (!string.IsNullOrEmpty(difficulty))
            {
                recipes = recipes.Where(r => r.Difficulty == difficulty);
            }
            
            // Tarih filtresi uygula
            if (startDate.HasValue)
            {
                recipes = recipes.Where(r => r.PublishDate >= startDate.Value);
            }
            
            if (endDate.HasValue)
            {
                recipes = recipes.Where(r => r.PublishDate <= endDate.Value);
            }
            
            // Sıralama seçeneğini uygula
            switch (sortOption)
            {
                case "En Yeni":
                    recipes = recipes.OrderByDescending(r => r.PublishDate);
                    break;
                case "En Eski":
                    recipes = recipes.OrderBy(r => r.PublishDate);
                    break;
                case "En Popüler":
                    recipes = recipes.OrderByDescending(r => r.ViewCount);
                    break;
                case "En Az Popüler":
                    recipes = recipes.OrderBy(r => r.ViewCount);
                    break;
                default:
                    recipes = recipes.OrderByDescending(r => r.PublishDate);
                    break;
            }

            // Kategorileri veritabanından al
            var animalTypes = _context.Recipes
                .Where(r => r.AnimalType != null)
                .Select(r => r.AnimalType)
                .Distinct()
                .ToList();

            // Zorluk seviyelerini veritabanından al
            var difficulties = _context.Recipes
                .Where(r => r.Difficulty != null)
                .Select(r => r.Difficulty)
                .Distinct()
                .ToList();

            // Veri yoksa varsayılan değerler
            if (animalTypes.Count == 0)
            {
                animalTypes = new List<string>
                {
                    "Kedi",
                    "Köpek",
                    "Kuş",
                    "Kemirgen",
                    "Akvaryum",
                    "Diğer"
                };
            }

            if (difficulties.Count == 0)
            {
                difficulties = new List<string>
                {
                    "Kolay",
                    "Orta",
                    "Zor"
                };
            }

            var viewModel = new RecipeViewModel
            {
                Recipes = recipes.ToList(),
                AnimalTypes = animalTypes,
                Difficulties = difficulties,
                SelectedAnimalType = animalType,
                SelectedDifficulty = difficulty,
                SelectedSortOption = sortOption,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(viewModel);
        }

        public IActionResult Details(int id)
        {
            // Veritabanından tarifi ID'ye göre bul
            var recipe = _context.Recipes
                .Include(r => r.User)
                .FirstOrDefault(r => r.Id == id);

            if (recipe == null)
            {
                return NotFound();
            }

            // Görüntülenme sayısını artır
            recipe.ViewCount++;
            _context.SaveChanges();
            ViewBag.ExternalSource = _context.ExternalContentSources.AsNoTracking().FirstOrDefault(x =>
                x.ContentType == ExternalContentTypes.Recipe && x.LocalContentId == recipe.Id &&
                x.ReviewStatus != ExternalContentReviewStatuses.Rejected && x.ReviewStatus != ExternalContentReviewStatuses.Archived);

            return View(recipe);
        }
        
        public IActionResult Add()
        {
            // Kullanıcı giriş yapmış mı kontrol et
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Tarif eklemek için giriş yapmalısınız.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Add", "Recipes") });
            }
            
            return View(new Recipe());
        }
        
        [HttpPost]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [SensitiveRateLimit("Expensive")]
        public async Task<IActionResult> Add(Recipe recipe, IFormFile ImageFile)
        {
            try
            {
                _logger.LogInformation(
                    "Tarif ekleme isteği alındı. TitleLength: {TitleLength}, DescriptionLength: {DescriptionLength}, HasImage: {HasImage}",
                    recipe.Title?.Length ?? 0, recipe.Description?.Length ?? 0, ImageFile is { Length: > 0 });
                _logger.LogInformation("Dosya yükleme bilgisi: {0}", ImageFile != null ? "Dosya mevcut" : "Dosya yok");
                
                // User property için validasyon hatasını temizle
                if (ModelState.ContainsKey("User"))
                {
                    ModelState.Remove("User");
                }
                
                if (ModelState.IsValid)
                {
                    StoredMedia? uploadedImage = null;
                    try
                    {
                        // Session'dan kullanıcı bilgisini al
                        var userId = HttpContext.Session.GetInt32("UserId");
                        _logger.LogInformation("Session UserId: {0}", userId);
                        
                        if (!userId.HasValue)
                        {
                            TempData["ErrorMessage"] = "Tarif eklemek için giriş yapmalısınız.";
                            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Add", "Recipes") });
                        }

                        // Debug bilgisi
                        _logger.LogInformation($"Tarif ekleme: UserId={userId.Value}, Title={recipe.Title}");

                        // Tarifi hazırla
                        recipe.UserId = userId.Value;
                        recipe.CreatedDate = DateTime.Now;
                        recipe.PublishDate = DateTime.Now;
                        recipe.ViewCount = 0;
                        
                        // Dosya Yükleme İşlemi
                        if (ImageFile != null && ImageFile.Length > 0)
                        {
                            try
                            {
                                uploadedImage = await _mediaStorage.StoreAsync(ImageFile, "recipes", 5 * 1024 * 1024);
                                recipe.ImageUrl = uploadedImage.StorageKey;
                                recipe.FeaturedImage = recipe.ImageUrl;
                                
                                _logger.LogInformation("Görsel başarıyla yüklendi: {0}", recipe.ImageUrl);
                            }
                            catch (MediaValidationException ex)
                            {
                                _logger.LogError(ex, "Dosya yükleme hatası");
                                ModelState.AddModelError("ImageFile", "Dosya yüklenirken bir hata oluştu: " + ex.Message);
                                return View(recipe);
                            }
                        }
                        else
                        {
                            // Dosya yüklenmemişse varsayılan görsel kullan
                            recipe.ImageUrl = "img/hero-recipes-v2.png";
                            recipe.FeaturedImage = "img/hero-recipes-v2.png";
                        }
                        
                        // İlişkili User nesnesini yükle
                        var user = _context.Users.Find(userId.Value);
                        if (user != null)
                        {
                            recipe.User = user;
                        }

                        // Veritabanına ekle
                        _context.Recipes.Add(recipe);
                        
                        // Database durumunu logla
                        _logger.LogInformation("Veritabanı değişiklikleri: {0}", 
                            string.Join(", ", _context.ChangeTracker.Entries()
                                .Where(e => e.State == EntityState.Added)
                                .Select(e => e.Entity.GetType().Name)));
                        
                        int result = await _context.SaveChangesAsync();

                        // Başarılı mı kontrol et
                        if (result > 0)
                        {
                            uploadedImage = null;
                            await _experienceService.AddExperienceAsync(userId.Value,
                                ExperienceService.ExperiencePoints.CreateRecipe, "Tarif paylaştı");
                            _logger.LogInformation("Tarif başarıyla eklendi, ID: {0}", recipe.Id);
                            TempData["SuccessMessage"] = $"Tarifiniz başarıyla eklendi! +{ExperienceService.ExperiencePoints.CreateRecipe} XP";
                            return RedirectToAction("Details", new { id = recipe.Id });
                        }
                        else
                        {
                            if (uploadedImage is not null) _mediaStorage.Discard(uploadedImage.StorageKey);
                            TempData["ErrorMessage"] = "Tarifiniz eklenirken bir hata oluştu. Lütfen tekrar deneyin.";
                            _logger.LogWarning("Tarif eklenemedi. SaveChanges 0 döndü.");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (uploadedImage is not null) _mediaStorage.Discard(uploadedImage.StorageKey);
                        var referenceCode = ErrorReferenceCode.Create(HttpContext);
                        TempData["ErrorMessage"] = ErrorReferenceCode.UserMessage(
                            "Tarif eklenirken beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.", referenceCode);
                        _logger.LogError(ex, "Tarif eklenemedi. ReferenceCode: {ReferenceCode}", referenceCode);
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
                var referenceCode = ErrorReferenceCode.Create(HttpContext);
                TempData["ErrorMessage"] = ErrorReferenceCode.UserMessage(
                    "Tarif işlenirken beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.", referenceCode);
                _logger.LogError(ex, "Tarif işleme hatası. ReferenceCode: {ReferenceCode}", referenceCode);
            }
            
            return View(recipe);
        }
    }
} 
