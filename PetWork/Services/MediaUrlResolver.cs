using Microsoft.AspNetCore.Mvc;

namespace PetWork.Services;

public static class MediaUrlResolver
{
    public static string Resolve(IUrlHelper url, string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (TryResolveExternalHttpUrl(candidate, out var externalUrl))
            return externalUrl;

        // An absolute value with any other scheme (javascript:, data:, file:, ...)
        // must never be converted into a browser-facing URL.
        if (Uri.TryCreate(candidate, UriKind.Absolute, out _))
            candidate = fallback;

        return url.Content("~/" + candidate.TrimStart('/', '\\'));
    }

    public static bool TryResolveExternalHttpUrl(string? value, out string safeUrl)
    {
        safeUrl = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var absolute) ||
            (absolute.Scheme != Uri.UriSchemeHttps && absolute.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(absolute.Host))
            return false;

        safeUrl = absolute.AbsoluteUri;
        return true;
    }
}
