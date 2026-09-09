using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(string path, CancellationToken cancellationToken)
    {
        var storageKey = $"uploads/{path.Replace('\\', '/').TrimStart('/')}";
        if (storageKey.Contains("..", StringComparison.Ordinal)) return BadRequest();
        var asset = await _context.MobileMediaAssets.AsNoTracking()
            .Where(item => item.StorageKey == storageKey)
            .Select(item => new { item.Data, item.ContentType })
            .FirstOrDefaultAsync(cancellationToken);
        return asset is null ? NotFound() : File(asset.Data, asset.ContentType);
    }
}
