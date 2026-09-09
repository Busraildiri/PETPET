using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PetWork.Data;
using PetWork.Models;

namespace PetWork.Services;

public sealed class MobilePushNotificationService
{
    private readonly PetWorkDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MobilePushNotificationService> _logger;

    public MobilePushNotificationService(PetWorkDbContext context, IHttpClientFactory httpClientFactory,
        ILogger<MobilePushNotificationService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(MobileNotification notification, CancellationToken cancellationToken)
    {
        MobileNotificationPreference? preference;
        List<string> tokens;
        try
        {
            preference = await _context.MobileNotificationPreferences.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == notification.UserId, cancellationToken);
            if (!IsEnabled(preference, notification.Type)) return;
            tokens = await _context.MobilePushTokens.AsNoTracking()
                .Where(item => item.UserId == notification.UserId)
                .Select(item => item.Token)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Push bildirimi altyapısı henüz hazır değil; uygulama içi bildirim korundu.");
            return;
        }
        if (tokens.Count == 0) return;

        var channelId = Category(notification.Type) switch
        {
            "lost" => "lost-pets",
            "match" => "matches",
            _ => "community"
        };
        var messages = tokens.Select(token => new
        {
            to = token,
            title = notification.Title,
            body = notification.Body,
            sound = "default",
            channelId,
            data = new { type = Category(notification.Type), notificationId = notification.Id, entityType = notification.EntityType, entityId = notification.EntityId }
        }).ToArray();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await _httpClientFactory.CreateClient().PostAsJsonAsync(
                "https://exp.host/--/api/v2/push/send", messages, timeout.Token);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Expo push servisi {StatusCode} döndürdü.", response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Expo push bildirimi gönderilemedi; uygulama içi bildirim korundu.");
        }
    }

    private static bool IsEnabled(MobileNotificationPreference? preference, string type)
    {
        if (preference is null) return false;
        return Category(type) switch
        {
            "lost" => preference.LostPetNotifications,
            "match" => preference.MatchNotifications,
            _ => preference.CommunityNotifications
        };
    }

    private static string Category(string type) => type.StartsWith("lost_", StringComparison.Ordinal) ? "lost"
        : type.StartsWith("pati_match", StringComparison.Ordinal) ? "match" : "community";
}
