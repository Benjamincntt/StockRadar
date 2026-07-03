using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IBacktestService
{
    Task<SmartMoneyBacktestResultDto> RunSmartMoneyAsync(
        SmartMoneyBacktestRequestDto request,
        CancellationToken cancellationToken = default);
}
