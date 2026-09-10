namespace PetWork.Services;

public sealed class MobileMediaStorageService
{
    private const long MaximumBytes = 8 * 1024 * 1024;
    private readonly SecureMediaStorageService _storage;

    public MobileMediaStorageService(SecureMediaStorageService storage) => _storage = storage;

    public string StageUpload(string folder, string contentType, byte[] data)
    {
        return _storage.StoreBytes(folder, data, MaximumBytes, contentType).StorageKey;
    }

    public void DiscardPending(string? storageKey)
    {
        _storage.Discard(storageKey);
    }

    public async Task StageDeleteAsync(string? storageKey, CancellationToken cancellationToken)
    {
        await _storage.DeleteAsync(storageKey, cancellationToken);
    }
}
