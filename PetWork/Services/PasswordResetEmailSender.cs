using System.Security.Cryptography;
using System.Text.Json;

namespace PetWork.Services;

public interface IPasswordResetEmailSender
{
    Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken);
}

public sealed class DevelopmentPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IWebHostEnvironment _environment;

    public DevelopmentPasswordResetEmailSender(IWebHostEnvironment environment) => _environment = environment;

    public async Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var inbox = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".local", "password-reset-inbox"));
        Directory.CreateDirectory(inbox);
        var fileName = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant() + ".json";
        var payload = JsonSerializer.Serialize(new
        {
            email,
            username,
            resetUrl = $"petim://reset-password?token={Uri.EscapeDataString(token)}",
            expiresAt
        });
        await File.WriteAllTextAsync(Path.Combine(inbox, fileName), payload, cancellationToken);
    }
}

public sealed class UnavailablePasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly ILogger<UnavailablePasswordResetEmailSender> _logger;

    public UnavailablePasswordResetEmailSender(ILogger<UnavailablePasswordResetEmailSender> logger) => _logger = logger;

    public Task SendAsync(string email, string username, string token, DateTime expiresAt, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Password reset requested but no production email provider is configured.");
        return Task.CompletedTask;
    }
}
