using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/questions")]
public sealed class MobileQuestionsApiController : ControllerBase
{
    private static readonly string[] DemoUsernames = ["kediSever", "goldenSahibi", "kusSever"];
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Beslenme", "Sağlık", "Davranış", "Bakım", "Yas ve Kayıp", "Diğer"
    };
    private readonly PetWorkDbContext _context;
    private readonly MobilePushNotificationService _pushNotifications;

    public MobileQuestionsApiController(PetWorkDbContext context, MobilePushNotificationService pushNotifications)
    {
        _context = context;
        _pushNotifications = pushNotifications;
    }

    [HttpGet]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<MobileQuestionsResponse>> Get(
        string? category = null,
        string sortBy = "newest",
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);
        var baseQuery = _context.Questions.AsNoTracking()
            .Where(question => question.User == null || !DemoUsernames.Contains(question.User.Username));

        var categories = await baseQuery
            .Where(question => question.Category != null && question.Category != "")
            .Select(question => question.Category!)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);

        var filtered = string.IsNullOrWhiteSpace(category)
            ? baseQuery
            : baseQuery.Where(question => question.Category == category);

        filtered = sortBy switch
        {
            "mostanswered" => filtered.OrderByDescending(question => question.Answers!.Count),
            "mostviewed" => filtered.OrderByDescending(question => question.ViewCount),
            _ => filtered.OrderByDescending(question => question.CreatedDate)
        };

        var totalCount = await filtered.CountAsync(cancellationToken);
        var items = await filtered.Take(take)
            .Select(question => new MobileQuestionSummary(
                question.Id,
                question.Title,
                question.Content.Length > 180 ? question.Content.Substring(0, 180) + "…" : question.Content,
                question.Category ?? "Genel",
                question.User == null ? "Topluluk üyesi" : question.User.Username,
                question.Answers == null ? 0 : question.Answers.Count(answer => !DemoUsernames.Contains(answer.User.Username)),
                question.ViewCount,
                question.Answers != null && question.Answers.Any(answer => answer.IsAccepted && !DemoUsernames.Contains(answer.User.Username)),
                question.CreatedDate))
            .ToListAsync(cancellationToken);

        return Ok(new MobileQuestionsResponse(items, categories, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MobileQuestionDetail>> GetById(int id, CancellationToken cancellationToken)
    {
        var question = await _context.Questions.AsNoTracking()
            .Where(item => item.Id == id && (item.User == null || !DemoUsernames.Contains(item.User.Username)))
            .Select(item => new MobileQuestionDetail(
                item.Id,
                item.Title,
                item.Content,
                item.Category ?? "Genel",
                item.Tags,
                item.User == null ? "Topluluk üyesi" : item.User.Username,
                item.ViewCount,
                item.CreatedDate,
                item.Answers == null
                    ? new List<MobileAnswerItem>()
                    : item.Answers
                        .Where(answer => !DemoUsernames.Contains(answer.User.Username))
                        .OrderByDescending(answer => answer.IsAccepted)
                        .ThenByDescending(answer => answer.UpVotes - answer.DownVotes)
                        .ThenBy(answer => answer.CreatedDate)
                        .Select(answer => new MobileAnswerItem(
                            answer.Id,
                            answer.Content,
                            answer.User == null ? "Topluluk üyesi" : answer.User.Username,
                            answer.IsAccepted,
                            answer.UpVotes - answer.DownVotes,
                            answer.CreatedDate))
                        .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return question is null ? NotFound() : Ok(question);
    }

    [HttpPost("{id:int}/answers")]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileAnswerItem>> PostAnswer(
        int id,
        MobileAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
            return Unauthorized(new { message = "Oturumun geçersiz veya süresi dolmuş. Lütfen yeniden giriş yap." });

        var question = await _context.Questions.AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new { candidate.Id, candidate.Title, candidate.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (question is null)
            return NotFound(new { message = "Yanıtlamak istediğin soru bulunamadı." });

        var username = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId.Value)
            .Select(user => user.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (username is null)
            return Unauthorized(new { message = "Bu oturuma ait kullanıcı bulunamadı." });

        var content = request.Content.Trim();
        if (content.Length < 2)
            return BadRequest(new { message = "Yanıt en az 2 görünür karakter içermelidir." });

        var answer = new Answer
        {
            QuestionId = id,
            UserId = userId.Value,
            Content = content,
            CreatedDate = DateTime.Now,
            IsAccepted = false,
            UpVotes = 0,
            DownVotes = 0
        };

        _context.Answers.Add(answer);
        MobileNotification? notification = null;
        if (question.UserId != userId.Value)
        {
            notification = new MobileNotification
            {
                UserId = question.UserId,
                Type = "question_answer",
                Title = "Soruna yeni yanıt",
                Body = $"@{username}, {question.Title} sorunu yanıtladı.",
                EntityType = "question",
                EntityId = question.Id,
                CreatedAt = DateTime.Now
            };
            _context.MobileNotifications.Add(notification);
        }
        await _context.SaveChangesAsync(cancellationToken);
        if (notification is not null) await _pushNotifications.SendAsync(notification, cancellationToken);

        return Created(string.Empty, new MobileAnswerItem(
            answer.Id,
            answer.Content,
            username,
            answer.IsAccepted,
            0,
            answer.CreatedDate));
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileQuestionSummary>> PostQuestion(
        MobileCreateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
            return Unauthorized(new { message = "Oturumun geçersiz veya süresi dolmuş. Lütfen yeniden giriş yap." });

        var user = await _context.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId.Value, cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "Bu oturuma ait kullanıcı bulunamadı." });

        var requestedCategory = request.Category.Trim();
        var category = AllowedCategories.FirstOrDefault(item =>
            item.Equals(requestedCategory, StringComparison.OrdinalIgnoreCase));
        if (category is null)
            return BadRequest(new { message = "Lütfen geçerli bir soru kategorisi seç." });

        var title = request.Title.Trim();
        var content = request.Content.Trim();
        if (title.Length < 5)
            return BadRequest(new { message = "Başlık en az 5 görünür karakter içermelidir." });
        if (content.Length < 10)
            return BadRequest(new { message = "Soru detayı en az 10 görünür karakter içermelidir." });

        var question = new Question
        {
            Title = title,
            Content = content,
            Category = category,
            UserId = user.Id,
            CreatedDate = DateTime.Now,
            ViewCount = 0
        };

        _context.Questions.Add(question);
        user.ExperiencePoints += 10;
        await _context.SaveChangesAsync(cancellationToken);

        var excerpt = question.Content.Length > 180
            ? question.Content[..180] + "…"
            : question.Content;

        return Created(string.Empty, new MobileQuestionSummary(
            question.Id,
            question.Title,
            excerpt,
            question.Category,
            user.Username,
            0,
            0,
            false,
            question.CreatedDate));
    }

    private int? GetAuthenticatedUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(subject, out var userId) ? userId : null;
    }
}

public sealed class MobileAnswerRequest
{
    [Required(ErrorMessage = "Yanıt metni gereklidir.")]
    [StringLength(5000, MinimumLength = 2, ErrorMessage = "Yanıt 2-5000 karakter arasında olmalıdır.")]
    public string Content { get; init; } = string.Empty;
}

public sealed class MobileCreateQuestionRequest
{
    [Required(ErrorMessage = "Soru başlığı gereklidir.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Başlık 5-200 karakter arasında olmalıdır.")]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Soru detayı gereklidir.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Soru detayı 10-4000 karakter arasında olmalıdır.")]
    public string Content { get; init; } = string.Empty;

    [Required(ErrorMessage = "Kategori seçmelisin.")]
    [StringLength(50)]
    public string Category { get; init; } = string.Empty;
}

public sealed record MobileQuestionsResponse(
    IReadOnlyList<MobileQuestionSummary> Items,
    IReadOnlyList<string> Categories,
    int TotalCount);

public sealed record MobileQuestionSummary(
    int Id,
    string Title,
    string Excerpt,
    string Category,
    string Username,
    int AnswerCount,
    int ViewCount,
    bool HasAcceptedAnswer,
    DateTime CreatedAt);

public sealed record MobileQuestionDetail(
    int Id,
    string Title,
    string Content,
    string Category,
    string? Tags,
    string Username,
    int ViewCount,
    DateTime CreatedAt,
    IReadOnlyList<MobileAnswerItem> Answers);

public sealed record MobileAnswerItem(
    int Id,
    string Content,
    string Username,
    bool IsAccepted,
    int Score,
    DateTime CreatedAt);
