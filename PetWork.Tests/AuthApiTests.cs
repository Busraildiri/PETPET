using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetWork.Data;
using PetWork.Models;
using PetWork.Services;

namespace PetWork.Tests;

public sealed class AuthApiTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private readonly HttpClient _client;
    public AuthApiTests(AuthApiFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task Registration_requires_the_emailed_code()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var email = $"verify-{id}@example.com";
        var response = await _client.PostAsJsonAsync("api/mobile/auth/register", new { username = $"verify_{id}", email, password = "Valid1!x", confirmPassword = "Valid1!x", acceptTerms = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var challenge = (await response.Content.ReadFromJsonAsync<EmailChallengeDto>())!;
        var code = _factory.VerificationSender.GetCode(challenge.ChallengeToken);
        var wrongCode = code == "000000" ? "000001" : "000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("api/mobile/auth/verify-email", new { challengeToken = challenge.ChallengeToken, code = wrongCode })).StatusCode);
        var verified = await _client.PostAsJsonAsync("api/mobile/auth/verify-email", new { challengeToken = challenge.ChallengeToken, code });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
    }

    [Fact]
    public async Task Registration_email_failure_does_not_leave_an_account()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var email = $"failure-{id}@example.com";
        _factory.VerificationSender.FailNext = true;
        var response = await _client.PostAsJsonAsync("api/mobile/auth/register", new { username = $"failure_{id}", email, password = "Valid1!x", confirmPassword = "Valid1!x", acceptTerms = true });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PetWorkDbContext>().Users.AnyAsync(user => user.Email == email));
    }

    [Fact]
    public async Task Register_login_and_validation_are_enforced()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var email = $"qa-{id}@example.com";
        var registration = await Register($"qa_{id}", $"  {email.ToUpperInvariant()}  ", "Valid1!x");
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var auth = await registration.Content.ReadFromJsonAsync<AuthDto>();
        Assert.NotNull(auth?.Token); Assert.NotNull(auth?.RefreshToken);

        var duplicate = await Register($"other_{id}", email, "Valid1!x");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var invalid = await Register($"bad_{id}", "not-an-email", "weak");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var badLogin = await Login(email, "Wrong1!x");
        var missingLogin = await Login($"missing-{id}@example.com", "Wrong1!x");
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        Assert.Equal(await badLogin.Content.ReadAsStringAsync(), await missingLogin.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
        var user = await db.Users.SingleAsync(item => item.Email == email);
        Assert.NotEqual("Valid1!x", user.PasswordHash);
        Assert.StartsWith("AQAAAA", user.PasswordHash);
    }

    [Fact]
    public async Task Username_can_be_changed_when_available_and_conflicts_are_rejected()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var password = "Valid1!x";
        var owner = await ReadAuth(await Register($"owner_{id}", $"owner-{id}@example.com", password));
        var other = await ReadAuth(await Register($"taken_{id}", $"taken-{id}@example.com", password));
        var nextUsername = $"new_{id}";

        var changed = await Authorized(HttpMethod.Patch, "api/mobile/auth/username", owner.Token, new { username = nextUsername });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var current = await changed.Content.ReadFromJsonAsync<MeDto>();
        Assert.Equal(nextUsername, current!.Username);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            Assert.Equal(nextUsername, (await db.Users.SingleAsync(user => user.Id == owner.UserId)).Username);
        }

        Assert.Equal(HttpStatusCode.Conflict,
            (await Authorized(HttpMethod.Patch, "api/mobile/auth/username", owner.Token, new { username = other.Username.ToUpperInvariant() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Authorized(HttpMethod.Patch, "api/mobile/auth/username", owner.Token, new { username = "uygunsuz ad" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "api/mobile/auth/username") { Content = JsonContent.Create(new { username = $"noauth_{id}" }) })).StatusCode);
    }

    [Fact]
    public async Task Full_auth_lifecycle_revokes_tokens_and_deletes_account()
    {
        var id = Guid.NewGuid().ToString("N")[..10];
        var email = $"life-{id}@example.com";
        var oldPassword = "OldPass1!";
        var changedPassword = "Changed2!";
        var resetPassword = "ResetPass3!";
        var auth = await ReadAuth(await Register($"life_{id}", email, oldPassword));
        var protectedUser = await ReadAuth(await Register($"safe_{id}", $"safe-{id}@example.com", "SafePass1!"));
        Assert.Equal(HttpStatusCode.Created, (await Authorized(HttpMethod.Post, "api/mobile/pets", auth.Token, new { name = "Tarçın", type = "Kedi", age = 2 })).StatusCode);
        await SeedOwnedData(auth.UserId, protectedUser.UserId);

        Assert.Equal(HttpStatusCode.OK, (await Authorized(HttpMethod.Get, "api/mobile/auth/me", auth.Token)).StatusCode);
        var refreshed = await ReadAuth(await _client.PostAsJsonAsync("api/mobile/auth/refresh", new { auth.RefreshToken }));
        Assert.NotEqual(auth.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("api/mobile/auth/refresh", new { auth.RefreshToken })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Post, "api/mobile/auth/change-password", refreshed.Token, new { currentPassword = "Wrong1!x", newPassword = changedPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Post, "api/mobile/auth/change-password", refreshed.Token, new { currentPassword = oldPassword, newPassword = oldPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Post, "api/mobile/auth/change-password", refreshed.Token, new { currentPassword = oldPassword, newPassword = "short" })).StatusCode);
        var changed = await ReadAuth(await Authorized(HttpMethod.Post, "api/mobile/auth/change-password", refreshed.Token, new { currentPassword = oldPassword, newPassword = changedPassword }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, oldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, changedPassword)).StatusCode);

        var existingForgot = await _client.PostAsJsonAsync("api/mobile/auth/forgot-password", new { email });
        var missingForgot = await _client.PostAsJsonAsync("api/mobile/auth/forgot-password", new { email = $"none-{id}@example.com" });
        Assert.Equal(await existingForgot.Content.ReadAsStringAsync(), await missingForgot.Content.ReadAsStringAsync());
        var resetToken = _factory.EmailSender.GetToken(email);
        var reset = await _client.PostAsJsonAsync("api/mobile/auth/reset-password", new { token = resetToken, newPassword = resetPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("api/mobile/auth/reset-password", new { token = resetToken, newPassword = "AgainPass4!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Authorized(HttpMethod.Get, "api/mobile/auth/me", changed.Token)).StatusCode);

        var afterReset = await ReadAuth(await Login(email, resetPassword));
        Assert.Equal(HttpStatusCode.NoContent, (await Authorized(HttpMethod.Post, "api/mobile/auth/logout", afterReset.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Authorized(HttpMethod.Get, "api/mobile/auth/me", afterReset.Token)).StatusCode);

        var deleteSession = await ReadAuth(await Login(email, resetPassword));
        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Delete, "api/mobile/auth/account", deleteSession.Token, new { password = "Wrong1!x", userId = protectedUser.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Delete, "api/mobile/auth/account", deleteSession.Token, new { password = resetPassword, confirmation = "yanlış", userId = protectedUser.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Delete, "api/mobile/auth/account", deleteSession.Token, new { password = resetPassword, confirmation = "SİL", userId = protectedUser.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Authorized(HttpMethod.Delete, "api/mobile/auth/account", deleteSession.Token, new { password = resetPassword, confirmation = "SİL" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, resetPassword)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
        Assert.False(await db.Users.AnyAsync(item => item.Email == email));
        Assert.False(await db.MobileAuthSessions.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.PasswordResetTokens.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.Pets.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.Questions.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.Answers.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.Recipes.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.BlogPosts.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.Guides.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.SocialPosts.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.SocialComments.AnyAsync(item => item.UserId == auth.UserId));
        Assert.False(await db.SocialPostReports.AnyAsync(item => item.UserId == auth.UserId));
        Assert.True(await db.Users.AnyAsync(item => item.Id == protectedUser.UserId));
    }

    [Fact]
    public async Task Pet_endpoints_prevent_cross_user_read_update_and_delete()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var userA = await ReadAuth(await Register($"ownerA_{id}", $"a-{id}@example.com", "OwnerA1!"));
        var userB = await ReadAuth(await Register($"ownerB_{id}", $"b-{id}@example.com", "OwnerB1!"));
        var created = await Authorized(HttpMethod.Post, "api/mobile/pets", userB.Token, new { name = "Misket", type = "Kedi", age = 2 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var pet = await created.Content.ReadFromJsonAsync<PetDto>();

        var listA = await Authorized(HttpMethod.Get, "api/mobile/pets", userA.Token);
        Assert.DoesNotContain((await listA.Content.ReadFromJsonAsync<List<PetDto>>())!, item => item.Id == pet!.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await Authorized(HttpMethod.Put, $"api/mobile/pets/{pet!.Id}", userA.Token, new { name = "Çalındı", type = "Kedi", age = 3, userId = userB.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Authorized(HttpMethod.Delete, $"api/mobile/pets/{pet.Id}", userA.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Authorized(HttpMethod.Put, $"api/mobile/pets/{pet.Id}", userB.Token, new { name = "Misket 2", type = "Kedi", age = 3 })).StatusCode);
    }

    private async Task<HttpResponseMessage> Register(string username, string email, string password)
    {
        var response = await _client.PostAsJsonAsync("api/mobile/auth/register", new { username, email, password, confirmPassword = password, acceptTerms = true, rememberMe = true });
        if (response.StatusCode != HttpStatusCode.Created) return response;
        var challenge = await response.Content.ReadFromJsonAsync<EmailChallengeDto>();
        Assert.NotNull(challenge);
        var code = _factory.VerificationSender.GetCode(challenge!.ChallengeToken);
        return await _client.PostAsJsonAsync("api/mobile/auth/verify-email", new { challengeToken = challenge.ChallengeToken, code });
    }
    private Task<HttpResponseMessage> Login(string email, string password) => _client.PostAsJsonAsync("api/mobile/auth/login", new { emailOrUsername = email, password, rememberMe = true });
    private static async Task<AuthDto> ReadAuth(HttpResponseMessage response) { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<AuthDto>())!; }
    private async Task<HttpResponseMessage> Authorized(HttpMethod method, string path, string token, object? body = null) { using var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); if (body is not null) request.Content = JsonContent.Create(body); return await _client.SendAsync(request); }
    private async Task SeedOwnedData(int userId, int protectedUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
        var question = new Question { UserId = userId, Title = "Silinecek soru", Content = "İçerik" };
        var ownPost = new SocialPost { UserId = userId, Body = "Silinecek gönderi" };
        var protectedPost = new SocialPost { UserId = protectedUserId, Body = "Kalacak gönderi" };
        db.AddRange(question, ownPost, protectedPost,
            new Recipe { UserId = userId, Title = "Tarif", Description = "Açıklama", Ingredients = "Malzeme", Instructions = "Adım", Content = "İçerik" },
            new BlogPost { UserId = userId, Title = "Blog", Content = "İçerik", Category = "Bakım" },
            new Guide { UserId = userId, Title = "Rehber", Description = "Açıklama", Content = "İçerik" });
        await db.SaveChangesAsync();
        db.AddRange(new Answer { UserId = userId, QuestionId = question.Id, Content = "Yanıt" },
            new SocialComment { UserId = userId, SocialPostId = protectedPost.Id, Body = "Yorum" },
            new SocialPostReport { UserId = userId, SocialPostId = protectedPost.Id, Reason = "Rapor" });
        await db.SaveChangesAsync();
    }
    private sealed record AuthDto(int UserId, string Username, string Token, DateTime ExpiresAt, string RefreshToken, DateTime RefreshExpiresAt, string Message);
    private sealed record MeDto(int UserId, string Username, string Email);
    private sealed record EmailChallengeDto(bool RequiresEmailVerification, string ChallengeToken, DateTime ExpiresAt, string MaskedEmail, string Message);
    private sealed record PetDto(int Id, string Name);
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public CapturingPasswordResetEmailSender EmailSender { get; } = new();
    public CapturingEmailVerificationSender VerificationSender { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("DatabaseProvider", "SqlServer");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\mssqllocaldb;Database=unused");
        builder.UseSetting("MobileAuth:JwtKey", "test-only-jwt-key-with-at-least-thirty-two-bytes");
        builder.UseSetting("RateLimiting:MobileAuthPermitLimit", "1000");
        builder.UseSetting("RateLimiting:MobileContentPermitLimit", "1000");
        builder.UseSetting("ExternalContent:BootstrapOnStartup", "false");
        builder.UseSetting("ExternalContent:AutoPublish", "false");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["DatabaseProvider"] = "SqlServer", ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=unused", ["MobileAuth:JwtKey"] = "test-only-jwt-key-with-at-least-thirty-two-bytes", ["GooglePlaces:ApiKey"] = "", ["RateLimiting:MobileAuthPermitLimit"] = "1000", ["RateLimiting:MobileContentPermitLimit"] = "1000", ["ExternalContent:BootstrapOnStartup"] = "false", ["ExternalContent:AutoPublish"] = "false", ["DatabaseMigrations:ApplyOnStartup"] = "false",
            ["RateLimits:Policies:Login:PermitLimit"] = "1000", ["RateLimits:Policies:Registration:PermitLimit"] = "1000", ["RateLimits:Policies:CodeSend:PermitLimit"] = "1000", ["RateLimits:Policies:CodeVerify:PermitLimit"] = "1000", ["RateLimits:Policies:PasswordReset:PermitLimit"] = "1000", ["RateLimits:Policies:Expensive:PermitLimit"] = "1000"
        }));
        builder.ConfigureServices(services => {
            services.RemoveAll<IDbContextOptionsConfiguration<PetWorkDbContext>>();
            services.RemoveAll<DbContextOptions<PetWorkDbContext>>();
            services.RemoveAll<PetWorkDbContext>();
            _connection.Open(); services.AddSingleton(_connection); services.AddDbContext<PetWorkDbContext>(options => options.UseSqlite(_connection));
            services.RemoveAll<IPasswordResetEmailSender>(); services.AddSingleton<IPasswordResetEmailSender>(EmailSender);
            services.RemoveAll<IEmailVerificationSender>(); services.AddSingleton<IEmailVerificationSender>(VerificationSender);
        });
    }
    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder); using var scope = host.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<PetWorkDbContext>().Database.EnsureCreated(); return host;
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _connection.Dispose(); }
}

public sealed class CapturingEmailVerificationSender : IEmailVerificationSender
{
    private readonly Dictionary<string, string> _codes = new();
    public bool FailNext { get; set; }
    public Task SendAsync(string email, string username, string code, string challengeToken, CancellationToken cancellationToken)
    {
        if (FailNext) { FailNext = false; throw new EmailDeliveryException("Test delivery failure."); }
        lock (_codes) _codes[challengeToken] = code;
        return Task.CompletedTask;
    }
    public string GetCode(string challengeToken) { lock (_codes) return _codes[challengeToken]; }
}

public sealed class CapturingPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    public Task SendAsync(string email, string username, string rawToken, DateTime expiresAt, CancellationToken cancellationToken) { lock (_tokens) _tokens[email] = rawToken; return Task.CompletedTask; }
    public string GetToken(string email) { lock (_tokens) return _tokens[email]; }
}
