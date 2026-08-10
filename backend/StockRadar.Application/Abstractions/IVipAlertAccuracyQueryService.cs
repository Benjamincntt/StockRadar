using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IVipAlertAccuracyQueryService
{
    Task<VipAlertAccuracyReportDto> GetReportAsync(int days = 30, CancellationToken cancellationToken = default);
}
