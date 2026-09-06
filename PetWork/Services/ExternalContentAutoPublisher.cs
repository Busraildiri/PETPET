using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class ExternalContentAutoPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalContentAutoPublisher> _logger;
    public ExternalContentAutoPublisher(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<ExternalContentAutoPublisher> logger)
    { _scopeFactory = scopeFactory; _configuration = configuration; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("ExternalContent:AutoPublish", true)) return;
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IExternalContentPublishingService>();
            var importer = scope.ServiceProvider.GetRequiredService<IExternalContentImportService>();
            var adminId = await db.Users.Where(x => x.IsAdmin).OrderBy(x => x.Id).Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(stoppingToken);
            if (adminId is null) return;
            var roots = await db.ExternalContentSources.Where(x => x.ParentSourceId == null && x.LocalContentId == null &&
                (x.ReviewStatus == ExternalContentReviewStatuses.Pending || x.ReviewStatus == ExternalContentReviewStatuses.NeedsReview))
                .Select(x => x.Id).ToListAsync(stoppingToken);
            foreach (var id in roots)
            {
                await importer.RefreshAsync(id, adminId.Value, stoppingToken);
                await publisher.PublishTreeAsync(id, adminId.Value, false, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Bekleyen dış içerikler otomatik yayınlanamadı."); }
    }
}
