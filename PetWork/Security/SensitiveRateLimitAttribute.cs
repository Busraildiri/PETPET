using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Security;

public sealed class RateLimitSettings
{
    public Dictionary<string, RateLimitRule> Policies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RateLimitRule
{
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; }
}

public readonly record struct RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

public sealed class SensitiveEndpointRateLimiter
{
    private readonly RateLimitSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;

    public SensitiveEndpointRateLimiter(
        IOptions<RateLimitSettings> settings,
        IServiceScopeFactory scopeFactory)
    {
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
    }

    public async Task<RateLimitDecision> AttemptAsync(
        string policyName,
        string ipAddress,
        string? accountKey,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Policies.TryGetValue(policyName, out var rule) ||
            rule.PermitLimit <= 0 || rule.WindowSeconds <= 0)
            throw new InvalidOperationException($"Rate limit policy '{policyName}' is missing or invalid.");

        var ipDecision = await ConsumeAsync(policyName, $"ip:{ipAddress}", rule, cancellationToken);
        if (!ipDecision.Allowed) return ipDecision;

        return string.IsNullOrWhiteSpace(accountKey)
            ? ipDecision
            : await ConsumeAsync(policyName, $"account:{accountKey.Trim().ToLowerInvariant()}", rule, cancellationToken);
    }

    private async Task<RateLimitDecision> ConsumeAsync(
        string policyName,
        string subject,
        RateLimitRule rule,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var windowTicks = TimeSpan.FromSeconds(rule.WindowSeconds).Ticks;
        var windowStart = new DateTime(now.Ticks - now.Ticks % windowTicks, DateTimeKind.Utc);
        var retryAfter = windowStart.AddSeconds(rule.WindowSeconds) - now;
        var subjectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PetWorkDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var usage = await db.RateLimitUsages.SingleOrDefaultAsync(item =>
                item.Policy == policyName &&
                item.SubjectHash == subjectHash,
                cancellationToken);

            if (usage is not null && usage.WindowStartedAtUtc != windowStart)
            {
                usage.WindowStartedAtUtc = windowStart;
                usage.Count = 0;
            }

            if (usage is not null && usage.Count >= rule.PermitLimit)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(false, retryAfter);
            }

            if (usage is null)
            {
                usage = new RateLimitUsage
                {
                    Policy = policyName,
                    SubjectHash = subjectHash,
                    WindowStartedAtUtc = windowStart,
                    Count = 1,
                    UpdatedAtUtc = now
                };
                db.RateLimitUsages.Add(usage);
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
                return new(true, TimeSpan.Zero);
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (attempt == 1) return new(false, retryAfter);
            }
        }

        return new(false, retryAfter);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveRateLimitAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public SensitiveRateLimitAttribute(string policyName, string? accountProperty = null)
    {
        PolicyName = policyName;
        AccountProperty = accountProperty;
    }

    public string PolicyName { get; }
    public string? AccountProperty { get; }
    public int Order => int.MinValue + 100;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var limiter = context.HttpContext.RequestServices.GetRequiredService<SensitiveEndpointRateLimiter>();
        var accountKey = FindAccountKey(context);
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var decision = await limiter.AttemptAsync(
            PolicyName, ipAddress, accountKey, context.HttpContext.RequestAborted);
        if (decision.Allowed)
        {
            await next();
            return;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(new
        {
            message = $"Çok fazla deneme yaptın. {FormatWait(seconds)} sonra tekrar dene.",
            retryAfterSeconds = seconds
        }) { StatusCode = StatusCodes.Status429TooManyRequests };
    }

    private string? FindAccountKey(ActionExecutingContext context)
    {
        if (!string.IsNullOrWhiteSpace(AccountProperty))
        {
            foreach (var argument in context.ActionArguments.Values.Where(value => value is not null))
            {
                var property = argument!.GetType().GetProperty(AccountProperty,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                var value = property?.GetValue(argument)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        return context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.Session.GetInt32("UserId")?.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatWait(int seconds)
    {
        if (seconds < 60) return $"{seconds} saniye";
        var minutes = (int)Math.Ceiling(seconds / 60d);
        return minutes < 60 ? $"{minutes} dakika" : $"{(int)Math.Ceiling(minutes / 60d)} saat";
    }
}
