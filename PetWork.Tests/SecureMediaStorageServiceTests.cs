using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Services;

namespace PetWork.Tests;

public sealed class SecureMediaStorageServiceTests : IAsyncLifetime
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private PetWorkDbContext _context = null!;
    private SecureMediaStorageService _service = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PetWorkDbContext>().UseSqlite(_connection).Options;
        _context = new PetWorkDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _service = new SecureMediaStorageService(_context);
    }

    [Fact]
    public async Task StoreAsync_IgnoresOriginalName_AndUsesDetectedType()
    {
        var stored = await _service.StoreAsync(File(Png, "../../evil.php", "image/png"), "profiles", 1024);

        Assert.StartsWith("uploads/profiles/", stored.StorageKey);
        Assert.EndsWith(".png", stored.StorageKey);
        Assert.DoesNotContain("evil", stored.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^uploads/profiles/[a-f0-9]{32}\\.png$", stored.StorageKey);
    }

    [Fact]
    public async Task StoreAsync_PersistsBytesInDatabase()
    {
        var stored = await _service.StoreAsync(File(Png, "photo.png", "image/png"), "social", 1024);
        await _context.SaveChangesAsync();

        var asset = await _context.MobileMediaAssets.SingleAsync(item => item.StorageKey == stored.StorageKey);
        Assert.Equal("image/png", asset.ContentType);
        Assert.Equal(Png, asset.Data);
    }

    [Fact]
    public async Task StoreBytes_IsNotPersistedUntilSaveChanges()
    {
        _service.StoreBytes("social", Png, 1024, "image/png");

        Assert.Empty(await _context.MobileMediaAssets.ToListAsync());
    }

    [Fact]
    public async Task Discard_RemovesPendingUpload()
    {
        var stored = _service.StoreBytes("social", Png, 1024, "image/png");

        _service.Discard(stored.StorageKey);
        await _context.SaveChangesAsync();

        Assert.Empty(await _context.MobileMediaAssets.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesPersistedAsset()
    {
        var stored = _service.StoreBytes("social", Png, 1024, "image/png");
        await _context.SaveChangesAsync();

        await _service.DeleteAsync(stored.StorageKey);
        await _context.SaveChangesAsync();

        Assert.Empty(await _context.MobileMediaAssets.ToListAsync());
    }

    [Fact]
    public async Task StoreAsync_RejectsSpoofedImageContent()
    {
        var formFile = File("<script>alert(1)</script>"u8.ToArray(), "photo.jpg", "image/jpeg");

        await Assert.ThrowsAsync<MediaValidationException>(() =>
            _service.StoreAsync(formFile, "profiles", 1024));
    }

    [Fact]
    public async Task StoreAsync_RejectsDeclaredTypeThatDoesNotMatchBytes()
    {
        var formFile = File(Png, "photo.jpg", "image/jpeg");

        await Assert.ThrowsAsync<MediaValidationException>(() =>
            _service.StoreAsync(formFile, "profiles", 1024));
    }

    [Fact]
    public async Task StoreAsync_RejectsServerSideSizeOverflow()
    {
        var formFile = File(Png, "photo.png", "image/png");

        await Assert.ThrowsAsync<MediaValidationException>(() =>
            _service.StoreAsync(formFile, "profiles", 8));
    }

    [Fact]
    public void StoreBytes_RejectsFolderOutsideAllowedCharacters()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.StoreBytes("../etc", Png, 1024, "image/png"));
    }

    private static FormFile File(byte[] bytes, string name, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
