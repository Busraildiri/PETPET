using Microsoft.EntityFrameworkCore;

namespace PetWork.Data;

/// <summary>
/// PostgreSQL'e özel migration geçmişini SQL Server migration'larından ayırır.
/// Uygulama controller'ları ortak PetWorkDbContext modelini kullanmaya devam eder.
/// </summary>
public sealed class PostgresPetWorkDbContext : PetWorkDbContext
{
    public const string SchemaName = "petwork";

    public PostgresPetWorkDbContext(
        DbContextOptions<PostgresPetWorkDbContext> options,
        ILogger<PetWorkDbContext>? logger = null)
        : base(options, logger)
    {
    }
}
