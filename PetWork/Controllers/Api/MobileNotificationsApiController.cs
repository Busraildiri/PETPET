using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

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

    [HttpGet("preferences")]
    public async Task<ActionResult<MobileNotificationPreferencesResponse>> GetPreferences(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var preference = await _context.MobileNotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return Ok(preference is null
            ? new MobileNotificationPreferencesResponse(false, false, false, false)
            : new MobileNotificationPreferencesResponse(preference.CommunityNotifications,
                preference.LostPetNotifications, preference.MatchNotifications, true));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<MobileNotificationPreferencesResponse>> UpdatePreferences(
        MobileNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var preference = await _context.MobileNotificationPreferences
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new MobileNotificationPreference { UserId = userId };
            _context.MobileNotificationPreferences.Add(preference);
        }
        preference.CommunityNotifications = request.CommunityNotifications;
        preference.LostPetNotifications = request.LostPetNotifications;
        preference.MatchNotifications = request.MatchNotifications;
        preference.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new MobileNotificationPreferencesResponse(preference.CommunityNotifications,
            preference.LostPetNotifications, preference.MatchNotifications, true));
    }

    [HttpPut("push-token")]
    public async Task<IActionResult> RegisterPushToken(MobilePushTokenRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var tokenValue = request.Token.Trim();
        if (!(tokenValue.StartsWith("ExponentPushToken[", StringComparison.Ordinal) ||
              tokenValue.StartsWith("ExpoPushToken[", StringComparison.Ordinal)) || !tokenValue.EndsWith(']'))
            return BadRequest(new { message = "Expo bildirim anahtarı geçersiz." });

        var token = await _context.MobilePushTokens.FirstOrDefaultAsync(item => item.Token == tokenValue, cancellationToken);
        if (token is null)
        {
            token = new MobilePushToken { Token = tokenValue };
            _context.MobilePushTokens.Add(token);
        }
        token.UserId = userId;
        token.Platform = string.IsNullOrWhiteSpace(request.Platform) ? null : request.Platform.Trim()[..Math.Min(20, request.Platform.Trim().Length)];
        token.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("push-token")]
    public async Task<IActionResult> RemovePushToken(MobilePushTokenRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _context.MobilePushTokens
            .Where(item => item.UserId == userId && item.Token == request.Token.Trim())
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out int userId) => int.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}

public sealed record MobileNotificationResponse(int Id, string Type, string Title, string Body,
    string? EntityType, int? EntityId, bool IsRead, DateTime CreatedAt);
public sealed record MobileNotificationsResponse(IReadOnlyList<MobileNotificationResponse> Items, int UnreadCount);
public sealed record MobileNotificationPreferencesResponse(bool CommunityNotifications,
    bool LostPetNotifications, bool MatchNotifications, bool IsConfigured);
public sealed record MobileNotificationPreferencesRequest(bool CommunityNotifications,
    bool LostPetNotifications, bool MatchNotifications);
public sealed record MobilePushTokenRequest(string Token, string? Platform);
