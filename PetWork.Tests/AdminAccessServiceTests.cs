using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;
using PetWork.Security;

namespace PetWork.Tests;

public sealed class AdminAccessServiceTests
{
    [Fact]
    public async Task CheckUserAsync_UsesCurrentDatabaseRole_NotAStaleSessionFlag()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PetWorkDbContext>().UseSqlite(connection).Options;
        await using var db = new PetWorkDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var user = new User
        {
            Username = "admin-test",
            Email = "admin-test@example.com",
            PasswordHash = "not-used",
            IsAdmin = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new AdminAccessService(db);

        var allowed = await service.CheckUserAsync(user.Id);
        user.IsAdmin = false;
        await db.SaveChangesAsync();
        var revoked = await service.CheckUserAsync(user.Id);

        Assert.Equal(new AdminAccessResult(true, true), allowed);
        Assert.Equal(new AdminAccessResult(true, false), revoked);
    }

    [Fact]
    public async Task CheckUserAsync_RejectsMissingSession()
    {
        var options = new DbContextOptionsBuilder<PetWorkDbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        await using var db = new PetWorkDbContext(options);

        var result = await new AdminAccessService(db).CheckUserAsync(null);

        Assert.Equal(new AdminAccessResult(false, false), result);
    }
}
