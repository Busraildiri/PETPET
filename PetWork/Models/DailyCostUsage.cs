namespace PetWork.Models;

/// <summary>
/// Shared, database-backed daily counters for calls that can create third-party cost.
/// SubjectHash deliberately avoids storing e-mail addresses or IP addresses as plain text.
/// </summary>
public sealed class DailyCostUsage
{
    public string Category { get; set; } = string.Empty;
    public string SubjectHash { get; set; } = string.Empty;
    public DateTime UsageDateUtc { get; set; }
    public int Count { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
