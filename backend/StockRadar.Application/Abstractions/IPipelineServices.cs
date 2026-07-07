namespace StockRadar.Application.Abstractions;

public interface IOpportunityIntradayMonitorService
{
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
