using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace PetWork.Data;

public sealed class PostgresPetWorkDbContextFactory
    : IDesignTimeDbContextFactory<PostgresPetWorkDbContext>
{
    public PostgresPetWorkDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<PostgresPetWorkDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = PostgreSqlConnectionString.Normalize(
            configuration.GetConnectionString("PostgreSqlAdmin")
                ?? "Host=localhost;Port=5432;Database=petwork_design;Username=postgres;Password=design-time-only");

        var options = new DbContextOptionsBuilder<PostgresPetWorkDbContext>()
            .UseNpgsql(
                connectionString,
                postgresOptions => postgresOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    PostgresPetWorkDbContext.SchemaName))
            .Options;

        return new PostgresPetWorkDbContext(
            options,
            NullLogger<PetWorkDbContext>.Instance);
    }
}
