namespace PrinzipPriceChecker.Api.Parsing;

/// <summary>Не удалось извлечь цену со страницы объявления.</summary>
public class FlatPageParseException(string message) : Exception(message);
