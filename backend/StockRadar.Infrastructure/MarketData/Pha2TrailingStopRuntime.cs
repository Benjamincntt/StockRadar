using System.Collections.Concurrent;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Trạng thái in-memory của Pha 2 phục vụ dừng lỗ đuổi theo: cache ATR theo mã/phiên
/// và bộ đếm lượt xác nhận chống nhiễu. Restart API thì mất, chấp nhận được.
/// </summary>
internal sealed class Pha2TrailingStopRuntime
{
    private sealed record AtrSlot(DateOnly Ngay, decimal GiaTri);
    private sealed record ConfirmSlot(DateOnly Ngay, int SoLuot);

    private readonly ConcurrentDictionary<string, AtrSlot> _atrCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConfirmSlot> _confirmCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lấy ATR đã cache cho (mã, phiên); nếu chưa có thì tính qua <paramref name="factory"/> rồi cache lại.</summary>
    public decimal GetOrComputeAtr(string symbol, DateOnly ngay, Func<decimal> factory)
    {
        var slot = _atrCache.GetOrAdd(symbol, _ => new AtrSlot(ngay, factory()));
        if (slot.Ngay == ngay)
            return slot.GiaTri;

        var fresh = new AtrSlot(ngay, factory());
        _atrCache[symbol] = fresh;
        return fresh.GiaTri;
    }

    /// <summary>Tăng bộ đếm lượt xác nhận liên tiếp (giá dưới dừng lỗ) cho (mã, phiên), trả về giá trị mới.</summary>
    public int IncrementConfirmation(string symbol, DateOnly ngay)
    {
        var slot = _confirmCache.AddOrUpdate(
            symbol,
            _ => new ConfirmSlot(ngay, 1),
            (_, existing) => existing.Ngay == ngay
                ? existing with { SoLuot = existing.SoLuot + 1 }
                : new ConfirmSlot(ngay, 1));
        return slot.SoLuot;
    }

    /// <summary>Reset bộ đếm xác nhận khi giá hồi lên trên dừng lỗ (mất chuỗi liên tiếp).</summary>
    public void ResetConfirmation(string symbol) =>
        _confirmCache.TryRemove(symbol, out _);

    /// <summary>Xoá toàn bộ trạng thái in-memory — dùng trong test để cách ly.</summary>
    public void ResetForTests()
    {
        _atrCache.Clear();
        _confirmCache.Clear();
    }
}
