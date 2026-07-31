using PrinzipPriceChecker.Api.Parsing;

namespace PrinzipPriceChecker.Api.Services;

public interface IFlatPriceSource
{
    /// <exception cref="FlatPageParseException">Страница недоступна или цену не удалось определить.</exception>
    Task<FlatSnapshot> GetSnapshotAsync(string flatUrl, CancellationToken cancellationToken);
}
