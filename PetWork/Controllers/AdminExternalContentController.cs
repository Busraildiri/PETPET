using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Models.ViewModels;
using PetWork.Services;
using PetWork.Security;

namespace PetWork.Controllers;

[Route("Admin/ExternalContent")]
public sealed class AdminExternalContentController : Controller
{
    private readonly PetWorkDbContext _db;
    private readonly IExternalContentImportService _imports;
    private readonly IExternalContentPublishingService _publisher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminExternalContentController> _logger;
    public AdminExternalContentController(PetWorkDbContext db, IExternalContentImportService imports,
        IExternalContentPublishingService publisher, IConfiguration configuration,
        ILogger<AdminExternalContentController> logger)
    { _db = db; _imports = imports; _publisher = publisher; _configuration = configuration; _logger = logger; }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? provider, string? status, string? contentType, string? query,
        CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(cancellationToken)) return RedirectToAction("Login", "Account");
        var items = _db.ExternalContentSources.Include(x => x.Children).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(provider)) items = items.Where(x => x.Provider == provider);
        if (!string.IsNullOrWhiteSpace(status)) items = items.Where(x => x.ReviewStatus == status);
        if (!string.IsNullOrWhiteSpace(contentType)) items = items.Where(x => x.ContentType == contentType);
        return View(new ExternalContentListViewModel
        {
            Items = await items.OrderByDescending(x => x.ImportedAt).Take(250).ToListAsync(cancellationToken),
            Providers = _imports.Providers, Provider = provider, Status = status, ContentType = contentType, Query = query
        });
    }

    [HttpGet("Search")]
    [SensitiveRateLimit("Expensive")]
    public async Task<IActionResult> Search(string provider, string query, string? tags, string? targetContentType,
        CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(cancellationToken)) return RedirectToAction("Login", "Account");
        var model = new ExternalContentListViewModel { Providers = _imports.Providers, Provider = provider, Query = query,
            TargetContentType = targetContentType };
        try { model.SearchResults = (await _imports.SearchAsync(provider, new(query, tags), cancellationToken)).ToList(); }
        catch (Exception ex) { SetUnexpectedError(ex, "Harici içerik araması tamamlanamadı.", "ExternalContentSearch"); }
        model.Items = await _db.ExternalContentSources.AsNoTracking().OrderByDescending(x => x.ImportedAt).Take(100).ToListAsync(cancellationToken);
        return View("Index", model);
    }

    [HttpPost("Import")]
    [ValidateAntiForgeryToken]
    [SensitiveRateLimit("Expensive")]
    public async Task<IActionResult> Import(string provider, string externalId, string? targetContentType,
        CancellationToken cancellationToken)
    {
        var userId = await AdminUserIdAsync(cancellationToken);
        if (userId is null) return RedirectToAction("Login", "Account");
        try
        {
            var source = await _imports.ImportAsync(provider, externalId, userId.Value, cancellationToken, targetContentType);
            if (_configuration.GetValue("ExternalContent:AutoPublish", true))
            {
                var count = await _publisher.PublishTreeAsync(source.Id, userId.Value, false, cancellationToken);
                TempData["SuccessMessage"] = count > 0
                    ? $"{count} Türkçe içerik otomatik yayınlandı; sonradan düzenleyebilirsiniz."
                    : "Çeviri tamamlanamadığı için içerik yayınlanmadı.";
            }
            else TempData["SuccessMessage"] = "İçerik inceleme kuyruğuna alındı.";
            return RedirectToAction(nameof(Details), new { id = source.Id });
        }
        catch (Exception ex)
        {
            SetUnexpectedError(ex, "İçerik içe aktarılamadı.", "ExternalContentImport");
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(cancellationToken)) return RedirectToAction("Login", "Account");
        var source = await _db.ExternalContentSources.Include(x => x.ParentSource).Include(x => x.Children)
            .Include(x => x.AuditEntries).ThenInclude(x => x.PerformedByUser).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null) return NotFound();
        return View(new ExternalContentEditViewModel
        {
            Id = source.Id, Title = source.TranslatedTitle ?? source.SourceTitle,
            Text = source.TranslatedText ?? source.OriginalText, Tags = source.Tags, Category = source.Category, Source = source
        });
    }

    [HttpPost("{id:int}/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ExternalContentEditViewModel model, CancellationToken cancellationToken)
    {
        var userId = await AdminUserIdAsync(cancellationToken);
        if (userId is null) return RedirectToAction("Login", "Account");
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return await Details(id, cancellationToken);
        var source = await _db.ExternalContentSources.FindAsync([id], cancellationToken);
        if (source is null) return NotFound();
        source.TranslatedTitle = model.Title.Trim(); source.TranslatedText = model.Text.Trim();
        source.Tags = model.Tags?.Trim(); source.Category = model.Category?.Trim(); source.WasModified = true;
        if (source.ReviewStatus == ExternalContentReviewStatuses.Approved) source.ReviewStatus = ExternalContentReviewStatuses.NeedsReview;
        AddAudit(source, "AdminEdited", "Success", "Başlık, metin veya sınıflandırma alanları düzenlendi.", userId.Value);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Taslak kaydedildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/Refresh")]
    [ValidateAntiForgeryToken]
    [SensitiveRateLimit("Expensive")]
    public async Task<IActionResult> Refresh(int id, CancellationToken cancellationToken)
    {
        var userId = await AdminUserIdAsync(cancellationToken);
        if (userId is null) return RedirectToAction("Login", "Account");
        try { await _imports.RefreshAsync(id, userId.Value, cancellationToken); TempData["SuccessMessage"] = "Kaynak yeniden kontrol edildi."; }
        catch (Exception ex) { SetUnexpectedError(ex, "Kaynak yenilenemedi.", "ExternalContentRefresh"); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/Approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, ExternalContentApprovalViewModel model, CancellationToken cancellationToken)
    {
        var userId = await AdminUserIdAsync(cancellationToken);
        if (userId is null) return RedirectToAction("Login", "Account");
        if (id != model.Id || !model.LicenseConfirmed || !model.AttributionConfirmed || !model.SafetyConfirmed || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }
        var source = await _db.ExternalContentSources.Include(x => x.ParentSource).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null) return NotFound();
        try
        {
            await _publisher.PublishAsync(source.Id, userId.Value, true, cancellationToken);
            TempData["SuccessMessage"] = "İçerik kaynak bilgisiyle yayınlandı.";
        }
        catch (InvalidOperationException ex) { SetUnexpectedError(ex, "İçerik yayınlanamadı.", "ExternalContentApprove"); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason, CancellationToken cancellationToken) =>
        await SetStatus(id, ExternalContentReviewStatuses.Rejected, reason, "Rejected", cancellationToken);

    [HttpPost("{id:int}/Archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken) =>
        await SetStatus(id, ExternalContentReviewStatuses.Archived, null, "Archived", cancellationToken);

    private async Task<IActionResult> SetStatus(int id, string status, string? reason, string eventType, CancellationToken token)
    {
        var userId = await AdminUserIdAsync(token); if (userId is null) return RedirectToAction("Login", "Account");
        var source = await _db.ExternalContentSources.FindAsync([id], token); if (source is null) return NotFound();
        source.ReviewStatus = status; source.RejectionReason = reason; source.ReviewedByUserId = userId; source.ReviewedAt = Now();
        AddAudit(source, eventType, "Success", reason ?? status, userId.Value); await _db.SaveChangesAsync(token);
        return RedirectToAction(nameof(Details), new { id });
    }

    private void SetUnexpectedError(Exception exception, string userMessage, string operation)
    {
        var referenceCode = ErrorReferenceCode.Create(HttpContext);
        _logger.LogError(exception,
            "{Operation} başarısız. ReferenceCode: {ReferenceCode}, TraceIdentifier: {TraceIdentifier}",
            operation, referenceCode, HttpContext.TraceIdentifier);
        TempData["ErrorMessage"] = ErrorReferenceCode.UserMessage(
            $"{userMessage} Lütfen tekrar deneyin.", referenceCode);
    }

    private async Task<int> PublishAsync(ExternalContentSource source, int userId, CancellationToken token)
    {
        var title = source.TranslatedTitle ?? source.SourceTitle;
        var text = source.TranslatedText ?? source.OriginalText;
        if (source.ContentType == ExternalContentTypes.Question)
        {
            var item = new Question { Title = title, Content = text, Category = source.Category ?? "Genel", Tags = source.Tags,
                UserId = userId, CreatedDate = Now() };
            _db.Questions.Add(item); await _db.SaveChangesAsync(token); return item.Id;
        }
        if (source.ContentType == ExternalContentTypes.Answer)
        {
            if (source.ParentSource?.LocalContentId is not int questionId)
                throw new InvalidOperationException("Önce bu cevabın bağlı olduğu dış kaynak soruyu yayınlayın.");
            var item = new Answer { Content = text, QuestionId = questionId, UserId = userId, CreatedDate = Now() };
            _db.Answers.Add(item); await _db.SaveChangesAsync(token); return item.Id;
        }
        if (source.ContentType == ExternalContentTypes.Guide)
        {
            var item = new Guide { Title = title, Description = text.Length > 300 ? text[..300] + "…" : text,
                Content = text, Category = source.Category ?? "Genel", AnimalType = "Genel", Level = "Bilgilendirme",
                UserId = userId, PublishDate = Now() };
            _db.Guides.Add(item); await _db.SaveChangesAsync(token); return item.Id;
        }
        throw new InvalidOperationException("Bu veri türü doğrudan yayınlanmaz; bir PetWork içeriğini zenginleştirmek için kullanılır.");
    }

    private async Task<bool> IsAdminAsync(CancellationToken token) => (await AdminUserIdAsync(token)).HasValue;
    private async Task<int?> AdminUserIdAsync(CancellationToken token)
    {
        var id = HttpContext.Session.GetInt32("UserId");
        return id.HasValue && await _db.Users.AnyAsync(x => x.Id == id && x.IsAdmin, token) ? id : null;
    }
    private void AddAudit(ExternalContentSource source, string type, string outcome, string details, int userId) =>
        _db.ContentImportAudits.Add(new() { ExternalContentSource = source, EventType = type, Outcome = outcome,
            Details = details, PerformedByUserId = userId, CreatedAt = Now() });
    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
