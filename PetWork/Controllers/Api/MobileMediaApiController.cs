using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;

namespace PetWork.Controllers.Api;

[ApiController]
public sealed class MobileMediaApiController : ControllerBase
{
    private readonly PetWorkDbContext _context;

    public MobileMediaApiController(PetWorkDbContext context) => _context = context;

    [HttpGet("/uploads/{**path}")]
    [AllowAnonymous]
    [EnableRateLimiting("media")]
    public async Task<IActionResult> Get(string path, CancellationToken cancellationToken)
    {
        var storageKey = $"uploads/{path.Replace('\\', '/').TrimStart('/')}";
        if (storageKey.Contains("..", StringComparison.Ordinal)) return BadRequest();
        var asset = await _context.MobileMediaAssets.AsNoTracking()
            .Where(item => item.StorageKey == storageKey)
            .Select(item => new { item.Data, item.ContentType })
            .FirstOrDefaultAsync(cancellationToken);
        if (asset is null) return NotFound();

        // Yalnızca başarılı yanıt önbelleklenir. Öznitelik olarak verildiğinde 404'ler de
        // önbelleklenir ve istemci, görsel sonradan eklense bile bir gün boyunca sormaz.
        Response.Headers.CacheControl = "public,max-age=86400";
        return File(asset.Data, asset.ContentType);
    }
}
