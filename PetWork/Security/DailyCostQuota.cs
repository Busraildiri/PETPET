using System.Data;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Security;

public sealed class DailyCostQuotaSettings
{
    public Dictionary<string, int> DailyLimits { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public readonly record struct DailyCostQuotaDecision(bool Allowed, int Remaining, TimeSpan RetryAfter);

/// <summary>
/// Uses the application database so the quota is shared by every server instance and survives restarts.
/// A consumed attempt is not refunded when the upstream provider fails: cost controls fail closed.
/// </summary>
public sealed class DailyCostQuotaService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DailyCostQuotaSettings _settings;

    public DailyCostQuotaService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _settings = configuration.GetSection("CostControls").Get<DailyCostQuotaSettings>()
            ?? new DailyCostQuotaSettings();
    }

    public int GetLimit(string category)
    {
        if (!_settings.DailyLimits.TryGetValue(category, out var limit) || limit <= 0)
            throw new InvalidOperationException($"Daily cost limit '{category}' is missing or invalid.");
        return limit;
    }

    public async Task<DailyCostQuotaDecision> TryConsumeAsync(
        string category,
        string subject,
        CancellationToken cancellationToken = default)
    {
        var limit = GetLimit(category);
        var now = DateTime.UtcNow;
        var day = now.Date;
        var retryAfter = day.AddDays(1) - now;
        var subjectHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(subject.Trim().ToLowerInvariant())));

        // The composite primary key serializes concurrent first-use attempts. A unique-key
        // race retries once in a fresh DbContext and then observes the committed counter.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);

            var usage = await db.DailyCostUsages.SingleOrDefaultAsync(item =>
                item.Category == category &&
                item.SubjectHash == subjectHash &&
                item.UsageDateUtc == day,
                cancellationToken);

            if (usage is not null && usage.Count >= limit)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(false, 0, retryAfter);
            }

            if (usage is null)
            {
                usage = new DailyCostUsage
                {
                    Category = category,
                    SubjectHash = subjectHash,
                    UsageDateUtc = day,
                    Count = 1,
                    UpdatedAtUtc = now
                };
                db.DailyCostUsages.Add(usage);
            }
            else
            {
                usage.Count++;
                usage.UpdatedAtUtc = now;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(true, limit - usage.Count, TimeSpan.Zero);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        // Fail closed if a concurrent update could not be resolved safely.
        return new(false, 0, retryAfter);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DailyCostQuotaAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public DailyCostQuotaAttribute(string category, string? accountProperty = null)
    {
        Category = category;
        AccountProperty = accountProperty;
    }

    public string Category { get; }
    public string? AccountProperty { get; }
    public int Order => int.MinValue + 110;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var subject = FindSubject(context);
        var limiter = context.HttpContext.RequestServices.GetRequiredService<DailyCostQuotaService>();
        var decision = await limiter.TryConsumeAsync(Category, subject, context.HttpContext.RequestAborted);
        if (decision.Allowed)
        {
            context.HttpContext.Response.Headers["X-Daily-Quota-Remaining"] =
                decision.Remaining.ToString(CultureInfo.InvariantCulture);
            await next();
            return;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Result = CreateRejectedResult(seconds);
    }

    public static ObjectResult CreateRejectedResult(int retryAfterSeconds) => new(new
    {
        message = "Günlük kullanım sınırına ulaştın. Kota 00:00 UTC'de yenilenir.",
        retryAfterSeconds
    }) { StatusCode = StatusCodes.Status429TooManyRequests };

    private string FindSubject(ActionExecutingContext context)
    {
        if (!string.IsNullOrWhiteSpace(AccountProperty))
        {
            foreach (var argument in context.ActionArguments.Values.Where(value => value is not null))
            {
                var property = argument!.GetType().GetProperty(AccountProperty,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                var value = property?.GetValue(argument)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return $"account:{value}";
            }
        }

        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.Session.GetInt32("UserId")?.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(userId)) return $"user:{userId}";

        return $"ip:{context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
