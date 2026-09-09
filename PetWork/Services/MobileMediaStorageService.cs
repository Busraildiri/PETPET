using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class MobileMediaStorageService
{
    private readonly PetWorkDbContext _context;

    public MobileMediaStorageService(PetWorkDbContext context) => _context = context;

    public string StageUpload(string folder, string extension, string contentType, byte[] data)
    {
        var key = $"uploads/{folder}/{Guid.NewGuid():N}{extension}";
        _context.MobileMediaAssets.Add(new MobileMediaAsset
        {
            StorageKey = key,
            ContentType = contentType,
            Data = data,
            CreatedAt = DateTime.Now
        });
        return key;
    }

    public void DiscardPending(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        var pending = _context.MobileMediaAssets.Local.FirstOrDefault(item => item.StorageKey == storageKey);
        if (pending is not null) _context.MobileMediaAssets.Remove(pending);
    }

    public async Task StageDeleteAsync(string? storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        var asset = await _context.MobileMediaAssets.FirstOrDefaultAsync(
            item => item.StorageKey == storageKey, cancellationToken);
        if (asset is not null) _context.MobileMediaAssets.Remove(asset);
    }
}
