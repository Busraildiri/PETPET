using Microsoft.EntityFrameworkCore;
using PetWork.Data;

namespace PetWork.Security;

public readonly record struct AdminAccessResult(bool IsAuthenticated, bool IsAdmin);

public sealed class AdminAccessService
{
    private readonly PetWorkDbContext _db;

    public AdminAccessService(PetWorkDbContext db) => _db = db;

    public async Task<AdminAccessResult> CheckAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var userId = context.Session.GetInt32("UserId");
        return await CheckUserAsync(userId, cancellationToken);
    }

    public async Task<AdminAccessResult> CheckUserAsync(int? userId, CancellationToken cancellationToken = default)
    {
        if (!userId.HasValue)
            return new(false, false);

        // The database is authoritative. A role copied into the session can be stale
        // after an administrator revokes access.
        var isAdmin = await _db.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId.Value && user.IsAdmin, cancellationToken);
        return new(true, isAdmin);
    }
}
