using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/home")]
public sealed class MobileHomeApiController : ControllerBase
{
    private static readonly string[] DemoUsernames = ["kediSever", "goldenSahibi", "kusSever"];
    private readonly PetWorkDbContext _context;

    public MobileHomeApiController(PetWorkDbContext context) => _context = context;

    [HttpGet]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<MobileHomeResponse>> Get(CancellationToken cancellationToken)
    {
        var questionsQuery = _context.Questions.AsNoTracking()
            .Where(question => question.User == null || !DemoUsernames.Contains(question.User.Username));

        var questions = await questionsQuery.OrderByDescending(question => question.CreatedDate).Take(5)
            .Select(question => new MobileQuestionItem(
                question.Id, question.Title, question.Category ?? "Genel",
                question.User == null ? "Topluluk üyesi" : question.User.Username,
                question.Answers == null ? 0 : question.Answers.Count(answer => !DemoUsernames.Contains(answer.User.Username)),
                question.CreatedDate))
            .ToListAsync(cancellationToken);

        var blogs = await _context.BlogPosts.AsNoTracking().OrderByDescending(blog => blog.PublishedDate).Take(4)
            .Select(blog => new MobileStoryItem(
                blog.Id, "blog", blog.Title, blog.Category ?? "Blog",
                blog.Content.Length > 150 ? blog.Content.Substring(0, 150) + "…" : blog.Content,
                blog.ImageUrl, blog.PublishedDate))
            .ToListAsync(cancellationToken);

        var diseases = await _context.Diseases.AsNoTracking().OrderByDescending(disease => disease.PublishDate).Take(3)
            .Select(disease => new MobileStoryItem(
                disease.Id, "disease", disease.Name, disease.Category ?? "Sağlık",
                disease.Description.Length > 150 ? disease.Description.Substring(0, 150) + "…" : disease.Description,
                disease.FeaturedImage, disease.PublishDate))
            .ToListAsync(cancellationToken);

        var recipes = await _context.Recipes.AsNoTracking().OrderByDescending(recipe => recipe.PublishDate).Take(3)
            .Select(recipe => new MobileStoryItem(
                recipe.Id, "recipe", recipe.Title, recipe.PetType ?? recipe.AnimalType ?? "Tarif",
                recipe.Description.Length > 150 ? recipe.Description.Substring(0, 150) + "…" : recipe.Description,
                recipe.ImageUrl, recipe.PublishDate))
            .ToListAsync(cancellationToken);

        blogs = blogs.Select(item => item with
        {
            ImagePath = MobileContentImageResolver.Resolve("blogs", item.Id, item.Title, item.Category, null)
        }).ToList();
        diseases = diseases.Select(item => item with
        {
            ImagePath = MobileContentImageResolver.Resolve("diseases", item.Id, item.Title, item.Category, null)
        }).ToList();
        recipes = recipes.Select(item => item with
        {
            ImagePath = MobileContentImageResolver.Resolve("recipes", item.Id, item.Title, item.Category, null)
        }).ToList();

        var featured = diseases.Concat(recipes).Concat(blogs.Take(2))
            .OrderByDescending(item => item.PublishedAt).Take(7).ToList();

        return Ok(new MobileHomeResponse(
            featured, blogs, questions,
            await _context.Users.CountAsync(user => !DemoUsernames.Contains(user.Username), cancellationToken),
            await questionsQuery.CountAsync(cancellationToken)));
    }
}

public sealed record MobileHomeResponse(
    IReadOnlyList<MobileStoryItem> Featured,
    IReadOnlyList<MobileStoryItem> Blogs,
    IReadOnlyList<MobileQuestionItem> Questions,
    int MemberCount,
    int QuestionCount);

public sealed record MobileStoryItem(
    int Id, string Type, string Title, string Category, string Excerpt, string? ImagePath, DateTime PublishedAt);

public sealed record MobileQuestionItem(
    int Id, string Title, string Category, string Username, int AnswerCount, DateTime CreatedAt);
