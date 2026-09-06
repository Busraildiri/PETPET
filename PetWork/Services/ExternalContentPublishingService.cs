using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class ExternalContentPublishingService : IExternalContentPublishingService
{
    private readonly PetWorkDbContext _db;
    public ExternalContentPublishingService(PetWorkDbContext db) => _db = db;

    public async Task<int> PublishTreeAsync(int sourceId, int adminUserId, bool reviewed, CancellationToken cancellationToken)
    {
        var published = await PublishAsync(sourceId, adminUserId, reviewed, cancellationToken) ? 1 : 0;
        var childIds = await _db.ExternalContentSources.Where(x => x.ParentSourceId == sourceId)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var childId in childIds)
            if (await PublishAsync(childId, adminUserId, reviewed, cancellationToken)) published++;
        return published;
    }

    public async Task<bool> PublishAsync(int sourceId, int adminUserId, bool reviewed, CancellationToken cancellationToken)
    {
        var source = await _db.ExternalContentSources.Include(x => x.ParentSource)
            .SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
        if (source is null) return false;
        if (source.ReviewStatus is ExternalContentReviewStatuses.Rejected or ExternalContentReviewStatuses.Archived)
            return false;
        if (!source.WasTranslated && !source.SourceLanguage.Equals("tr", StringComparison.OrdinalIgnoreCase))
            return false;

        if (source.LocalContentId is null)
        {
            var title = source.TranslatedTitle ?? source.SourceTitle;
            var text = source.TranslatedText ?? source.OriginalText;
            switch (source.ContentType)
            {
                case ExternalContentTypes.Question:
                    var question = new Question { Title = title, Content = text, Category = source.Category ?? "Genel",
                        Tags = source.Tags, UserId = adminUserId, CreatedDate = Now() };
                    _db.Questions.Add(question); await _db.SaveChangesAsync(cancellationToken); source.LocalContentId = question.Id;
                    break;
                case ExternalContentTypes.Answer:
                    if (source.ParentSource?.LocalContentId is not int questionId) return false;
                    var answer = new Answer { Content = text, QuestionId = questionId, UserId = adminUserId, CreatedDate = Now() };
                    _db.Answers.Add(answer); await _db.SaveChangesAsync(cancellationToken); source.LocalContentId = answer.Id;
                    break;
                case ExternalContentTypes.Guide:
                    var guide = new Guide { Title = title, Description = text.Length > 300 ? text[..300] + "…" : text,
                        Content = text, Category = source.Category ?? "Genel", AnimalType = "Genel", Level = "Bilgilendirme",
                        UserId = adminUserId, PublishDate = Now() };
                    _db.Guides.Add(guide); await _db.SaveChangesAsync(cancellationToken); source.LocalContentId = guide.Id;
                    break;
                case ExternalContentTypes.Disease:
                    var disease = new Disease { Name = title, Description = text, Category = source.Category ?? "Genel",
                        AnimalType = "Genel", PetType = "Genel", SeverityLevel = "Bilgilendirme",
                        PublishDate = Now(), FeaturedImage = "img/hero-health-v2.png" };
                    _db.Diseases.Add(disease); await _db.SaveChangesAsync(cancellationToken); source.LocalContentId = disease.Id;
                    break;
                case ExternalContentTypes.BlogPost:
                    var blogCategory = string.IsNullOrWhiteSpace(source.Category) || source.Category == "Genel"
                        ? BlogCategoryClassifier.Classify(title, text)
                        : source.Category;
                    var blog = new BlogPost { Title = title, Content = text, Category = blogCategory,
                        UserId = adminUserId, PublishedDate = Now(), PublishDate = Now(),
                        FeaturedImage = "img/hero-community-v2.png", ImageUrl = "img/hero-community-v2.png" };
                    _db.BlogPosts.Add(blog); await _db.SaveChangesAsync(cancellationToken); source.LocalContentId = blog.Id;
                    break;
                case ExternalContentTypes.Recipe:
                    var recipe = new Recipe { Title = title, Description = "Kaynağı belirtilmiş, Türkçeye uyarlanmış köpek ödül tarifi.",
                        Ingredients = text, Instructions = text, Content = text, PetType = "Köpek", AnimalType = "Köpek",
                        DietType = "Ödül", PreparationTime = 30, PrepTime = "30 dakika", Difficulty = "Orta",
                        UserId = adminUserId, CreatedDate = Now(), PublishDate = Now(),
                        FeaturedImage = "img/hero-recipes-v2.png", ImageUrl = "img/hero-recipes-v2.png" };
                    _db.Recipes.Add(recipe); await _db.SaveChangesAsync(cancellationToken); source.LocalContentId = recipe.Id;
                    break;
                default:
                    return false;
            }
        }

        source.ReviewStatus = reviewed ? ExternalContentReviewStatuses.Approved : ExternalContentReviewStatuses.PublishedUnreviewed;
        source.ReviewedByUserId = reviewed ? adminUserId : null;
        source.ReviewedAt = reviewed ? Now() : null;
        _db.ContentImportAudits.Add(new ContentImportAudit
        {
            ExternalContentSource = source,
            EventType = reviewed ? "Approved" : "AutoPublished",
            Outcome = "Success",
            Details = reviewed ? "Editör kontrolü tamamlandı." : "İçerik otomatik yayınlandı; editör kontrolü bekliyor.",
            PerformedByUserId = adminUserId,
            CreatedAt = Now()
        });
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
