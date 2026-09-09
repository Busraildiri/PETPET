using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PetWork.Controllers;

[AllowAnonymous]
[Route("mobile/reset-password")]
public sealed class MobilePasswordResetLinkController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MobilePasswordResetLinkController(IConfiguration configuration) => _configuration = configuration;

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Open([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            return BadRequest("Şifre yenileme bağlantısı geçersiz.");

        var deepLinkBase = _configuration["Email:PasswordReset:AppDeepLinkBase"] ?? "petim://reset-password";
        var separator = deepLinkBase.Contains('?') ? '&' : '?';
        var appUrl = $"{deepLinkBase}{separator}token={Uri.EscapeDataString(token)}";
        var encodedAppUrl = WebUtility.HtmlEncode(appUrl);
        var serializedAppUrl = JsonSerializer.Serialize(appUrl);

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        var html = $$"""
            <!doctype html>
            <html lang="tr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Pet'im şifre yenileme</title>
              <style>
                body{margin:0;background:#fff9f2;color:#30262b;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;display:grid;min-height:100vh;place-items:center;padding:24px;box-sizing:border-box}
                main{width:min(100%,420px);background:#fff;border:1px solid #eadfda;border-radius:28px;padding:32px;box-shadow:0 18px 45px rgba(70,45,64,.12);text-align:center}
                h1{margin:0 0 12px;font-size:30px}p{color:#756a70;line-height:1.55;margin:0 0 24px}
                a{display:block;background:#71486b;color:#fff;text-decoration:none;font-weight:800;border-radius:17px;padding:16px}
                small{display:block;color:#91868c;margin-top:18px;line-height:1.45}
              </style>
            </head>
            <body>
              <main>
                <h1>Yeni şifreni belirle</h1>
                <p>Pet'im uygulamasında şifre yenileme ekranını açmak için düğmeye dokun.</p>
                <a href="{{encodedAppUrl}}">Pet'im uygulamasında aç</a>
                <small>Uygulama açılmazsa Expo Go'nun çalıştığını ve aynı Wi-Fi ağına bağlı olduğunu kontrol et.</small>
              </main>
              <script>setTimeout(function(){ window.location.href = {{serializedAppUrl}}; }, 250);</script>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }
}
