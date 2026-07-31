namespace PrinzipPriceChecker.Api.Domain;

/// <summary>
/// Цена квартиры в определённый момент.
/// </summary>
public class PriceHistoryEntry
{
    public int Id { get; set; }

    public int TrackedFlatId { get; set; }

    public TrackedFlat TrackedFlat { get; set; } = null!;

    public long Price { get; set; }

    public DateTimeOffset DetectedAt { get; set; }
}
