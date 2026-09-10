namespace PetWork.Models;

/// <summary>
/// Shared fixed-window counters for sensitive endpoints. Subjects are hashed so
/// account names and IP addresses are not persisted as plaintext.
/// </summary>
public sealed class RateLimitUsage
{
    public string Policy { get; set; } = string.Empty;
    public string SubjectHash { get; set; } = string.Empty;
    public DateTime WindowStartedAtUtc { get; set; }
    public int Count { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
