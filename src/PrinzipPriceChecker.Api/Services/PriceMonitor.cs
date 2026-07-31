using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Data;
using PrinzipPriceChecker.Api.Domain;
using PrinzipPriceChecker.Api.Parsing;
using PrinzipPriceChecker.Api.Services.Email;

namespace PrinzipPriceChecker.Api.Services;

/// <summary>
/// Сравнивает сохранённую цену квартиры с ценой на сайте и, если она изменилась,
/// обновляет данные, пишет историю и рассылает уведомления подписчикам.
/// </summary>
public class PriceMonitor(
    AppDbContext db,
    IFlatPriceSource priceSource,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<PriceMonitor> logger)
{
    public async Task<IReadOnlyList<FlatCheckResult>> CheckAllAsync(CancellationToken cancellationToken)
    {
        var flats = await db.TrackedFlats
            .Include(flat => flat.Subscriptions)
            .OrderBy(flat => flat.Id)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Запускаем проверку цен: квартир к проверке - {Count}", flats.Count);

        var results = new List<FlatCheckResult>(flats.Count);

        foreach (var flat in flats)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await CheckAsync(flat, cancellationToken));
        }

        return results;
    }

    /// <summary>Проверяет цену одной квартиры.</summary>
    /// <returns><c>null</c>, если квартиры с таким идентификатором нет.</returns>
    public async Task<FlatCheckResult?> CheckFlatAsync(int flatId, CancellationToken cancellationToken)
    {
        var flat = await db.TrackedFlats
            .Include(f => f.Subscriptions)
            .FirstOrDefaultAsync(f => f.Id == flatId, cancellationToken);

        return flat is null ? null : await CheckAsync(flat, cancellationToken);
    }

    private async Task<FlatCheckResult> CheckAsync(TrackedFlat flat, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Цена у отслеживаемой квартиры есть всегда: без неё подписка не оформляется.
        var oldPrice = flat.CurrentPrice!.Value;

        FlatSnapshot snapshot;
        try
        {
            snapshot = await priceSource.GetSnapshotAsync(flat.Url, cancellationToken);
        }
        catch (FlatPageParseException exception)
        {
            flat.LastCheckedAt = now;
            flat.LastCheckError = exception.Message;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Не удалось проверить цену для {FlatUrl}: {Error}",
                flat.Url,
                exception.Message);

            return new FlatCheckResult(flat.Id, flat.Url, oldPrice, null, false, 0, exception.Message);
        }

        flat.LastCheckedAt = now;
        flat.LastCheckError = null;
        flat.Name = snapshot.Name ?? flat.Name;
        flat.Description = snapshot.Description ?? flat.Description;

        var priceChanged = oldPrice != snapshot.Price;

        if (priceChanged)
        {
            flat.CurrentPrice = snapshot.Price;
            flat.LastPriceChangeAt = now;

            db.PriceHistory.Add(new PriceHistoryEntry
            {
                TrackedFlatId = flat.Id,
                Price = snapshot.Price,
                DetectedAt = now,
            });
        }

        var notificationsSent = priceChanged
            ? await NotifySubscribersAsync(flat, oldPrice, snapshot.Price, now, cancellationToken)
            : 0;

        await db.SaveChangesAsync(cancellationToken);

        if (priceChanged)
        {
            logger.LogInformation(
                "Цена изменилась для {FlatUrl}: {OldPrice} -> {NewPrice}, отправлено писем: {Sent}",
                flat.Url,
                oldPrice,
                snapshot.Price,
                notificationsSent);
        }
        else
        {
            logger.LogDebug("Цена не изменилась для {FlatUrl}: {Price}", flat.Url, snapshot.Price);
        }

        return new FlatCheckResult(
            flat.Id, 
            flat.Url,
            oldPrice,
            snapshot.Price,
            priceChanged,
            notificationsSent,
            null);
    }

    public async Task<int> NotifySubscribersAsync(
        TrackedFlat flat,
        long oldPrice,
        long newPrice,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sent = 0;

        foreach (var subscription in flat.Subscriptions)
        {
            var (subject, body) = BuildNotification(flat, oldPrice, newPrice);

            var record = new NotificationRecord
            {
                SubscriptionId = subscription.Id,
                TrackedFlatId = flat.Id,
                Email = subscription.Email,
                FlatUrl = flat.Url,
                OldPrice = oldPrice,
                NewPrice = newPrice,
                Subject = subject,
                Body = body,
                CreatedAt = now,
            };

            try
            {
                await emailSender.SendAsync(
                    new EmailMessage(subscription.Email, subject, body),
                    cancellationToken);

                record.IsSent = true;
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Одно неотправленное письмо не должно ломать проверку остальных подписок:
                // логируем ошибку и идём дальше.
                record.IsSent = false;
                record.Error = exception.Message;

                logger.LogError(
                    exception,
                    "Не удалось отправить уведомление на {Email} по квартире {FlatUrl}",
                    subscription.Email,
                    flat.Url);
            }

            // Сохраняем уведомление, даже если письмо не отправлено, чтобы легко можно было посмотреть ошибку
            db.Notifications.Add(record);
        }

        return sent;
    }

    internal static (string Subject, string Body) BuildNotification(
        TrackedFlat flat,
        long oldPrice,
        long newPrice)
    {
        var direction = newPrice > oldPrice ? "выросла" : "снизилась";
        var difference = Math.Abs(newPrice - oldPrice);
        var title = string.IsNullOrWhiteSpace(flat.Name) ? "Квартира" : flat.Name;

        var subject = $"Цена {direction}: {title} - {PriceFormatter.Format(newPrice)}";

        var body = $"""
            Здравствуйте.

            Цена квартиры, на которую вы подписаны, {direction}.

            {title}
            {flat.Description}

            Было:  {PriceFormatter.Format(oldPrice)}
            Стало: {PriceFormatter.Format(newPrice)}
            Разница: {PriceFormatter.Format(difference)}

            Ссылка на объявление: {flat.Url}

            ---
            Это письмо отправлено сервисом Prinzip Price Checker.
            """;

        return (subject, body);
    }
}
