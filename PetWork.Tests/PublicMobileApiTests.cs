using System.Net;
using System.Net.Http.Json;

namespace PetWork.Tests;

public sealed class PublicMobileApiTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public PublicMobileApiTests(AuthApiFactory factory) => _client = factory.CreateClient();

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
        var response = await _client.PostAsJsonAsync($"/api/mobile/nearby/{route}",
            new { latitude = 91, longitude = 181, radiusMeters = 5000 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("veterinarians")]
    [InlineData("groomers")]
    [InlineData("pet-hotels")]
    public async Task Nearby_BlankArea_ReturnsBadRequest(string route)
    {
        var response = await _client.GetAsync($"/api/mobile/nearby/{route}/search?city=%20&district=%20");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("veterinarians")]
    [InlineData("groomers")]
    [InlineData("pet-hotels")]
    public async Task Nearby_MissingGoogleKey_ReturnsServiceUnavailable(string route)
    {
        var response = await _client.PostAsJsonAsync($"/api/mobile/nearby/{route}",
            new { latitude = 41.0082, longitude = 28.9784, radiusMeters = 5000 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
