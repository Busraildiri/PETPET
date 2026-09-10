using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PetWork.Data;
using PetWork.Security;

namespace PetWork.Tests;

public sealed class DailyCostQuotaServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    [Fact]
    public async Task Persists_a_shared_daily_limit_without_storing_the_raw_subject()
    {
        await _connection.OpenAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CostControls:DailyLimits:TransactionalEmail"] = "2"
        }).Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddDbContext<PetWorkDbContext>(options => options.UseSqlite(_connection))
            .AddSingleton<DailyCostQuotaService>()
            .BuildServiceProvider();

        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PetWorkDbContext>().Database.EnsureCreatedAsync();

        var quota = services.GetRequiredService<DailyCostQuotaService>();
        Assert.True((await quota.TryConsumeAsync("TransactionalEmail", "email:alice@example.com")).Allowed);
        Assert.True((await quota.TryConsumeAsync("TransactionalEmail", "email:alice@example.com")).Allowed);
        var rejected = await quota.TryConsumeAsync("TransactionalEmail", "email:alice@example.com");
        Assert.False(rejected.Allowed);
        Assert.InRange(rejected.RetryAfter.TotalSeconds, 1, TimeSpan.FromDays(1).TotalSeconds);

        using var verificationScope = services.CreateScope();
        var row = await verificationScope.ServiceProvider.GetRequiredService<PetWorkDbContext>()
            .DailyCostUsages.SingleAsync();
        Assert.Equal(2, row.Count);
        Assert.DoesNotContain("alice", row.SubjectHash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, row.SubjectHash.Length);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
