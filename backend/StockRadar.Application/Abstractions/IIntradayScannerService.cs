namespace StockRadar.Application.Abstractions;

public interface IIntradayScannerService
{
    Task<int> ScanAsync(CancellationToken cancellationToken = default);
}
