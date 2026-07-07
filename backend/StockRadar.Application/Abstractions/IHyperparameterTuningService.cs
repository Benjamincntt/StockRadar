namespace StockRadar.Application.Abstractions;

public interface IHyperparameterTuningService
{
    Task RunWeeklyAsync(CancellationToken cancellationToken = default);
}

public interface ITelegramNotifier
{
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
