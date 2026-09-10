using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed record StoredMedia(string StorageKey, string ContentType, long Length);

public sealed class MediaValidationException : Exception
{
    public MediaValidationException(string message) : base(message) { }
}

public sealed class SecureMediaStorageService
{
    private const int HeaderLength = 12;
    private readonly PetWorkDbContext _context;

    public SecureMediaStorageService(PetWorkDbContext context) => _context = context;

    public async Task<StoredMedia> StoreAsync(IFormFile file, string folder, long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0) throw new MediaValidationException("Boş dosya yüklenemez.");
        if (file.Length > maximumBytes) throw new MediaValidationException($"Dosya en fazla {maximumBytes / 1024 / 1024} MB olabilir.");

        await using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > maximumBytes) throw new MediaValidationException($"Dosya en fazla {maximumBytes / 1024 / 1024} MB olabilir.");
        return StoreBytes(folder, buffer.ToArray(), maximumBytes, file.ContentType);
    }

    // Kayıt çağıranın DbContext'ine iliştirilir; kalıcı olması için SaveChanges gerekir.
    // Böylece görsel, ait olduğu içerikle aynı transaction'da yazılır.
    public StoredMedia StoreBytes(string folder, byte[] data, long maximumBytes, string? declaredContentType)
    {
        if (data.Length <= 0) throw new MediaValidationException("Boş dosya yüklenemez.");
        if (data.LongLength > maximumBytes) throw new MediaValidationException($"Dosya en fazla {maximumBytes / 1024 / 1024} MB olabilir.");
        var mediaType = Detect(data) ?? throw new MediaValidationException("Yalnızca gerçek JPEG, PNG veya WebP görselleri kabul edilir.");
        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            !string.Equals(declaredContentType, mediaType.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new MediaValidationException("Bildirilen dosya türü gerçek içerikle eşleşmiyor.");

        var key = $"uploads/{NormalizeFolder(folder)}/{Guid.NewGuid():N}{mediaType.Extension}";
        _context.MobileMediaAssets.Add(new MobileMediaAsset
        {
            StorageKey = key,
            ContentType = mediaType.ContentType,
            Data = data,
            CreatedAt = DateTime.Now
        });
        return new StoredMedia(key, mediaType.ContentType, data.LongLength);
    }

    // Henüz kaydedilmemiş bir yüklemeyi geri alır.
    public void Discard(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        var pending = _context.MobileMediaAssets.Local.FirstOrDefault(asset => asset.StorageKey == storageKey);
        if (pending is not null) _context.MobileMediaAssets.Remove(pending);
    }

    // Kalıcı bir görseli siler; SaveChanges ile kesinleşir.
    public async Task DeleteAsync(string? storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        Discard(storageKey);
        var asset = await _context.MobileMediaAssets
            .FirstOrDefaultAsync(item => item.StorageKey == storageKey, cancellationToken);
        if (asset is not null) _context.MobileMediaAssets.Remove(asset);
    }

    private static string NormalizeFolder(string folder)
    {
        var normalized = folder.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 50 || normalized.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            throw new InvalidOperationException("Geçersiz medya klasörü.");
        return normalized;
    }

    private static (string ContentType, string Extension)? Detect(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ("image/jpeg", ".jpg");
        if (data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ("image/png", ".png");
        if (data.Length >= HeaderLength && data.AsSpan(0, 4).SequenceEqual("RIFF"u8) && data.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp");
        return null;
    }
}
