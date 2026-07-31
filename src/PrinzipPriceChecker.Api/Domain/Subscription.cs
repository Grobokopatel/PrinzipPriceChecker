namespace PrinzipPriceChecker.Api.Domain;

/// <summary>Подписка конкретного email на изменение цены конкретной квартиры.</summary>
public class Subscription
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public int TrackedFlatId { get; set; }

    public TrackedFlat TrackedFlat { get; set; } = null!;
}
