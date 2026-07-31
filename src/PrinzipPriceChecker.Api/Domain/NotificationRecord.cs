namespace PrinzipPriceChecker.Api.Domain;

/// <summary>
/// Журнал отправленных на почту уведомлений. Нужен и для истории, и для тестов.
/// Через GET /api/notifications видно, какие письма ушли и с каким результатом.
/// </summary>
public class NotificationRecord
{
    public int Id { get; set; }

    public int? SubscriptionId { get; set; }

    public int TrackedFlatId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FlatUrl { get; set; } = string.Empty;

    public long? OldPrice { get; set; }

    public long NewPrice { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsSent { get; set; }

    public string? Error { get; set; }
}
