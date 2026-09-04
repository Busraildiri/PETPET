using Microsoft.AspNetCore.Mvc;
using PetWork.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using PetWork.Data;
using PetWork.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
            // Örnek kullanıcı verileri - gerçek uygulamada veritabanından gelecek
            var viewModel = new EditProfileViewModel
            {
                Username = "pet_lover",
                Email = "pet_lover@example.com",
                Bio = "Hayvan sevgisiyle dolu bir evcil hayvan tutkunu. İki kedim ve bir köpeğim var. Boş zamanlarımda hayvan barınaklarında gönüllülük yapıyorum."
            };
            
            return View(viewModel);
        }
        
        [HttpPost]
        public IActionResult EditProfile(EditProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Gerçek uygulamada kullanıcı güncelleme işlemleri burada gerçekleştirilir
                return RedirectToAction("Profile");
            }
            
            return View(model);
        }
        
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
} 