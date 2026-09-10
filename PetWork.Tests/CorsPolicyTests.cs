using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PetWork.Tests;

public sealed class CorsPolicyTests : IClassFixture<CorsPolicyFactory>
{
    private readonly HttpClient _client;

    public CorsPolicyTests(CorsPolicyFactory factory) => _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://petwork.test")
        });

    [Fact]
    public async Task DevelopmentOrigin_GetsCredentialedRestrictedPreflightResponse()
    {
        using var request = Preflight("http://localhost:8081", "PUT", "authorization,content-type");

        using var response = await _client.SendAsync(request);

        Assert.Equal("http://localhost:8081", Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
        var methods = Header(response, "Access-Control-Allow-Methods");
        Assert.Contains("GET", methods);
        Assert.Contains("POST", methods);
        Assert.Contains("PUT", methods);
        Assert.Contains("DELETE", methods);
        Assert.DoesNotContain("PATCH", methods);
        var headers = Header(response, "Access-Control-Allow-Headers");
        Assert.Contains("authorization", headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content-type", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownOrigin_DoesNotReceiveCorsPermission()
    {
        using var request = Preflight("https://attacker.example", "POST", "authorization");

        using var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Patch_IsNotAllowed()
    {
        using var request = Preflight("http://localhost:8081", "PATCH", "authorization");

        using var response = await _client.SendAsync(request);

        Assert.False(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods) &&
                     methods.Any(value => value.Contains("PATCH", StringComparison.OrdinalIgnoreCase)));
    }

    private static HttpRequestMessage Preflight(string origin, string method, string headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/mobile/home");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    private static string Header(HttpResponseMessage response, string name) =>
        string.Join(",", response.Headers.GetValues(name));
}

public sealed class CorsPolicyFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("MobileAuth:JwtKey", "test-only-jwt-key-with-at-least-thirty-two-bytes");
        builder.UseSetting("DatabaseMigrations:ApplyOnStartup", "false");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
    }
}
