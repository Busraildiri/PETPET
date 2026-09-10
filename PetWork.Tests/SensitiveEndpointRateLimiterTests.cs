using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PetWork.Data;
using PetWork.Security;

namespace PetWork.Tests;

public sealed class SensitiveEndpointRateLimiterTests
{
    [Fact]
    public async Task Enforces_shared_ip_and_account_buckets_and_returns_retry_delay()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var settings = Options.Create(new RateLimitSettings
        {
            Policies = new Dictionary<string, RateLimitRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["Login"] = new() { PermitLimit = 2, WindowSeconds = 900 }
            }
        });

        var services = new ServiceCollection()
            .AddSingleton(settings)
            .AddLogging()
            .AddDbContext<PetWorkDbContext>(options => options.UseSqlite(connection))
            .AddSingleton<SensitiveEndpointRateLimiter>()
            .BuildServiceProvider();
        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PetWorkDbContext>().Database.EnsureCreatedAsync();

        var limiter = services.GetRequiredService<SensitiveEndpointRateLimiter>();
        Assert.True((await limiter.AttemptAsync("Login", "10.0.0.1", "alice@example.com")).Allowed);
        Assert.True((await limiter.AttemptAsync("Login", "10.0.0.2", "alice@example.com")).Allowed);

        var accountLimited = await limiter.AttemptAsync("Login", "10.0.0.3", "alice@example.com");
        Assert.False(accountLimited.Allowed);
        Assert.InRange(accountLimited.RetryAfter.TotalSeconds, 1, 900);

        Assert.True((await limiter.AttemptAsync("Login", "10.0.0.4", "bob@example.com")).Allowed);
        Assert.True((await limiter.AttemptAsync("Login", "10.0.0.4", "carol@example.com")).Allowed);
        Assert.False((await limiter.AttemptAsync("Login", "10.0.0.4", "dave@example.com")).Allowed);

        using var verificationScope = services.CreateScope();
        var rows = await verificationScope.ServiceProvider.GetRequiredService<PetWorkDbContext>()
            .RateLimitUsages.ToListAsync();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(64, row.SubjectHash.Length));
        Assert.DoesNotContain(rows, row => row.SubjectHash.Contains("alice", StringComparison.OrdinalIgnoreCase));
    }
}
