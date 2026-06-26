namespace StockRadar.Application.Abstractions;

public interface IZaloNotifier
{
    Task SendAsync(string message, string? symbol = null, CancellationToken cancellationToken = default);
}

public interface IOpportunityIntradayMonitorService
{
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
