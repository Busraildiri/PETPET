using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;

namespace PetWork.Services;

public enum ResourceAccessRequirement
{
    OwnerOrAdmin,
    NonOwner
}

public enum ResourceAuthorizationStatus
{
    Allowed,
    Unauthenticated,
    Forbidden
}

public readonly record struct ResourceAuthorizationResult(ResourceAuthorizationStatus Status, int? UserId)
{
    public bool IsAllowed => Status == ResourceAuthorizationStatus.Allowed;
}

public sealed class ResourceAuthorizationService
{
    private readonly PetWorkDbContext _context;

    public ResourceAuthorizationService(PetWorkDbContext context) => _context = context;

    public async Task<ResourceAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal principal,
        int ownerUserId,
        ResourceAccessRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(
                principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
                out var userId))
        {
            return new(ResourceAuthorizationStatus.Unauthenticated, null);
        }

        var user = await _context.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Id, item.IsAdmin })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return new(ResourceAuthorizationStatus.Unauthenticated, null);

        var allowed = requirement switch
        {
            ResourceAccessRequirement.OwnerOrAdmin => user.Id == ownerUserId || user.IsAdmin,
            ResourceAccessRequirement.NonOwner => user.Id != ownerUserId,
            _ => false
        };

        return new(allowed ? ResourceAuthorizationStatus.Allowed : ResourceAuthorizationStatus.Forbidden, user.Id);
    }
}
