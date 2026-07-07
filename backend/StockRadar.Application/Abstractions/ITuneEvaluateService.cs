using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface ITuneEvaluateService
{
    Task<TuneEvaluateResponse> EvaluateAsync(
        TuneEvaluateRequest request,
        CancellationToken cancellationToken = default);
}
