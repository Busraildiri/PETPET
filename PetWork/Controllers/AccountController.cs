using Microsoft.AspNetCore.Mvc;
using PetWork.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using PetWork.Data;
using PetWork.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace PetWork.Controllers
{
    public class AccountController : Controller
    {
        private readonly PetWorkDbContext _context;
        private readonly ExperienceService _experienceService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(PetWorkDbContext context, ExperienceService experienceService, ILogger<AccountController> logger)
        {
            _context = context;
            _experienceService = experienceService;
            _logger = logger;
        }

        public IActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            _logger.LogInformation("Login girişimi: {0}", model.UsernameOrEmail);
            
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        var user = _context.Users
                            .FirstOrDefault(u => u.Email == model.UsernameOrEmail || u.Username == model.UsernameOrEmail);

                        if (user == null)
                        {
                            _logger.LogWarning("Kullanıcı bulunamadı: {0}", model.UsernameOrEmail);
                            ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                            return View(model);
                        }

                        var hasher = new PasswordHasher<User>();
                        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

                        if (result == PasswordVerificationResult.Failed)
                        {
                            _logger.LogWarning("Geçersiz şifre: {0}", model.UsernameOrEmail);
                            ModelState.AddModelError("", "Şifre yanlış.");
                            return View(model);
                        }

                        // Oturum aç (cookie tabanlı)
                        HttpContext.Session.SetInt32("UserId", user.Id);
                        HttpContext.Session.SetString("Username", user.Username);
                        HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());
                        
                        // Session kontrolü
                        if (HttpContext.Session.GetInt32("UserId") == null)
                        {
                            _logger.LogError("Session oluşturma hatası: UserId null");
                            ModelState.AddModelError("", "Oturum başlatılırken bir hata oluştu.");
                            return View(model);
                        }
                        
                        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı: {0}, ID: {1}", user.Username, user.Id);
                        
                        // Günlük giriş yapmak için deneyim puanı ekle
                        await _experienceService.AddExperienceAsync(user.Id, ExperienceService.ExperiencePoints.DailyLogin, "Günlük giriş yaptı");

                        TempData["SuccessMessage"] = "Giriş başarılı! Hoş geldiniz " + user.Username;
                        
                        // ReturnUrl kontrolü
                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        {
                            return Redirect(returnUrl);
                        }
                        
                        return RedirectToAction("Index", "Home");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Giriş işlemi sırasında hata: {0}", model.UsernameOrEmail);
                        ModelState.AddModelError("", "Giriş sırasında bir hata oluştu: " + ex.Message);
                        // Debug için hatayı daha detaylı göster
                        ViewBag.Error = ex.ToString();
                    }
                }
                else
                {
                    _logger.LogWarning("Geçersiz model durumu");
                    foreach (var state in ModelState)
                    {
                        foreach (var error in state.Value.Errors)
                        {
                            _logger.LogWarning($"Doğrulama hatası {state.Key}: {error.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Beklenmeyen hata: {0}", model.UsernameOrEmail);
                ModelState.AddModelError("", "Beklenmeyen bir hata oluştu: " + ex.Message);
                ViewBag.Error = ex.ToString();
            }
            
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }
        
        public IActionResult Register()
        {
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Kullanım koşullarını kabul etme kontrolü
            if (!model.AcceptTerms)
            {
                ModelState.AddModelError("AcceptTerms", "Devam etmek için kullanım koşullarını kabul etmelisiniz.");
            }

            if (ModelState.IsValid)
            {
                // E-posta veya kullanıcı adı zaten var mı kontrolü
                var exists = _context.Users.Any(u => u.Email == model.Email || u.Username == model.Username);
                if (exists)
                {
                    ModelState.AddModelError("", "Bu e-posta veya kullanıcı adı zaten kayıtlı.");
                    return View(model);
                }

                // Şifre hash'leme
                var hasher = new PasswordHasher<User>();
                var user = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    PasswordHash = "", // aşağıda set edilecek
                    RegistrationDate = DateTime.Now,
                    ExperiencePoints = 50 // Yeni üyelere başlangıç puanı
                };
                user.PasswordHash = hasher.HashPassword(user, model.Password);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                
                // Yeni kullanıcı için rozet kontrolü yap
                await _experienceService.AddExperienceAsync(user.Id, 0, "Üyelik tamamlandı");

                // Otomatik giriş veya login sayfasına yönlendirme
                TempData["SuccessMessage"] = "Kayıt işlemi başarılı! Şimdi giriş yapabilirsiniz. 50 deneyim puanı kazandınız!";
                return RedirectToAction("Login", "Account");
            }
            return View(model);
        }
        
        public IActionResult Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(u => u.Id == userId.Value);
            if (user == null)
                return RedirectToAction("Login");

            return RedirectToAction("Index", "Profile");
        }
        
        public IActionResult EditProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login");

            var user = _context.Users.FirstOrDefault(item => item.Id == userId.Value);
            if (user == null)
                return RedirectToAction("Login");

            var viewModel = new EditProfileViewModel
            {
                Username = user.Username,
                Email = user.Email,
                Bio = user.Bio
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return RedirectToAction("Login");

            if (await _context.Users.AnyAsync(item => item.Id != user.Id && (item.Username == model.Username || item.Email == model.Email)))
                ModelState.AddModelError(string.Empty, "Bu kullanıcı adı veya e-posta başka bir hesap tarafından kullanılıyor.");

            var hasher = new PasswordHasher<User>();
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword) || hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut şifreniz doğru değil.");
            }

            if (model.ProfileImage is { Length: > 0 })
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
                if (model.ProfileImage.Length > 2 * 1024 * 1024 || !allowedExtensions.Contains(extension))
                    ModelState.AddModelError(nameof(model.ProfileImage), "En fazla 2 MB boyutunda JPG, PNG veya WebP görsel yükleyebilirsiniz.");
            }

            if (!ModelState.IsValid)
                return View(model);

            user.Username = model.Username.Trim();
            user.Email = model.Email.Trim();
            user.Bio = model.Bio?.Trim();

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
                user.PasswordHash = hasher.HashPassword(user, model.NewPassword);

            if (model.ProfileImage is { Length: > 0 })
            {
                var extension = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var profileDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "profiles");
                Directory.CreateDirectory(profileDirectory);
                await using var stream = System.IO.File.Create(Path.Combine(profileDirectory, fileName));
                await model.ProfileImage.CopyToAsync(stream);
                user.ProfileImage = $"img/profiles/{fileName}";
            }

            await _context.SaveChangesAsync();
            HttpContext.Session.SetString("Username", user.Username);
            TempData["SuccessMessage"] = "Profiliniz güncellendi.";
            return RedirectToAction("Index", "Profile");
        }
        
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
} 
