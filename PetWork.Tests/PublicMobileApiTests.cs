using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace PetWork.Tests;

public sealed class PublicMobileApiTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;
    private readonly AuthApiFactory _factory;

    public PublicMobileApiTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Content_UnknownKind_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/mobile/content/unknown-kind");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("veterinarians")]
    [InlineData("groomers")]
    [InlineData("pet-hotels")]
    public async Task Nearby_InvalidCoordinates_ReturnBadRequest(string route)
    {
        var response = await AuthorizedAsync(HttpMethod.Post, $"/api/mobile/nearby/{route}",
            new { latitude = 91, longitude = 181, radiusMeters = 5000 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("veterinarians")]
    [InlineData("groomers")]
    [InlineData("pet-hotels")]
    public async Task Nearby_BlankArea_ReturnsBadRequest(string route)
    {
        var response = await AuthorizedAsync(HttpMethod.Get, $"/api/mobile/nearby/{route}/search?city=%20&district=%20");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("veterinarians")]
    [InlineData("groomers")]
    [InlineData("pet-hotels")]
    public async Task Nearby_MissingGoogleKey_ReturnsServiceUnavailable(string route)
    {
        var response = await AuthorizedAsync(HttpMethod.Post, $"/api/mobile/nearby/{route}",
            new { latitude = 41.0082, longitude = 28.9784, radiusMeters = 5000 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Nearby_negative_radius_is_rejected_instead_of_silently_clamped()
    {
        var response = await AuthorizedAsync(HttpMethod.Post, "/api/mobile/nearby/veterinarians",
            new { latitude = 41.0082, longitude = 28.9784, radiusMeters = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Query_and_route_limits_reject_oversized_or_negative_values()
    {
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync($"/api/mobile/content/all?search={new string('a', 101)}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/mobile/questions?take=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/mobile/content/all/-1")).StatusCode);
    }

    [Fact]
    public async Task Nearby_requires_an_authenticated_user_for_cost_attribution()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/mobile/nearby/veterinarians/search?city=Ankara")).StatusCode);
    }

    private async Task<HttpResponseMessage> AuthorizedAsync(HttpMethod method, string path, object? body = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var register = await _client.PostAsJsonAsync("/api/mobile/auth/register", new
        {
            username = $"nearby_{suffix}", email = $"nearby-{suffix}@example.com",
            password = "ValidPass1!", confirmPassword = "ValidPass1!", acceptTerms = true, rememberMe = true
        });
        var challenge = await register.Content.ReadFromJsonAsync<EmailChallengeDto>();
        var code = _factory.VerificationSender.GetCode(challenge!.ChallengeToken);
        var verified = await _client.PostAsJsonAsync("/api/mobile/auth/verify-email", new
            { challengeToken = challenge.ChallengeToken, code });
        var auth = await verified.Content.ReadFromJsonAsync<AuthDto>();

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private sealed record EmailChallengeDto(string ChallengeToken);
    private sealed record AuthDto(string Token);
}
