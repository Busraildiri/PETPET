using PetWork.Services;

namespace PetWork.Tests;

public sealed class MediaUrlResolverTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/local/path")]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolveExternalHttpUrl_RejectsNonHttpUrls(string? value) =>
        Assert.False(MediaUrlResolver.TryResolveExternalHttpUrl(value, out _));

    [Theory]
    [InlineData("https://example.com/article", "https://example.com/article")]
    [InlineData("http://example.com/article", "http://example.com/article")]
    public void TryResolveExternalHttpUrl_AllowsHttpUrls(string value, string expected)
    {
        Assert.True(MediaUrlResolver.TryResolveExternalHttpUrl(value, out var safeUrl));
        Assert.Equal(expected, safeUrl);
    }
}
