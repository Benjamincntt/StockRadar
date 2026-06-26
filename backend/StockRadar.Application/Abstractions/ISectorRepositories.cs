namespace StockRadar.Application.Abstractions;

public interface ISectorCatalogRepository
{
    Task<IReadOnlyList<string>> GetActiveNamesAsync(CancellationToken cancellationToken = default);
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string sectorName, CancellationToken cancellationToken = default);
}

public interface IStockSectorRepository
{
    Task<bool> UpdateSectorAsync(string symbol, string sector, CancellationToken cancellationToken = default);
}
