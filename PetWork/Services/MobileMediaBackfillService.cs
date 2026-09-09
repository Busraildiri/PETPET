using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class MobileMediaBackfillService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MobileMediaBackfillService> _logger;

    public MobileMediaBackfillService(IServiceProvider services, IWebHostEnvironment environment,
        ILogger<MobileMediaBackfillService> logger)
    {
        _services = services;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadRoot)) return;

            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            var knownKeys = await context.MobileMediaAssets.AsNoTracking()
                .Select(item => item.StorageKey)
                .ToHashSetAsync(stoppingToken);

            foreach (var filePath in Directory.EnumerateFiles(uploadRoot, "*", SearchOption.AllDirectories))
            {
                var file = new FileInfo(filePath);
                if (file.Length is <= 0 or > 8 * 1024 * 1024) continue;
                var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => null
                };
                if (contentType is null) continue;

                var key = $"uploads/{Path.GetRelativePath(uploadRoot, filePath).Replace('\\', '/')}";
                if (!knownKeys.Add(key)) continue;
                context.MobileMediaAssets.Add(new MobileMediaAsset
                {
                    StorageKey = key,
                    ContentType = contentType,
                    Data = await File.ReadAllBytesAsync(filePath, stoppingToken),
                    CreatedAt = file.CreationTime
                });
            }

            var count = await context.SaveChangesAsync(stoppingToken);
            if (count > 0) _logger.LogInformation("{Count} yerel mobil görsel ortak veritabanına aktarıldı.", count);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Yerel mobil görseller veritabanına aktarılamadı. Medya migration'ı uygulanmış olmalıdır.");
        }
    }
}
