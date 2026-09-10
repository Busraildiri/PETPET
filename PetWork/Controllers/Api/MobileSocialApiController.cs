using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;
using PetWork.Security;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/social/posts")]
public sealed class MobileSocialApiController : ControllerBase
{
    private readonly PetWorkDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly MobilePushNotificationService _pushNotifications;
    private readonly MobileMediaStorageService _mediaStorage;

    public MobileSocialApiController(PetWorkDbContext context, IWebHostEnvironment environment,
        MobilePushNotificationService pushNotifications, MobileMediaStorageService mediaStorage)
    {
        _context = context;
        _environment = environment;
        _pushNotifications = pushNotifications;
        _mediaStorage = mediaStorage;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MobileSocialPostResponse>>> GetPosts(
        CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        var posts = await _context.SocialPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted)
            .OrderByDescending(post => post.CreatedAt)
            .Take(50)
            .Select(post => new MobileSocialPostResponse(
                post.Id,
                post.User.Username,
                post.User.IsAdmin,
                post.Body,
                post.Tags,
                post.ImagePath,
                post.CreatedAt,
                post.Comments.Count(comment => !comment.IsDeleted),
                post.Likes.Count,
                currentUserId.HasValue && post.Likes.Any(like => like.UserId == currentUserId.Value),
                currentUserId.HasValue && post.Saves.Any(save => save.UserId == currentUserId.Value)))
            .ToListAsync(cancellationToken);

        return Ok(posts);
    }

    [HttpPost]
    [Authorize]
    [Consumes("application/json")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<MobileSocialPostResponse>> CreatePost(
        MobileCreateSocialPostRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı. Lütfen yeniden giriş yap." });

        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "Kullanıcı hesabı bulunamadı." });

        var body = request.Body.Trim();
        if (body.Length < 2)
            return BadRequest(new { message = "Paylaşım en az 2 görünür karakter içermelidir." });
        var tags = NormalizeTags(request.Tags);
        string? imagePath = null;

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            var imageResult = SaveImage(
                request.ImageBase64,
                request.ImageContentType);
            if (imageResult.Error is not null)
                return BadRequest(new { message = imageResult.Error });

            imagePath = imageResult.Path;
        }

        var post = new SocialPost
        {
            UserId = user.Id,
            Body = body,
            Tags = tags,
            ImagePath = imagePath,
            CreatedAt = DateTime.Now
        };

        _context.SocialPosts.Add(post);
        user.ExperiencePoints += 10;
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetPosts), new MobileSocialPostResponse(
            post.Id,
            user.Username,
            user.IsAdmin,
            post.Body,
            post.Tags,
            post.ImagePath,
            post.CreatedAt,
            0,
            0,
            false,
            false));
    }

    [HttpGet("{postId:int}/comments")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MobileSocialCommentResponse>>> GetComments(
        int postId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetUserId();
        var postExists = await _context.SocialPosts
            .AsNoTracking()
            .AnyAsync(post => post.Id == postId && !post.IsDeleted, cancellationToken);

        if (!postExists)
            return NotFound(new { message = "Gönderi bulunamadı." });

        var comments = await _context.SocialComments
            .AsNoTracking()
            .Where(comment => comment.SocialPostId == postId && !comment.IsDeleted)
            .OrderBy(comment => comment.CreatedAt)
            .Take(200)
            .Select(comment => new MobileSocialCommentResponse(
                comment.Id,
                comment.User.Username,
                comment.User.IsAdmin,
                comment.Body,
                comment.CreatedAt,
                comment.Likes.Count,
                currentUserId.HasValue && comment.Likes.Any(like => like.UserId == currentUserId.Value)))
            .ToListAsync(cancellationToken);

        return Ok(comments);
    }

    [HttpPost("{postId:int}/comments")]
    [Authorize]
    [SensitiveRateLimit("Expensive")]
    public async Task<ActionResult<MobileSocialCommentResponse>> CreateComment(
        int postId,
        MobileCreateSocialCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı. Lütfen yeniden giriş yap." });

        var post = await _context.SocialPosts
            .Where(candidate => candidate.Id == postId && !candidate.IsDeleted)
            .Select(candidate => new { candidate.Id, candidate.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (post is null)
            return NotFound(new { message = "Gönderi bulunamadı." });

        var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "Kullanıcı hesabı bulunamadı." });

        var body = request.Body.Trim();
        if (body.Length < 1)
            return BadRequest(new { message = "Yorum boş bırakılamaz." });

        var comment = new SocialComment
        {
            SocialPostId = postId,
            UserId = user.Id,
            Body = body,
            CreatedAt = DateTime.Now
        };

        _context.SocialComments.Add(comment);
        user.ExperiencePoints += 5;
        MobileNotification? notification = null;
        if (post.UserId != userId)
        {
            notification = new MobileNotification
            {
                UserId = post.UserId,
                Type = "social_comment",
                Title = "Paylaşımına yeni yorum",
                Body = $"@{user.Username} paylaşımına yorum yaptı.",
                EntityType = "social_post",
                EntityId = postId,
                CreatedAt = DateTime.Now
            };
            _context.MobileNotifications.Add(notification);
        }
        await _context.SaveChangesAsync(cancellationToken);
        if (notification is not null) await _pushNotifications.SendAsync(notification, cancellationToken);

        return CreatedAtAction(nameof(GetComments), new { postId }, new MobileSocialCommentResponse(
            comment.Id,
            user.Username,
            user.IsAdmin,
            comment.Body,
            comment.CreatedAt,
            0,
            false));
    }

    [HttpPut("{postId:int}/like")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileReactionResponse>> SetPostLike(
        int postId,
        MobileToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        var post = await _context.SocialPosts
            .Where(candidate => candidate.Id == postId && !candidate.IsDeleted)
            .Select(candidate => new { candidate.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (post is null)
            return NotFound(new { message = "Gönderi bulunamadı." });

        var like = await _context.SocialPostLikes.FirstOrDefaultAsync(
            candidate => candidate.SocialPostId == postId && candidate.UserId == userId.Value,
            cancellationToken);
        MobileNotification? notification = null;
        if (request.Active && like is null)
        {
            _context.SocialPostLikes.Add(new SocialPostLike { SocialPostId = postId, UserId = userId.Value });
            if (post.UserId != userId.Value)
            {
                var username = await _context.Users.Where(user => user.Id == userId.Value)
                    .Select(user => user.Username).SingleAsync(cancellationToken);
                notification = new MobileNotification
                {
                    UserId = post.UserId, Type = "social_like", Title = "Paylaşımın beğenildi",
                    Body = $"@{username} paylaşımını beğendi.", EntityType = "social_post", EntityId = postId,
                    CreatedAt = DateTime.Now
                };
                _context.MobileNotifications.Add(notification);
            }
        }
        else if (!request.Active && like is not null)
            _context.SocialPostLikes.Remove(like);

        await _context.SaveChangesAsync(cancellationToken);
        if (notification is not null) await _pushNotifications.SendAsync(notification, cancellationToken);
        var count = await _context.SocialPostLikes.CountAsync(candidate => candidate.SocialPostId == postId, cancellationToken);
        return Ok(new MobileReactionResponse(request.Active, count));
    }

    [HttpPut("{postId:int}/save")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileReactionResponse>> SetPostSave(
        int postId,
        MobileToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        if (!await _context.SocialPosts.AnyAsync(post => post.Id == postId && !post.IsDeleted, cancellationToken))
            return NotFound(new { message = "Gönderi bulunamadı." });

        var save = await _context.SocialPostSaves.FirstOrDefaultAsync(
            candidate => candidate.SocialPostId == postId && candidate.UserId == userId.Value,
            cancellationToken);
        if (request.Active && save is null)
            _context.SocialPostSaves.Add(new SocialPostSave { SocialPostId = postId, UserId = userId.Value });
        else if (!request.Active && save is not null)
            _context.SocialPostSaves.Remove(save);

        await _context.SaveChangesAsync(cancellationToken);
        var count = await _context.SocialPostSaves.CountAsync(candidate => candidate.SocialPostId == postId, cancellationToken);
        return Ok(new MobileReactionResponse(request.Active, count));
    }

    [HttpPut("comments/{commentId:int}/like")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileReactionResponse>> SetCommentLike(
        int commentId,
        MobileToggleReactionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı." });
        var comment = await _context.SocialComments
            .Where(candidate => candidate.Id == commentId && !candidate.IsDeleted && !candidate.SocialPost.IsDeleted)
            .Select(candidate => new { candidate.UserId, candidate.SocialPostId })
            .FirstOrDefaultAsync(cancellationToken);
        if (comment is null)
            return NotFound(new { message = "Yorum bulunamadı." });

        var like = await _context.SocialCommentLikes.FirstOrDefaultAsync(
            candidate => candidate.SocialCommentId == commentId && candidate.UserId == userId.Value,
            cancellationToken);
        MobileNotification? notification = null;
        if (request.Active && like is null)
        {
            _context.SocialCommentLikes.Add(new SocialCommentLike { SocialCommentId = commentId, UserId = userId.Value });
            if (comment.UserId != userId.Value)
            {
                var username = await _context.Users.Where(user => user.Id == userId.Value)
                    .Select(user => user.Username).SingleAsync(cancellationToken);
                notification = new MobileNotification
                {
                    UserId = comment.UserId, Type = "social_comment_like", Title = "Yorumun beğenildi",
                    Body = $"@{username} yorumunu beğendi.", EntityType = "social_post", EntityId = comment.SocialPostId,
                    CreatedAt = DateTime.Now
                };
                _context.MobileNotifications.Add(notification);
            }
        }
        else if (!request.Active && like is not null)
            _context.SocialCommentLikes.Remove(like);

        await _context.SaveChangesAsync(cancellationToken);
        if (notification is not null) await _pushNotifications.SendAsync(notification, cancellationToken);
        var count = await _context.SocialCommentLikes.CountAsync(candidate => candidate.SocialCommentId == commentId, cancellationToken);
        return Ok(new MobileReactionResponse(request.Active, count));
    }

    [HttpDelete("{postId:int}")]
    [Authorize]
    public async Task<IActionResult> DeletePost(int postId, CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı. Lütfen yeniden giriş yap." });

        var user = await _context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.Id, candidate.IsAdmin })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "Kullanıcı hesabı bulunamadı." });

        var post = await _context.SocialPosts
            .FirstOrDefaultAsync(candidate => candidate.Id == postId && !candidate.IsDeleted, cancellationToken);
        if (post is null)
            return NotFound(new { message = "Gönderi bulunamadı." });

        if (post.UserId != user.Id && !user.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bu gönderiyi silme yetkin yok." });

        post.IsDeleted = true;
        await _mediaStorage.StageDeleteAsync(post.ImagePath, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{postId:int}/report")]
    [Authorize]
    [SensitiveRateLimit("Expensive")]
    public async Task<IActionResult> ReportPost(
        int postId,
        MobileReportSocialPostRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { message = "Oturum bilgisi doğrulanamadı. Lütfen yeniden giriş yap." });

        var post = await _context.SocialPosts
            .AsNoTracking()
            .Where(candidate => candidate.Id == postId && !candidate.IsDeleted)
            .Select(candidate => new { candidate.Id, candidate.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (post is null)
            return NotFound(new { message = "Gönderi bulunamadı." });

        if (post.UserId == userId)
            return BadRequest(new { message = "Kendi gönderini bildirmek yerine silebilirsin." });

        var alreadyReported = await _context.SocialPostReports
            .AnyAsync(report => report.SocialPostId == postId && report.UserId == userId, cancellationToken);
        if (alreadyReported)
            return Conflict(new { message = "Bu gönderiyi daha önce bildirdin." });

        var reason = request.Reason.Trim();
        if (reason.Length < 3)
            return BadRequest(new { message = "Bildirim nedeni en az 3 görünür karakter içermelidir." });

        _context.SocialPostReports.Add(new SocialPostReport
        {
            SocialPostId = postId,
            UserId = userId,
            Reason = reason,
            CreatedAt = DateTime.Now
        });
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Bildirimin alındı. Teşekkür ederiz." });
    }

    private (string? Path, string? Error) SaveImage(
        string imageBase64,
        string? contentType)
    {
        const int maximumImageSize = 8 * 1024 * 1024;
        if (imageBase64.Length > 11_500_000)
            return (null, "Fotoğraf en fazla 8 MB olabilir.");

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(imageBase64);
        }
        catch (FormatException)
        {
            return (null, "Fotoğraf verisi okunamadı.");
        }

        if (imageBytes.Length is <= 0 or > maximumImageSize)
            return (null, "Fotoğraf en fazla 8 MB olabilir.");

        var extension = contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };

        if (extension is null)
            return (null, "Yalnızca JPG, PNG veya WebP fotoğrafları yükleyebilirsin.");

        if (!HasValidImageSignature(imageBytes, imageBytes.Length, extension))
            return (null, "Seçilen dosya geçerli bir fotoğraf değil.");

        return (_mediaStorage.StageUpload("social", contentType!.ToLowerInvariant(), imageBytes), null);
    }

    private static bool HasValidImageSignature(byte[] bytes, int length, string extension)
    {
        return extension switch
        {
            ".jpg" => length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => length >= 12 &&
                       bytes[..4].SequenceEqual("RIFF"u8.ToArray()) &&
                       bytes[8..12].SequenceEqual("WEBP"u8.ToArray()),
            _ => false
        };
    }

    private static string? NormalizeTags(string? tagText)
    {
        if (string.IsNullOrWhiteSpace(tagText)) return null;

        var normalized = tagText.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim().TrimStart('#'))
            .Where(tag => tag.Length is > 0 and <= 30)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed class MobileCreateSocialPostRequest
{
    [Required(ErrorMessage = "Paylaşım metni gereklidir.")]
    [StringLength(2000, MinimumLength = 2, ErrorMessage = "Paylaşım 2-2000 karakter arasında olmalıdır.")]
    public string Body { get; init; } = string.Empty;

    [StringLength(300, ErrorMessage = "Etiketler çok uzun.")]
    public string? Tags { get; init; }

    public string? ImageBase64 { get; init; }

    [StringLength(100, ErrorMessage = "Fotoğraf türü geçersiz.")]
    public string? ImageContentType { get; init; }
}

public sealed record MobileSocialPostResponse(
    int Id,
    string Username,
    bool IsAdmin,
    string Body,
    string? Tags,
    string? ImagePath,
    DateTime CreatedAt,
    int CommentCount,
    int LikeCount,
    bool IsLikedByMe,
    bool IsSavedByMe);

public sealed class MobileCreateSocialCommentRequest
{
    [Required(ErrorMessage = "Yorum metni gereklidir.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Yorum 1-1000 karakter arasında olmalıdır.")]
    public string Body { get; init; } = string.Empty;
}

public sealed record MobileSocialCommentResponse(
    int Id,
    string Username,
    bool IsAdmin,
    string Body,
    DateTime CreatedAt,
    int LikeCount,
    bool IsLikedByMe);

public sealed class MobileToggleReactionRequest
{
    public bool Active { get; init; }
}

public sealed record MobileReactionResponse(bool Active, int Count);

public sealed class MobileReportSocialPostRequest
{
    [Required(ErrorMessage = "Bildirim nedeni gereklidir.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Bildirim nedeni 3-500 karakter arasında olmalıdır.")]
    public string Reason { get; init; } = string.Empty;
}
