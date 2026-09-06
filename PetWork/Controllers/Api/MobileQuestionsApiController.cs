using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/questions")]
public sealed class MobileQuestionsApiController : ControllerBase
{
    private static readonly string[] DemoUsernames = ["kediSever", "goldenSahibi", "kusSever"];
    private readonly PetWorkDbContext _context;
    private readonly IConfiguration _configuration;

    public MobileQuestionsApiController(PetWorkDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
    [EnableRateLimiting("mobile-content")]
    public async Task<ActionResult<MobileAnswerItem>> PostAnswer(
        int id,
        MobileAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
            return Unauthorized(new { message = "Oturumun geçersiz veya süresi dolmuş. Lütfen yeniden giriş yap." });

        var questionExists = await _context.Questions.AsNoTracking()
            .AnyAsync(question => question.Id == id, cancellationToken);
        if (!questionExists)
            return NotFound(new { message = "Yanıtlamak istediğin soru bulunamadı." });

        var username = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId.Value)
            .Select(user => user.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (username is null)
            return Unauthorized(new { message = "Bu oturuma ait kullanıcı bulunamadı." });

        var answer = new Answer
        {
            QuestionId = id,
            UserId = userId.Value,
            Content = request.Content.Trim(),
            CreatedDate = DateTime.Now,
            IsAccepted = false,
            UpVotes = 0,
            DownVotes = 0
        };

        _context.Answers.Add(answer);
        await _context.SaveChangesAsync(cancellationToken);

        return Created(string.Empty, new MobileAnswerItem(
            answer.Id,
            answer.Content,
            username,
            answer.IsAccepted,
            0,
            answer.CreatedDate));
    }

    private int? GetAuthenticatedUserId()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var key = _configuration["MobileAuth:JwtKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            return null;

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(
                authorization["Bearer ".Length..].Trim(),
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer = true,
                    ValidIssuer = "PetWork",
                    ValidateAudience = true,
                    ValidAudience = "PetimMobile",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                },
                out _);

            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(subject, out var userId) ? userId : null;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed class MobileAnswerRequest
{
    [Required(ErrorMessage = "Yanıt metni gereklidir.")]
    [StringLength(5000, MinimumLength = 2, ErrorMessage = "Yanıt 2-5000 karakter arasında olmalıdır.")]
    public string Content { get; init; } = string.Empty;
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
