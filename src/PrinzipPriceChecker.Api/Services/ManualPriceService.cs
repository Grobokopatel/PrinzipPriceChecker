using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Contracts;
using PrinzipPriceChecker.Api.Data;
using PrinzipPriceChecker.Api.Domain;

namespace PrinzipPriceChecker.Api.Services;

/// <summary>Результат ручной замены сохранённой цены и последовавшей за ней проверки.</summary>
/// <param name="OldPrice">Старая цена.</param>
/// <param name="NewPrice">Новая цена.</param>
/// <param name="NotificationsCount">Количество отправленных уведомлений на почту.</param>
/// <param name="Error">Ошибка.</param>
public record ManualPriceChangeResult(
    long? OldPrice,
    long? NewPrice,
    int NotificationsCount,
    string? Error);

public class ManualPriceService(AppDbContext db, TimeProvider timeProvider, PriceMonitor monitor)
{
    /// <summary>Заменяет сохранённую цену квартиры. Возвращает null, если квартира не найдена.</summary>
    public async Task<ManualPriceChangeResult?> SetPriceAsync(
        TrackedFlat flat,
        long newPrice,
        bool sendNotifications,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var previousPrice = flat.CurrentPrice;

        var notificationsSent = 0;
        if (flat.CurrentPrice != newPrice)
        {
            flat.CurrentPrice = newPrice;
            flat.LastPriceChangeAt = now;

            db.PriceHistory.Add(new PriceHistoryEntry
            {
                TrackedFlatId = flat.Id,
                Price = newPrice,
                DetectedAt = now,
            });

            if (sendNotifications)
            {
                notificationsSent =
                    await monitor.NotifySubscribersAsync(flat, previousPrice!.Value, newPrice, now, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return new ManualPriceChangeResult(previousPrice, newPrice, notificationsSent, null);
    }
}