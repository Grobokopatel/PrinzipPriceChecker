using System.Globalization;

namespace PrinzipPriceChecker.Api.Services;

public static class PriceFormatter
{
    private static readonly NumberFormatInfo PriceFormat = new() { NumberGroupSeparator = " " };

    private const string RubleSign = "₽";

    public static string Format(long? price) =>
        price is null ? "неизвестна" : $"{FormatAmount(price.Value)} {RubleSign}";

    private static string FormatAmount(long amount) => amount.ToString("N0", PriceFormat);
}
