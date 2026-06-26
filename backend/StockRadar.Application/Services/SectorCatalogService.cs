using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Services;

public sealed class SectorCatalogService(
    ISectorCatalogRepository catalog,
    IStockSectorRepository stockSectors,
    IStockRepository stocks) : ISectorCatalogService
{
    public Task<IReadOnlyList<string>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        catalog.GetActiveNamesAsync(cancellationToken);

    public async Task<StockSectorUpdateResultDto> UpdateStockSectorAsync(
        string symbol,
        UpdateStockSectorRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        var sector = request.Sector?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(sector))
            throw new AppException("Bad Request", "Ngành không được để trống.", 400);

        var stock = await stocks.GetBySymbolAsync(normalized, cancellationToken);
        if (stock is null)
            throw new AppException("Not Found", $"Không tìm thấy mã {normalized}.", 404);

        if (!await catalog.ExistsAsync(sector, cancellationToken))
            throw new AppException("Bad Request", $"Ngành \"{sector}\" không có trong danh mục.", 400);

        var updated = await stockSectors.UpdateSectorAsync(normalized, sector, cancellationToken);
        if (!updated)
            throw new AppException("Not Found", $"Không tìm thấy mã {normalized}.", 404);

        return new StockSectorUpdateResultDto(normalized, sector, true);
    }
}
