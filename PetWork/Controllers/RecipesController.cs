using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Models.ViewModels;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using PetWork.Services;

namespace PetWork.Controllers
{
    public class RecipesController : Controller
    {
        private readonly PetWorkDbContext _context;
        private readonly ILogger<RecipesController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ExperienceService _experienceService;

        public RecipesController(PetWorkDbContext context, ILogger<RecipesController> logger,
            IWebHostEnvironment webHostEnvironment, ExperienceService experienceService)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _experienceService = experienceService;
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
        public async Task<IActionResult> Add(Recipe recipe, IFormFile ImageFile)
        {
            try
            {
                _logger.LogInformation("Add metoduna POST isteği geldi: {0}", JsonSerializer.Serialize(recipe));
                _logger.LogInformation("Dosya yükleme bilgisi: {0}", ImageFile != null ? "Dosya mevcut" : "Dosya yok");
                
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
                            // Dosya boyutu kontrolü (5MB)
                            if (ImageFile.Length > 5 * 1024 * 1024)
                            {
                                ModelState.AddModelError("ImageFile", "Dosya boyutu 5MB'dan küçük olmalıdır.");
                                return View(recipe);
                            }
                            
                            // Dosya uzantısı kontrolü
                            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                            var fileExtension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();
                            
                            if (!allowedExtensions.Contains(fileExtension))
                            {
                                ModelState.AddModelError("ImageFile", "Yalnızca .jpg, .jpeg, .png veya .gif uzantılı dosyaları yükleyebilirsiniz.");
                                return View(recipe);
                            }
                            
                            try
                            {
                                // Yükleme klasörünü oluştur (yoksa)
                                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "recipes");
                                if (!Directory.Exists(uploadsFolder))
                                {
                                    Directory.CreateDirectory(uploadsFolder);
                                }
                                
                                // Benzersiz dosya adı oluştur
                                string uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                                
                                // Dosyayı kaydet
                                using (var fileStream = new FileStream(filePath, FileMode.Create))
                                {
                                    ImageFile.CopyTo(fileStream);
                                }
                                
                                // Tarif modelindeki görsel yolunu güncelle
                                recipe.ImageUrl = "/img/recipes/" + uniqueFileName;
                                recipe.FeaturedImage = recipe.ImageUrl;
                                
                                _logger.LogInformation("Görsel başarıyla yüklendi: {0}", recipe.ImageUrl);
                            }
                            catch (Exception ex)
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
                            await _experienceService.AddExperienceAsync(userId.Value,
                                ExperienceService.ExperiencePoints.CreateRecipe, "Tarif paylaştı");
                            _logger.LogInformation("Tarif başarıyla eklendi, ID: {0}", recipe.Id);
                            TempData["SuccessMessage"] = $"Tarifiniz başarıyla eklendi! +{ExperienceService.ExperiencePoints.CreateRecipe} XP";
                            return RedirectToAction("Details", new { id = recipe.Id });
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Tarifiniz eklenirken bir hata oluştu. Lütfen tekrar deneyin.";
                            _logger.LogWarning("Tarif eklenemedi. SaveChanges 0 döndü.");
                        }
                    }
                    catch (Exception ex)
                    {
                        TempData["ErrorMessage"] = "Tarif eklenirken bir hata oluştu: " + ex.Message;
                        _logger.LogError(ex, "Tarif eklenirken hata");
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
                _logger.LogError(ex, "Tarif işleme sırasında beklenmeyen hata");
            }
            
            return View(recipe);
        }
    }
} 
