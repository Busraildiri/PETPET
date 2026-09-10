using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PetWork.Tests;

public sealed class SecurityHeaderTests : IClassFixture<SecurityHeaderFactory>
{
    private readonly HttpClient _client;
    private readonly SecurityHeaderFactory _factory;

    public SecurityHeaderTests(SecurityHeaderFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://petwork.test") });
    }

    [Fact]
    public async Task StaticResponses_ReceiveSecurityHeaders_InReportOnlyMode()
    {
        using var response = await _client.GetAsync("/css/style.css");

        response.EnsureSuccessStatusCode();
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("SAMEORIGIN", Header(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Contains("camera=()", Header(response, "Permissions-Policy"));
        Assert.Contains("script-src 'self' https://cdn.jsdelivr.net", Header(response, "Content-Security-Policy-Report-Only"));
        Assert.Contains("frame-ancestors 'self'", Header(response, "Content-Security-Policy-Report-Only"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task ProductionHttpsResponses_ReceiveLongLivedHsts()
    {
        using var response = await _client.GetAsync("/css/style.css");

        Assert.Contains("max-age=31536000", Header(response, "Strict-Transport-Security"));
        Assert.Contains("includeSubDomains", Header(response, "Strict-Transport-Security"));
    }

    [Fact]
    public async Task CspReportEndpoint_AcceptsBrowserReports()
    {
        const string report = """
            {"csp-report":{"document-uri":"https://petwork.test/reset?token=secret","violated-directive":"script-src","blocked-uri":"inline"}}
            """;
        using var content = new StringContent(report, Encoding.UTF8, "application/csp-report");

        using var response = await _client.PostAsync("/security/csp-report", content);

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task HttpRequests_ArePermanentlyRedirectedToHttps()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://petwork.test"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/css/style.css");

        Assert.Equal(System.Net.HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("https://petwork.test/css/style.css", response.Headers.Location?.AbsoluteUri);
    }

    [Fact]
    public async Task AnonymousAdminRequest_IsRedirectedBeforeControllerExecution()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://petwork.test"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/Admin/Users");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login?returnUrl=", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public void SessionAndAntiforgeryCookies_RequireSecureTransport()
    {
        var policy = _factory.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.CookiePolicyOptions>>().Value;
        var session = _factory.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Builder.SessionOptions>>().Value;
        var antiforgery = _factory.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>>().Value;

        Assert.Equal(CookieSecurePolicy.Always, policy.Secure);
        Assert.Equal(Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always, policy.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, session.Cookie.SecurePolicy);
        Assert.Equal(CookieSecurePolicy.Always, antiforgery.Cookie.SecurePolicy);
        Assert.True(session.Cookie.HttpOnly);
        Assert.True(antiforgery.Cookie.HttpOnly);
    }

    [Fact]
    public async Task ProductionApiErrors_HideExceptionDetails_AndReturnReferenceCode()
    {
        using var response = await _client.GetAsync("/api/test-only/unhandled-error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Users table", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\secrets\\appsettings.Production.json", body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body);
        var referenceCode = json.RootElement.GetProperty("referenceCode").GetString();
        Assert.Matches(new Regex("^ERR-[0-9]{8}-[0-9A-F]{10}$"), referenceCode!);
        Assert.Equal(referenceCode, Header(response, "X-Error-Reference"));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        string.Join(",", response.Headers.GetValues(name));
}

public sealed class SecurityHeaderFactory : WebApplicationFactory<Program>
{
    private readonly string _mediaPath = Path.Combine(Path.GetTempPath(), "petwork-header-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("MobileAuth:JwtKey", "test-only-jwt-key-with-at-least-thirty-two-bytes");
        builder.UseSetting("Email:Resend:ApiKey", "test-only-resend-key");
        builder.UseSetting("MediaStorage:StoragePath", _mediaPath);
        builder.UseSetting("DatabaseMigrations:ApplyOnStartup", "false");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddControllers().AddApplicationPart(typeof(TestOnlyErrorController).Assembly);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_mediaPath)) Directory.Delete(_mediaPath, recursive: true);
    }
}

[ApiController]
[Route("api/test-only")]
public sealed class TestOnlyErrorController : ControllerBase
{
    [HttpGet("unhandled-error")]
    public IActionResult UnhandledError() =>
        throw new InvalidOperationException("Users table query failed at C:\\secrets\\appsettings.Production.json");
}
