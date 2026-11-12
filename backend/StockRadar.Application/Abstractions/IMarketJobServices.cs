using StockRadar.Application.DTOs;
using StockRadar.Domain.Enums;

namespace StockRadar.Application.Abstractions;

public interface IDailySessionSyncService
{
    /// <summary>Append nến phiên hôm nay. Mặc định lần chính (kèm lọc lại danh sách mã) — gọi tay qua endpoint giữ hành vi cũ.</summary>
    Task<DailySessionSyncResultDto> RunAsync(
        CancellationToken cancellationToken = default,
        CheDoJob2 cheDo = CheDoJob2.LanChinh);
}

public interface IKbsMarketSyncService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public interface IUniverseRescreenService
{
    Task<UniverseRescreenResultDto> RunAsync(CancellationToken cancellationToken = default);
}
