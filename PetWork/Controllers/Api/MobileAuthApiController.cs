using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/auth")]
[EnableRateLimiting("mobile-auth")]
public sealed class MobileAuthApiController : ControllerBase
{
    private readonly PetWorkDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileAuthApiController> _logger;

    public MobileAuthApiController(
        PetWorkDbContext context,
        IConfiguration configuration,
        ILogger<MobileAuthApiController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<MobileAuthResponse>> Register(
        MobileRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (!request.AcceptTerms)
            return BadRequest(new MobileAuthError("Devam etmek için üyelik koşullarını kabul etmelisin."));

        var exists = await _context.Users.AsNoTracking()
            .AnyAsync(user => user.Username == username || user.Email == email, cancellationToken);

        if (exists)
            return Conflict(new MobileAuthError("Bu e-posta veya kullanıcı adı zaten kayıtlı."));

        var signingKey = GetSigningKey();

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = string.Empty,
            RegistrationDate = DateTime.Now,
            ExperiencePoints = 50
        };

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Mobil üyelik sırasında benzersiz alan çakışması oluştu.");
            return Conflict(new MobileAuthError("Bu e-posta veya kullanıcı adı zaten kayıtlı."));
        }

        _logger.LogInformation("Mobil üyelik tamamlandı. UserId: {UserId}", user.Id);
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var token = CreateToken(user, signingKey, expiresAt);
        return Created(string.Empty, new MobileAuthResponse(
            user.Id,
            user.Username,
            token,
            expiresAt,
            "Kaydın başarıyla tamamlandı."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<MobileAuthResponse>> Login(
        MobileLoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.EmailOrUsername.Trim();
        var normalizedIdentifier = identifier.ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(candidate =>
                candidate.Email.ToLower() == normalizedIdentifier ||
                candidate.Username.ToLower() == normalizedIdentifier,
                cancellationToken);

        if (user is null)
            return Unauthorized(new MobileAuthError("E-posta/kullanıcı adı veya şifre hatalı."));

        var hasher = new PasswordHasher<User>();
        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new MobileAuthError("E-posta/kullanıcı adı veya şifre hatalı."));

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var signingKey = GetSigningKey();
        var expiresAt = DateTime.UtcNow.AddDays(request.RememberMe ? 30 : 1);
        var token = CreateToken(user, signingKey, expiresAt);

        _logger.LogInformation("Mobil giriş tamamlandı. UserId: {UserId}", user.Id);
        return Ok(new MobileAuthResponse(
            user.Id,
            user.Username,
            token,
            expiresAt,
            "Tekrar hoş geldin."));
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var key = _configuration["MobileAuth:JwtKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("MobileAuth:JwtKey en az 32 bayt olacak şekilde güvenli yapılandırmada tanımlanmalıdır.");

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    private static string CreateToken(User user, SymmetricSecurityKey signingKey, DateTime expiresAt)
    {
        var token = new JwtSecurityToken(
            issuer: "PetWork",
            audience: "PetimMobile",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "Member"),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class MobileRegisterRequest
{
    [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-50 karakter arasında olmalıdır.")]
    [RegularExpression(@"^[\p{L}\p{N}._-]+$", ErrorMessage = "Kullanıcı adı yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girmelisin.")]
    [StringLength(254, ErrorMessage = "E-posta adresi çok uzun.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Şifre gereklidir.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Şifre tekrarı gereklidir.")]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; init; } = string.Empty;

    public bool AcceptTerms { get; init; }
}

public sealed class MobileLoginRequest
{
    [Required(ErrorMessage = "E-posta adresi veya kullanıcı adı gereklidir.")]
    [StringLength(254, ErrorMessage = "Giriş bilgisi çok uzun.")]
    public string EmailOrUsername { get; init; } = string.Empty;

    [Required(ErrorMessage = "Şifre gereklidir.")]
    [StringLength(100, ErrorMessage = "Şifre çok uzun.")]
    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public sealed record MobileAuthResponse(int UserId, string Username, string Token, DateTime ExpiresAt, string Message);
public sealed record MobileAuthError(string Message);
