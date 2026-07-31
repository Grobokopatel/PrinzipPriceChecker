namespace PrinzipPriceChecker.Api.Parsing;

/// <summary>
/// Страница отдала 404 либо вернулась, но цену из неё получить не удалось.
/// </summary>
public class FlatNotFoundException(string message) : FlatPageParseException(message);
