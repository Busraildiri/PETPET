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
using PetWork.Security;

namespace PetWork.Controllers
{
    public class AccountController : Controller
    {
        private readonly PetWorkDbContext _context;
        private readonly ExperienceService _experienceService;
        private readonly ILogger<AccountController> _logger;
        private readonly SecureMediaStorageService _mediaStorage;

        public AccountController(PetWorkDbContext context, ExperienceService experienceService, ILogger<AccountController> logger,
            SecureMediaStorageService mediaStorage)
        {
            _context = context;
            _experienceService = experienceService;
            _logger = logger;
            _mediaStorage = mediaStorage;
        }

        public IActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        
        [HttpPost]
        [SensitiveRateLimit("Login", nameof(LoginViewModel.UsernameOrEmail))]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            var maskedIdentifier = SensitiveDataMasker.Mask(model.UsernameOrEmail);
            _logger.LogInformation("Login girişimi: {Identifier}", maskedIdentifier);
            
            try
            {
                if (ModelState.IsValid)
                {
                    try
                    {
                        var user = _context.Users
                            .FirstOrDefault(u => u.Email == model.UsernameOrEmail || u.Username == model.UsernameOrEmail);

                        var hasher = new PasswordHasher<User>();
                        var result = PasswordAuthentication.Verify(hasher, user, model.Password);

                        if (user is null || result == PasswordVerificationResult.Failed)
                        {
                            _logger.LogWarning("Geçersiz giriş denemesi: {Identifier}", maskedIdentifier);
                            ModelState.AddModelError("", "Kullanıcı adı/e-posta veya şifre hatalı.");
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
                        
                        _logger.LogInformation("Kullanıcı başarıyla giriş yaptı. UserId: {UserId}", user.Id);
                        
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
                        var referenceCode = ErrorReferenceCode.Create(HttpContext);
                        _logger.LogError(ex,
                            "Giriş işlemi başarısız. ReferenceCode: {ReferenceCode}, Identifier: {Identifier}",
                            referenceCode, maskedIdentifier);
                        ModelState.AddModelError("", ErrorReferenceCode.UserMessage(
                            "Giriş sırasında beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.", referenceCode));
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
                var referenceCode = ErrorReferenceCode.Create(HttpContext);
                _logger.LogError(ex,
                    "Beklenmeyen giriş hatası. ReferenceCode: {ReferenceCode}, Identifier: {Identifier}",
                    referenceCode, maskedIdentifier);
                ModelState.AddModelError("", ErrorReferenceCode.UserMessage(
                    "Giriş sırasında beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.", referenceCode));
            }
            
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }
        
        public IActionResult Register()
        {
            return View();
        }
        
        [HttpPost]
        [SensitiveRateLimit("Registration", nameof(RegisterViewModel.Email))]
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
        [RequestSizeLimit(3 * 1024 * 1024)]
        [SensitiveRateLimit("Expensive")]
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

            if (!ModelState.IsValid)
                return View(model);

            StoredMedia? uploadedProfile = null;
            if (model.ProfileImage is { Length: > 0 })
            {
                try { uploadedProfile = await _mediaStorage.StoreAsync(model.ProfileImage, "profiles", 2 * 1024 * 1024); }
                catch (MediaValidationException exception)
                {
                    ModelState.AddModelError(nameof(model.ProfileImage), exception.Message);
                    return View(model);
                }
            }

            var completedProfileNow = string.IsNullOrWhiteSpace(user.Bio) && !string.IsNullOrWhiteSpace(model.Bio);
            user.Username = model.Username.Trim();
            user.Email = model.Email.Trim();
            user.Bio = model.Bio?.Trim();

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
                user.PasswordHash = hasher.HashPassword(user, model.NewPassword);

            var previousProfile = user.ProfileImage;
            if (uploadedProfile is not null)
            {
                user.ProfileImage = uploadedProfile.StorageKey;
                await _mediaStorage.DeleteAsync(previousProfile);
            }

            await _context.SaveChangesAsync();
            if (completedProfileNow)
                await _experienceService.AddExperienceAsync(user.Id,
                    ExperienceService.ExperiencePoints.ProfileCompletion, "Profil bilgilerini tamamladı");
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
