using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Controllers.Api;

[ApiController]
[Route("api/mobile/content")]
public sealed class MobileContentApiController : ControllerBase
{
    private static readonly HashSet<string> AllowedKinds =
        new(["all", "guides", "diseases", "recipes", "blogs", "grief"], StringComparer.OrdinalIgnoreCase);
    private readonly PetWorkDbContext _context;
    public MobileContentApiController(PetWorkDbContext context) => _context = context;

    [HttpGet("{kind}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<IReadOnlyList<MobileContentItem>>> List(
        string kind, string? search, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(kind.Trim())) return NotFound();
        var items = await Query(kind, cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(item => item.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                                       item.Summary.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                                       item.Category.Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }
        return Ok(items);
    }

    [HttpGet("{kind}/{id:int}")]
    public async Task<ActionResult<MobileContentItem>> Detail(
        string kind, int id, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(kind.Trim())) return NotFound();
        var item = (await Query(kind, cancellationToken)).FirstOrDefault(value => value.Id == id);
        return item is null ? NotFound() : Ok(item);
    }

    private async Task<List<MobileContentItem>> Query(string rawKind, CancellationToken cancellationToken)
    {
        var kind = rawKind.Trim().ToLowerInvariant();
        if (kind == "all")
        {
            var all = new List<MobileContentItem>();
            all.AddRange(await Query("guides", cancellationToken));
            all.AddRange(await Query("diseases", cancellationToken));
            all.AddRange(await Query("recipes", cancellationToken));
            all.AddRange(await Query("blogs", cancellationToken));
            return all.OrderByDescending(x => x.PublishedAt).ToList();
        }
        List<MobileContentItem> items;
        string sourceType;

        switch (kind)
        {
            case "guides":
                sourceType = ExternalContentTypes.Guide;
                items = await _context.Guides.AsNoTracking().OrderByDescending(x => x.PublishDate).Take(100)
                    .Select(x => new MobileContentItem(x.Id, kind, x.Title, x.Description, x.Content,
                        x.Category ?? "Bakım", x.AnimalType, x.Level, null, x.PublishDate, x.ViewCount,
                        null, null, null)).ToListAsync(cancellationToken);
                break;
            case "diseases":
                sourceType = ExternalContentTypes.Disease;
                items = await _context.Diseases.AsNoTracking().OrderByDescending(x => x.PublishDate).Take(100)
                    .Select(x => new MobileContentItem(x.Id, kind, x.Name, x.Description,
                        BuildDiseaseBody(x.Symptoms, x.Treatments ?? x.Treatment, x.Prevention),
                        x.Category ?? "Sağlık", x.AnimalType ?? x.PetType, x.SeverityLevel,
                        x.FeaturedImage, x.PublishDate, x.ViewCount, null, null, null))
                    .ToListAsync(cancellationToken);
                break;
            case "recipes":
                sourceType = ExternalContentTypes.Recipe;
                items = await _context.Recipes.AsNoTracking().OrderByDescending(x => x.PublishDate).Take(100)
                    .Select(x => new MobileContentItem(x.Id, kind, x.Title, x.Description,
                        "Malzemeler\n" + x.Ingredients + "\n\nHazırlanışı\n" + x.Instructions,
                        x.DietType ?? "Tarif", x.AnimalType ?? x.PetType, x.PrepTime,
                        x.ImageUrl, x.PublishDate, x.ViewCount, null, null, null))
                    .ToListAsync(cancellationToken);
                break;
            case "blogs":
            case "grief":
                sourceType = ExternalContentTypes.BlogPost;
                var blogs = _context.BlogPosts.AsNoTracking();
                if (kind == "grief") blogs = blogs.Where(x => x.Category == "Yas ve Kayıp");
                items = await blogs.OrderByDescending(x => x.PublishedDate).Take(100)
                    .Select(x => new MobileContentItem(x.Id, kind, x.Title,
                        x.Content.Length > 220 ? x.Content.Substring(0, 220) + "…" : x.Content,
                        x.Content, x.Category ?? "Blog", null, null, x.ImageUrl,
                        x.PublishedDate, x.ViewCount, null, null, null))
                    .ToListAsync(cancellationToken);
                break;
            default:
                return [];
        }

        var ids = items.Select(x => x.Id).ToArray();
        var sources = await _context.ExternalContentSources.AsNoTracking()
            .Where(x => x.ContentType == sourceType && x.LocalContentId.HasValue && ids.Contains(x.LocalContentId.Value))
            .OrderByDescending(x => x.ImportedAt)
            .ToListAsync(cancellationToken);
        var sourceById = sources.GroupBy(x => x.LocalContentId!.Value).ToDictionary(x => x.Key, x => x.First());

        return items.Select(item =>
        {
            var illustrated = item with
            {
                ImagePath = MobileContentImageResolver.Resolve(item.Kind, item.Id, item.Title, item.Category, item.AnimalType)
            };
            return sourceById.TryGetValue(item.Id, out var source)
                ? illustrated with { SourceUrl = source.SourceUrl, SourceName = source.Provider, Attribution = source.AttributionText }
                : illustrated with { SourceName = "PetWork bilgi merkezi" };
        }).ToList();
    }

    private static string BuildDiseaseBody(string? symptoms, string? treatment, string? prevention) =>
        $"Belirtiler\n{symptoms ?? "Bilgi eklenmemiş."}\n\nTedavi ve destek\n{treatment ?? "Veteriner hekime danışın."}\n\nKorunma\n{prevention ?? "Bilgi eklenmemiş."}";
}

public sealed record MobileContentItem(
    int Id, string Kind, string Title, string Summary, string Body, string Category,
    string? AnimalType, string? Meta, string? ImagePath, DateTime PublishedAt, int ViewCount,
    string? SourceUrl, string? SourceName, string? Attribution);

public static class MobileContentImageResolver
{
    private const string Root = "img/content-generated/";
    private const string UniqueRoot = "img/content-unique/";

    public static string Resolve(string kind, int id, string title, string category, string? animalType)
    {
        var assetKind = kind == "grief" ? "blogs" : kind;
        if (assetKind is "guides" or "diseases" or "recipes" or "blogs")
            return $"{UniqueRoot}{assetKind}-{id}.jpg";

        var value = $"{title} {category} {animalType}".ToLowerInvariant();
        return kind switch
        {
            "diseases" when Has(value, "kedi", "cat", "toksoplaz") => Root + "disease-cat.png",
            "diseases" when Has(value, "köpek", "dog", "canine", "parvo", "kalp") => Root + "disease-dog.png",
            "diseases" => Root + "disease-lab.png",
            "recipes" when Has(value, "kuş", "kus", "bird") => Root + "recipe-bird.png",
            "recipes" when Has(value, "kedi", "cat") => Root + "recipe-cat-chicken.png",
            "recipes" => Root + "recipe-dog-treats.png",
            "guides" when Has(value, "kuş", "kus", "muhabbet") => Root + "recipe-bird.png",
            "guides" when Has(value, "köpek", "kopek", "dog", "chihuahua", "akita", "eğitim", "egitim") => Root + "guide-dog-training.png",
            "guides" when Has(value, "kedi", "cat", "bakım", "bakim", "feromon") => Root + "guide-cat-care.png",
            "guides" => Root + "blog-community.png",
            "grief" => Root + "blog-remembrance.png",
            "blogs" when Has(value, "yas", "kayıp", "kayip", "ölüm", "otanazi", "ötanazi", "suçluluk") => Root + "blog-remembrance.png",
            "blogs" when Has(value, "sağlık", "saglik", "veteriner", "hastalık") => Root + "disease-lab.png",
            "blogs" when Has(value, "bakım", "bakim", "kedi") => Root + "guide-cat-care.png",
            "blogs" => Root + "blog-community.png",
            _ => Root + "blog-community.png"
        };
    }

    private static bool Has(string value, params string[] terms) => terms.Any(value.Contains);
}
