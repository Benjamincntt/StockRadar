using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class HistoryBackfillState
{
    private readonly object _gate = new();
    private HistoryBackfillStatusDto _status = new(false, null, 0, 0, null);

    public HistoryBackfillStatusDto Get() => _status;

    public void SetRunning()
    {
        lock (_gate)
        {
            if (_status.IsRunning)
                throw new InvalidOperationException("Backfill đang chạy.");
            _status = new HistoryBackfillStatusDto(true, null, 0, 0, DateTime.UtcNow);
        }
    }

    public void Update(int processed, int total, string? symbol)
    {
        lock (_gate)
            _status = _status with { Processed = processed, Total = total, CurrentSymbol = symbol };
    }

    public void SetTotal(int total)
    {
        lock (_gate)
            _status = _status with { Total = total };
    }

    public DateTime? StartedAt
    {
        get
        {
            lock (_gate)
                return _status.StartedAt;
        }
    }

    public void Finish(int total)
    {
        lock (_gate)
            _status = new HistoryBackfillStatusDto(false, null, total, total, _status.StartedAt);
    }

    public void CancelRunning()
    {
        lock (_gate)
            _status = _status with { IsRunning = false, CurrentSymbol = null };
    }
}
