using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;

namespace PetWork.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/mobile/notifications")]
public sealed class MobileNotificationsApiController : ControllerBase
{
    private readonly PetWorkDbContext _context;

    public MobileNotificationsApiController(PetWorkDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<MobileNotificationsResponse>> GetNotifications([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        take = Math.Clamp(take, 1, 100);
        var items = await _context.MobileNotifications.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .Select(item => new MobileNotificationResponse(item.Id, item.Type, item.Title, item.Body,
                item.EntityType, item.EntityId, item.IsRead, item.CreatedAt))
            .ToListAsync(cancellationToken);
        var unreadCount = await _context.MobileNotifications.CountAsync(item => item.UserId == userId && !item.IsRead, cancellationToken);
        return Ok(new MobileNotificationsResponse(items, unreadCount));
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var item = await _context.MobileNotifications.FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId, cancellationToken);
        if (item is null) return NotFound(new { message = "Bildirim bulunamadı." });
        if (!item.IsRead)
        {
            item.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _context.MobileNotifications
            .Where(item => item.UserId == userId && !item.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsRead, true), cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}

public sealed record MobileNotificationResponse(int Id, string Type, string Title, string Body,
    string? EntityType, int? EntityId, bool IsRead, DateTime CreatedAt);
public sealed record MobileNotificationsResponse(IReadOnlyList<MobileNotificationResponse> Items, int UnreadCount);
