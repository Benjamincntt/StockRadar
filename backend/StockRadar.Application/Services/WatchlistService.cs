using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Constants;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class WatchlistService(
    IWatchlistRepository watchlist,
    IWatchlistListRepository watchlists,
    IStockRepository stocks,
    IDailyOpportunityRepository dailyOpportunities,
    SmartMoneyEvaluationService smartMoneyEval,
    IBuyDecisionEngine buyDecision) : IWatchlistService
{
    // ==== Backward compat — danh sách mặc định (api/v1/watchlist-items) ====

    public async Task<IReadOnlyList<WatchlistItemDto>> GetItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var symbols = await watchlist.GetSymbolsAsync(cancellationToken);
        return await EnrichItemsAsync(symbols, cancellationToken);
    }

    public async Task<bool> AddAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var stock = await stocks.GetBySymbolAsync(symbol, cancellationToken)
            ?? throw new AppException("Not Found", $"Không tìm thấy mã {symbol.ToUpperInvariant()}", 404);

        if (await watchlist.ContainsAsync(stock.Symbol, cancellationToken))
            return false;

        await watchlist.AddAsync(stock.Symbol, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (!await watchlist.ContainsAsync(symbol, cancellationToken))
            return false;

        await watchlist.RemoveAsync(symbol, cancellationToken);
        return true;
    }

    // ==== Multi-watchlist (api/v1/watchlists) ====

    public async Task<IReadOnlyList<WatchlistDto>> GetWatchlistsAsync(
        CancellationToken cancellationToken = default)
    {
        // Lazy seed: danh sách mặc định + danh sách ngành theo danh mục chuẩn (chỉ tạo 1 lần/user).
        await watchlists.GetOrCreateDefaultAsync(cancellationToken);
        await watchlists.EnsureSectorWatchlistsAsync(SectorCatalog.DefaultSectors, cancellationToken);

        var rows = await watchlists.GetAllAsync(cancellationToken);
        return await BuildDtosAsync(rows, cancellationToken);
    }

    public async Task<WatchlistDto?> GetWatchlistAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await watchlists.GetByIdAsync(id, cancellationToken);
        if (row is null)
            return null;

        var dtos = await BuildDtosAsync([row], cancellationToken);
        return dtos.Count > 0 ? dtos[0] : null;
    }

    public async Task<WatchlistDto> CreateWatchlistAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            throw new AppException("Bad Request", "Tên danh sách không được để trống", 400);

        var row = await watchlists.CreateAsync(trimmed, cancellationToken);
        return new WatchlistDto(
            row.Id, row.Name, row.LaDanhSachNganh, row.MaNganh, row.LaMacDinh, 0, row.CreatedAt);
    }

    public async Task<bool> RenameWatchlistAsync(
        int id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (newName ?? "").Trim();
        if (trimmed.Length == 0)
            throw new AppException("Bad Request", "Tên danh sách không được để trống", 400);

        var row = await watchlists.GetByIdAsync(id, cancellationToken);
        if (row is null)
            return false;

        if (row.LaDanhSachNganh)
            throw new AppException("Bad Request", "Không thể đổi tên danh sách ngành tự động", 400);

        return await watchlists.RenameAsync(id, trimmed, cancellationToken);
    }

    public async Task<bool> DeleteWatchlistAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await watchlists.GetByIdAsync(id, cancellationToken);
        if (row is null)
            return false;

        if (row.LaDanhSachNganh || row.LaMacDinh)
            throw new AppException("Bad Request", "Không thể xóa danh sách mặc định hoặc danh sách ngành", 400);

        return await watchlists.DeleteAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<WatchlistItemDto>> GetWatchlistItemsAsync(
        int watchlistId,
        CancellationToken cancellationToken = default)
    {
        var row = await watchlists.GetByIdAsync(watchlistId, cancellationToken)
            ?? throw new AppException("Not Found", "Không tìm thấy danh sách theo dõi", 404);

        // Danh sách ngành: items động — query Stocks theo ngành, không đọc WatchlistItems.
        var symbols = row.LaDanhSachNganh
            ? await watchlists.GetSectorSymbolsAsync(row.MaNganh!, cancellationToken)
            : await watchlist.GetSymbolsAsync(watchlistId, cancellationToken);

        return await EnrichItemsAsync(symbols, cancellationToken);
    }

    public async Task<bool> AddItemAsync(
        int watchlistId,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var row = await watchlists.GetByIdAsync(watchlistId, cancellationToken)
            ?? throw new AppException("Not Found", "Không tìm thấy danh sách theo dõi", 404);

        if (row.LaDanhSachNganh)
            throw new AppException("Bad Request", "Danh sách ngành tự động — không thể thêm mã thủ công", 400);

        var stock = await stocks.GetBySymbolAsync(symbol, cancellationToken)
            ?? throw new AppException("Not Found", $"Không tìm thấy mã {symbol.ToUpperInvariant()}", 404);

        if (await watchlist.ContainsAsync(watchlistId, stock.Symbol, cancellationToken))
            return false;

        await watchlist.AddAsync(watchlistId, stock.Symbol, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveItemAsync(
        int watchlistId,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var row = await watchlists.GetByIdAsync(watchlistId, cancellationToken)
            ?? throw new AppException("Not Found", "Không tìm thấy danh sách theo dõi", 404);

        if (row.LaDanhSachNganh)
            throw new AppException("Bad Request", "Danh sách ngành tự động — không thể xóa mã thủ công", 400);

        if (!await watchlist.ContainsAsync(watchlistId, symbol, cancellationToken))
            return false;

        await watchlist.RemoveAsync(watchlistId, symbol, cancellationToken);
        return true;
    }

    // ==== Helpers ====

    /// <summary>Map danh sách → DTO kèm số mã (ngành: đếm mã active của ngành; thường: đếm items).</summary>
    private async Task<IReadOnlyList<WatchlistDto>> BuildDtosAsync(
        IReadOnlyList<WatchlistRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var itemCounts = await watchlists.GetItemCountsAsync(cancellationToken);
        IReadOnlyDictionary<string, int>? sectorCounts = rows.Any(r => r.LaDanhSachNganh)
            ? await watchlists.GetActiveSectorStockCountsAsync(cancellationToken)
            : null;

        return rows
            .Select(r => new WatchlistDto(
                r.Id,
                r.Name,
                r.LaDanhSachNganh,
                r.MaNganh,
                r.LaMacDinh,
                r.LaDanhSachNganh
                    ? sectorCounts!.GetValueOrDefault(r.MaNganh ?? "", 0)
                    : itemCounts.GetValueOrDefault(r.Id, 0),
                r.CreatedAt))
            .ToList();
    }

    /// <summary>Enrich danh sách mã: tên + ngành + điểm (Top dùng snapshot, ngoài Top chấm live).</summary>
    private async Task<IReadOnlyList<WatchlistItemDto>> EnrichItemsAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken)
    {
        if (symbols.Count == 0)
            return [];

        var summaries = await stocks.GetSummariesBySymbolsAsync(symbols, cancellationToken);
        if (summaries.Count == 0)
            return [];

        var summarySymbols = summaries.Select(s => s.Symbol).ToList();
        var oppDate = TradingCalendar.GetActiveOpportunityDate();
        var opportunityScores = await dailyOpportunities.GetScoresBySymbolsForDateAsync(
            oppDate,
            summarySymbols,
            cancellationToken);

        // Mã ngoài Top: Buy Score live — cùng engine với StockService detail (không dùng Criterion Composite).
        Dictionary<string, int>? liveScores = null;
        var missing = summarySymbols.Where(s => !opportunityScores.ContainsKey(s)).ToList();
        if (missing.Count > 0)
        {
            liveScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var context = await smartMoneyEval.BuildContextAsync(cancellationToken);
            foreach (var symbol in missing)
            {
                var stock = await stocks.GetBySymbolAsync(symbol, cancellationToken);
                if (stock is null)
                    continue;
                liveScores[symbol] = buyDecision.Evaluate(stock, context).BuyScore;
            }
        }

        var items = summaries.Select(summary =>
        {
            var score = opportunityScores.TryGetValue(summary.Symbol, out var oppScore)
                ? oppScore
                : liveScores?.GetValueOrDefault(summary.Symbol) ?? 0;

            return new WatchlistItemDto(
                summary.Symbol,
                summary.Name,
                summary.Sector,
                score,
                summary.LastChangePercent,
                summary.SectorLocked);
        });

        return items.OrderByDescending(w => w.Score).ToList();
    }
}
