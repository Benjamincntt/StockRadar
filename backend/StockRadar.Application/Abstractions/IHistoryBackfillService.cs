using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IHistoryBackfillService
{
    HistoryBackfillStatusDto GetStatus();

    Task<HistoryBackfillResultDto> RunAsync(
        HistoryBackfillRequest? request = null,
        CancellationToken cancellationToken = default);
}
