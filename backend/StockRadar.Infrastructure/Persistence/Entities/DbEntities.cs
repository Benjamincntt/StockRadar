namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class StockEntity
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public bool SectorLocked { get; set; }
    public string HistoryJson { get; set; } = "[]";
    public decimal LastChangePercent { get; set; }
    /// <summary>Close/Volume của bar cuối trong <see cref="HistoryJson"/> — denormalize để tính breadth
    /// không phải parse JSON (xem <c>EfStockRepository.GetBreadthStatsAsync</c>).</summary>
    public decimal LastClose { get; set; }
    public long LastVolume { get; set; }
    public bool IsActive { get; set; }
    public string Exchange { get; set; } = "";
    public decimal AvgVolume30d { get; set; }
    public bool TradingRestricted { get; set; }
    public string? TradingStatus { get; set; }
    public DateOnly? FirstTradeDate { get; set; }
    public DateTime? UniverseUpdatedAt { get; set; }
}

public sealed class DailyAnalysisRunEntity
{
    public DateOnly ForTradingDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int StocksScored { get; set; }
    public int OpportunitiesSaved { get; set; }

    /// <summary>Gate rejection stats của lần quét: JSON nhãn gate tiếng Việt → số mã bị loại.</summary>
    public string? GateStatsJson { get; set; }
}

/// <summary>Lần chạy cuối của mỗi pipeline job (1 dòng/job, upsert). Nuôi màn hình Jobs.</summary>
public sealed class JobRunStatusEntity
{
    public string JobId { get; set; } = "";
    /// <summary>success | failed</summary>
    public string Status { get; set; } = "";
    /// <summary>schedule | manual</summary>
    public string? TriggeredBy { get; set; }
    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastFinishedAt { get; set; }
    public long? LastDurationMs { get; set; }
    public string? Summary { get; set; }
    public string? Error { get; set; }
}

public sealed class AlertEntity
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public int Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int Category { get; set; }
    public decimal? VolumeRatio { get; set; }
    public decimal? RelativeStrength { get; set; }
    public string? SectorRank { get; set; }
}

public sealed class MarketIndexEntity
{
    public string Symbol { get; set; } = "VNINDEX";
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
    public int Score { get; set; }
    public int Trend { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string HistoryJson { get; set; } = "[]";
}

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsGuest { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Danh sách theo dõi (watchlist) — mặc định, ngành (tự động) hoặc tùy chỉnh.</summary>
public sealed class WatchlistEntity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>true = danh sách ngành tự động (items query động theo <see cref="MaNganh"/>), false = tùy chỉnh.</summary>
    public bool LaDanhSachNganh { get; set; }
    /// <summary>Tên ngành cho danh sách tự động, null cho danh sách thường.</summary>
    public string? MaNganh { get; set; }
    /// <summary>Thứ tự hiển thị: 0 = mặc định, 1..N = danh sách ngành, tiếp theo = tùy chỉnh.</summary>
    public int ThuTu { get; set; }
    /// <summary>true = danh sách mặc định của user (không xóa được).</summary>
    public bool LaMacDinh { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserEntity User { get; set; } = null!;
    public ICollection<WatchlistItemEntity> Items { get; set; } = new List<WatchlistItemEntity>();
}

/// <summary>Mã trong một danh sách theo dõi — duy nhất theo (WatchlistId, Symbol).</summary>
public sealed class WatchlistItemEntity
{
    public long Id { get; set; }
    public int WatchlistId { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime AddedAt { get; set; }

    public WatchlistEntity Watchlist { get; set; } = null!;
}

public sealed class SectorDefinitionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
