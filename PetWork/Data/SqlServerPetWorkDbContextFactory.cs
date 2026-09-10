using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace PetWork.Data;

/// <summary>
/// Keeps SQL Server design-time migrations separate from the derived PostgreSQL context.
/// Runtime connection strings are not embedded here.
/// </summary>
public sealed class SqlServerPetWorkDbContextFactory : IDesignTimeDbContextFactory<PetWorkDbContext>
{
    public PetWorkDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<SqlServerPetWorkDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required for SQL Server migrations.");
        var options = new DbContextOptionsBuilder<PetWorkDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new PetWorkDbContext(options, NullLogger<PetWorkDbContext>.Instance);
    }
}
