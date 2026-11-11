namespace StockRadar.Application.Abstractions;

public interface ITelegramNotifier
{
    Task SendAsync(string message, CancellationToken cancellationToken = default, string? parseMode = null);
}
