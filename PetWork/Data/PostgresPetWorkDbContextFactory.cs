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

        var connectionStringValue = configuration.GetConnectionString("PostgreSqlAdmin");
        if (string.IsNullOrWhiteSpace(connectionStringValue))
        {
            throw new InvalidOperationException(
                "PostgreSqlAdmin bağlantı dizesi eksik. Bunu User Secrets veya ConnectionStrings__PostgreSqlAdmin ortam değişkeniyle sağlayın.");
        }

        var connectionString = PostgreSqlConnectionString.Normalize(connectionStringValue);

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
