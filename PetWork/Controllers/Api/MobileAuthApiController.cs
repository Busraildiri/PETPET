using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;
using PetWork.Security;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/auth")]
public sealed class MobileAuthApiController : ControllerBase
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RememberedSessionLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(1);
    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);

    private readonly PetWorkDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MobileAuthApiController> _logger;
    private readonly IPasswordResetEmailSender _passwordResetSender;
    private readonly EmailVerificationService _emailVerificationService;
    private readonly IWebHostEnvironment _environment;
    private readonly SecureMediaStorageService _mediaStorage;
    private readonly DailyCostQuotaService _dailyCostQuota;

    public MobileAuthApiController(
        PetWorkDbContext context,
        IConfiguration configuration,
        ILogger<MobileAuthApiController> logger,
        IPasswordResetEmailSender passwordResetSender,
        EmailVerificationService emailVerificationService,
        IWebHostEnvironment environment,
        SecureMediaStorageService mediaStorage,
        DailyCostQuotaService dailyCostQuota)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _passwordResetSender = passwordResetSender;
        _emailVerificationService = emailVerificationService;
        _environment = environment;
        _mediaStorage = mediaStorage;
        _dailyCostQuota = dailyCostQuota;
    }

    [HttpPost("register")]
    [SensitiveRateLimit("CodeSend", nameof(MobileRegisterRequest.Email))]
    public async Task<ActionResult<object>> Register(MobileRegisterRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (!request.AcceptTerms)
            return BadRequest(new MobileAuthError("Devam etmek için üyelik koşullarını kabul etmelisin."));

        var exists = await _context.Users.AsNoTracking()
            .AnyAsync(user => user.Username == username || user.Email == email, cancellationToken);
        if (exists)
            return Conflict(new MobileAuthError("Bu e-posta veya kullanıcı adı zaten kayıtlı."));

        var quotaRejection = await EnforceEmailQuotaAsync($"email:{email}", cancellationToken);
        if (quotaRejection is not null) return quotaRejection;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = string.Empty,
            RegistrationDate = DateTime.UtcNow,
            ExperiencePoints = 50,
            IsEmailVerified = false
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, request.Password);
        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var challenge = await CreateEmailChallengeAsync(user, request.RememberMe, cancellationToken);
            _logger.LogInformation("Mobil üyelik oluşturuldu; e-posta doğrulaması bekleniyor. UserId: {UserId}", user.Id);
            return Created(string.Empty, challenge);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(exception, "Mobil üyelik sırasında benzersiz alan çakışması oluştu.");
            return Conflict(new MobileAuthError("Bu e-posta veya kullanıcı adı zaten kayıtlı."));
        }
        catch (EmailDeliveryException exception)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(CancellationToken.None);
            return OperationalFailure(exception, StatusCodes.Status503ServiceUnavailable,
                "Üyelik doğrulama e-postası gönderimi",
                "Doğrulama e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin.");
        }
    }

    [HttpPost("login")]
    [SensitiveRateLimit("Login", nameof(MobileLoginRequest.EmailOrUsername))]
    public async Task<ActionResult<MobileAuthResponse>> Login(MobileLoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var identifier = request.EmailOrUsername.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(
                candidate => candidate.Email.ToLower() == identifier || candidate.Username.ToLower() == identifier,
                cancellationToken);
            var hasher = new PasswordHasher<User>();
            var verification = PasswordAuthentication.Verify(hasher, user, request.Password);
            if (user is null || verification == PasswordVerificationResult.Failed) return InvalidCredentials();
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = hasher.HashPassword(user, request.Password);

            if (!user.IsEmailVerified)
            {
                var quotaRejection = await EnforceEmailQuotaAsync($"email:{user.Email}", cancellationToken);
                if (quotaRejection is not null) return quotaRejection;
                try { return Ok(await CreateEmailChallengeAsync(user, request.RememberMe, cancellationToken)); }
                catch (EmailDeliveryException exception)
                {
                    return OperationalFailure(exception, StatusCodes.Status503ServiceUnavailable,
                        "Giriş doğrulama e-postası gönderimi",
                        "Doğrulama e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin.");
                }
            }

            var response = await CreateSessionAsync(user, request.RememberMe, "Tekrar hoş geldin.", cancellationToken);
            _logger.LogInformation("Mobil giriş tamamlandı. UserId: {UserId}", user.Id);
            return Ok(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OperationalFailure(exception, StatusCodes.Status500InternalServerError,
                "Mobil giriş", "Giriş sırasında beklenmeyen bir sunucu hatası oluştu. Lütfen tekrar deneyin.");
        }
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [SensitiveRateLimit("CodeVerify", nameof(MobileVerifyEmailRequest.ChallengeToken))]
    public async Task<ActionResult<MobileAuthResponse>> VerifyEmail(MobileVerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var verification = _emailVerificationService.Verify(request.ChallengeToken, request.Code);
        if (!verification.Succeeded)
            return Unauthorized(new MobileAuthError(verification.Failure == EmailVerificationFailure.Invalid ? "Doğrulama kodu hatalı." : "Doğrulama isteğinin süresi doldu. Yeniden giriş yap."));
        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Id == verification.UserId, cancellationToken);
        if (user is null) return Unauthorized(new MobileAuthError("Kullanıcı hesabı bulunamadı."));
        user.IsEmailVerified = true;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(await CreateSessionAsync(user, verification.RememberMe, "E-posta adresin doğrulandı.", cancellationToken));
    }

    [HttpPost("resend-email-code")]
    [AllowAnonymous]
    [SensitiveRateLimit("CodeSend", nameof(MobileResendEmailCodeRequest.ChallengeToken))]
    public async Task<ActionResult<MobileEmailChallengeResponse>> ResendEmailCode(MobileResendEmailCodeRequest request, CancellationToken cancellationToken)
    {
        if (!_emailVerificationService.TryConsumeContext(request.ChallengeToken, out var userId, out var rememberMe))
            return Unauthorized(new MobileAuthError("Doğrulama isteğinin süresi doldu. Yeniden giriş yap."));
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == userId && !candidate.IsEmailVerified, cancellationToken);
        if (user is null) return Unauthorized(new MobileAuthError("Kullanıcı hesabı bulunamadı."));
        var quotaRejection = await EnforceEmailQuotaAsync($"email:{user.Email}", cancellationToken);
        if (quotaRejection is not null) return quotaRejection;
        try { return Ok(await CreateEmailChallengeAsync(user, rememberMe, cancellationToken)); }
        catch (EmailDeliveryException exception)
        {
            return OperationalFailure(exception, StatusCodes.Status503ServiceUnavailable,
                "Doğrulama e-postasının yeniden gönderimi",
                "Doğrulama e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin.");
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MobileMeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş."));
        var user = await _context.Users.AsNoTracking()
            .Where(candidate => candidate.Id == userId.Value)
            .Select(candidate => new MobileMeResponse(candidate.Id, candidate.Username, candidate.Email))
            .FirstOrDefaultAsync(cancellationToken);
        return user is null ? Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş.")) : Ok(user);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [SensitiveRateLimit("PasswordReset", nameof(MobileRefreshRequest.RefreshToken))]
    public async Task<ActionResult<MobileAuthResponse>> Refresh(MobileRefreshRequest request, CancellationToken cancellationToken)
    {
        var hash = HashToken(request.RefreshToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;
        var session = await _context.MobileAuthSessions
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.RefreshTokenHash == hash, cancellationToken);
        if (session is null || session.RevokedAt is not null || session.RefreshExpiresAt <= now)
            return Unauthorized(new MobileAuthError("Oturum yenilenemedi. Lütfen yeniden giriş yap."));

        var rawRefreshToken = CreateOpaqueToken();
        session.RefreshTokenHash = HashToken(rawRefreshToken);
        session.LastUsedAt = now;
        session.AccessExpiresAt = now.Add(AccessTokenLifetime);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(CreateResponse(session.User, session, rawRefreshToken, "Oturumun yenilendi."));
    }

    [HttpPost("change-password")]
    [Authorize]
    [SensitiveRateLimit("Login")]
    public async Task<ActionResult<MobileAuthResponse>> ChangePassword(MobileChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş."));
        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);
        if (user is null) return Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş."));

        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new MobileAuthError("Mevcut şifren yanlış."));
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.NewPassword) != PasswordVerificationResult.Failed)
            return BadRequest(new MobileAuthError("Yeni şifren mevcut şifrenden farklı olmalı."));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        var now = DateTime.UtcNow;
        await _context.MobileAuthSessions
            .Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
        var response = await CreateSessionAsync(user, true, "Şifren başarıyla değiştirildi.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [SensitiveRateLimit("CodeSend", nameof(MobileForgotPasswordRequest.Email))]
    public async Task<IActionResult> ForgotPassword(MobileForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        const string genericMessage = "Bu e-posta ile bir hesap varsa şifre yenileme bağlantısı gönderildi.";
        var email = request.Email.Trim().ToLowerInvariant();
        var quotaRejection = await EnforceEmailQuotaAsync($"email:{email}", cancellationToken);
        if (quotaRejection is not null) return quotaRejection;
        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);
        if (user is null) return Ok(new MobileAuthError(genericMessage));

        var now = DateTime.UtcNow;
        await _context.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.UsedAt, now), cancellationToken);
        var rawToken = CreateOpaqueToken();
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(PasswordResetLifetime)
        };
        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _passwordResetSender.SendAsync(user.Email, user.Username, rawToken, resetToken.ExpiresAt, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Şifre sıfırlama iletisi gönderilemedi. UserId: {UserId}", user.Id);
        }
        return Ok(new MobileAuthError(genericMessage));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [SensitiveRateLimit("PasswordReset", nameof(MobileResetPasswordRequest.Token))]
    public async Task<IActionResult> ResetPassword(MobileResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var hash = HashToken(request.Token);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;
        var resetToken = await _context.PasswordResetTokens
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (resetToken is null || resetToken.UsedAt is not null || resetToken.ExpiresAt <= now)
            return BadRequest(new MobileAuthError("Şifre yenileme bağlantısı geçersiz veya süresi dolmuş."));

        resetToken.User.PasswordHash = new PasswordHasher<User>().HashPassword(resetToken.User, request.NewPassword);
        resetToken.UsedAt = now;
        await _context.MobileAuthSessions
            .Where(session => session.UserId == resetToken.UserId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new MobileAuthError("Şifren yenilendi. Yeni şifrenle giriş yapabilirsin."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = GetSessionId();
        if (sessionId is null) return Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş."));
        var session = await _context.MobileAuthSessions.FirstOrDefaultAsync(item => item.Id == sessionId.Value, cancellationToken);
        if (session is not null && session.RevokedAt is null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpDelete("account")]
    [Authorize]
    [SensitiveRateLimit("Login")]
    public async Task<IActionResult> DeleteAccount(MobileDeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş."));

        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);
        if (user is null) return Unauthorized(new MobileAuthError("Oturumun geçersiz veya süresi dolmuş."));
        if (user.IsAdmin) return StatusCode(StatusCodes.Status403Forbidden, new MobileAuthError("Yönetici hesabı mobil uygulamadan silinemez."));
        if (!string.Equals(request.Confirmation.Trim(), "SİL", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new MobileAuthError("Hesabı silmek için onay alanına SİL yazmalısın."));

        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return BadRequest(new MobileAuthError("Şifren yanlış."));

        var ownedFiles = new List<string?> { user.ProfileImage };
        ownedFiles.AddRange(await _context.Pets.Where(item => item.UserId == user.Id).Select(item => item.ProfileImage).ToListAsync(cancellationToken));
        ownedFiles.AddRange(await _context.Pets.Where(item => item.UserId == user.Id).Select(item => item.Image).ToListAsync(cancellationToken));
        ownedFiles.AddRange(await _context.Recipes.Where(item => item.UserId == user.Id).Select(item => item.FeaturedImage).ToListAsync(cancellationToken));
        ownedFiles.AddRange(await _context.Recipes.Where(item => item.UserId == user.Id).Select(item => item.ImageUrl).ToListAsync(cancellationToken));
        ownedFiles.AddRange(await _context.BlogPosts.Where(item => item.UserId == user.Id).Select(item => item.FeaturedImage).ToListAsync(cancellationToken));
        ownedFiles.AddRange(await _context.BlogPosts.Where(item => item.UserId == user.Id).Select(item => item.ImageUrl).ToListAsync(cancellationToken));
        ownedFiles.AddRange(await _context.SocialPosts.Where(item => item.UserId == user.Id).Select(item => item.ImagePath).ToListAsync(cancellationToken));

        var ownedStorageKeys = ownedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct()
            .ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // These user relationships are configured as RESTRICT and must be removed first.
        await _context.SocialPostReports.Where(item => item.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await _context.SocialComments.Where(item => item.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await _context.Answers.Where(item => item.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await _context.Questions.Where(item => item.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await _context.Recipes.Where(item => item.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        await _context.MobileMediaAssets.Where(asset => ownedStorageKeys.Contains(asset.StorageKey))
            .ExecuteDeleteAsync(cancellationToken);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Mobil hesap silindi. UserId: {UserId}", user.Id);
        return NoContent();
    }

    private UnauthorizedObjectResult InvalidCredentials() =>
        Unauthorized(new MobileAuthError("E-posta/kullanıcı adı veya şifre hatalı."));

    private ObjectResult OperationalFailure(Exception exception, int statusCode, string operation, string userMessage)
    {
        var referenceCode = ErrorReferenceCode.Create(HttpContext);
        _logger.LogError(exception,
            "{Operation} başarısız. ReferenceCode: {ReferenceCode}, TraceIdentifier: {TraceIdentifier}",
            operation, referenceCode, HttpContext.TraceIdentifier);
        return StatusCode(statusCode, new MobileAuthError(
            ErrorReferenceCode.UserMessage(userMessage, referenceCode), referenceCode));
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private Guid? GetSessionId() =>
        Guid.TryParse(User.FindFirstValue("sid"), out var sessionId) ? sessionId : null;

    private async Task<MobileAuthResponse> CreateSessionAsync(User user, bool rememberMe, string message, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rawRefreshToken = CreateOpaqueToken();
        var session = new MobileAuthSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash = HashToken(rawRefreshToken),
            CreatedAt = now,
            LastUsedAt = now,
            AccessExpiresAt = now.Add(AccessTokenLifetime),
            RefreshExpiresAt = now.Add(rememberMe ? RememberedSessionLifetime : SessionLifetime)
        };
        _context.MobileAuthSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return CreateResponse(user, session, rawRefreshToken, message);
    }

    private async Task<MobileEmailChallengeResponse> CreateEmailChallengeAsync(User user, bool rememberMe, CancellationToken cancellationToken)
    {
        var challenge = await _emailVerificationService.CreateAsync(user.Id, user.Email, user.Username, rememberMe, cancellationToken);
        return new(true, challenge.ChallengeToken, challenge.ExpiresAt, challenge.MaskedEmail,
            $"{challenge.MaskedEmail} adresine 6 haneli doğrulama kodu gönderdik.");
    }

    private async Task<ObjectResult?> EnforceEmailQuotaAsync(string subject, CancellationToken cancellationToken)
    {
        var decision = await _dailyCostQuota.TryConsumeAsync("TransactionalEmail", subject, cancellationToken);
        if (decision.Allowed)
        {
            Response.Headers["X-Daily-Quota-Remaining"] = decision.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return null;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return DailyCostQuotaAttribute.CreateRejectedResult(seconds);
    }

    private MobileAuthResponse CreateResponse(User user, MobileAuthSession session, string refreshToken, string message) =>
        new(user.Id, user.Username, CreateAccessToken(user, session), session.AccessExpiresAt, refreshToken, session.RefreshExpiresAt, message);

    private string CreateAccessToken(User user, MobileAuthSession session)
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
                new Claim("sid", session.Id.ToString())
            ],
            expires: session.AccessExpiresAt,
            signingCredentials: new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var key = _configuration["MobileAuth:JwtKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("MobileAuth:JwtKey güvenli yapılandırmada tanımlanmalıdır.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    private static string CreateOpaqueToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

}

public sealed class MobileRegisterRequest
{
    [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-50 karakter arasında olmalıdır.")]
    [RegularExpression(@"^[\p{L}\p{N}._-]+$", ErrorMessage = "Kullanıcı adı yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.")]
    public string Username { get; init; } = string.Empty;
    [Required(ErrorMessage = "E-posta adresi gereklidir."), EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girmelisin."), StringLength(254)]
    public string Email { get; init; } = string.Empty;
    [Required(ErrorMessage = "Şifre gereklidir."), PasswordPolicy]
    public string Password { get; init; } = string.Empty;
    [Required(ErrorMessage = "Şifre tekrarı gereklidir."), Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; init; } = string.Empty;
    public bool AcceptTerms { get; init; }
    public bool RememberMe { get; init; } = true;
}

public sealed class MobileLoginRequest
{
    [Required(ErrorMessage = "E-posta adresi veya kullanıcı adı gereklidir."), StringLength(254)]
    public string EmailOrUsername { get; init; } = string.Empty;
    [Required(ErrorMessage = "Şifre gereklidir."), StringLength(100)]
    public string Password { get; init; } = string.Empty;
    public bool RememberMe { get; init; }
}

public sealed class MobileRefreshRequest
{
    [Required, StringLength(200)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class MobileChangePasswordRequest
{
    [Required(ErrorMessage = "Mevcut şifre gereklidir."), StringLength(100)]
    public string CurrentPassword { get; init; } = string.Empty;
    [Required(ErrorMessage = "Yeni şifre gereklidir."), PasswordPolicy]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class MobileForgotPasswordRequest
{
    [Required(ErrorMessage = "E-posta adresi gereklidir."), EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girmelisin."), StringLength(254)]
    public string Email { get; init; } = string.Empty;
}

public sealed class MobileResetPasswordRequest
{
    [Required, StringLength(200)]
    public string Token { get; init; } = string.Empty;
    [Required(ErrorMessage = "Yeni şifre gereklidir."), PasswordPolicy]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class MobileDeleteAccountRequest
{
    [Required(ErrorMessage = "Şifre gereklidir."), StringLength(100)]
    public string Password { get; init; } = string.Empty;
    [Required(ErrorMessage = "SİL onayı gereklidir."), StringLength(3)]
    public string Confirmation { get; init; } = string.Empty;
}

public sealed class MobileVerifyEmailRequest
{
    [Required, StringLength(200)] public string ChallengeToken { get; init; } = string.Empty;
    [Required, RegularExpression(@"^\d{6}$")] public string Code { get; init; } = string.Empty;
}

public sealed class MobileResendEmailCodeRequest
{
    [Required, StringLength(200)] public string ChallengeToken { get; init; } = string.Empty;
}

public sealed record MobileEmailChallengeResponse(
    bool RequiresEmailVerification,
    string ChallengeToken,
    DateTime ExpiresAt,
    string MaskedEmail,
    string Message);

public sealed record MobileAuthResponse(
    int UserId,
    string Username,
    string Token,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    string Message);
public sealed record MobileMeResponse(int UserId, string Username, string Email);
public sealed record MobileAuthError(string Message, string? ReferenceCode = null);
