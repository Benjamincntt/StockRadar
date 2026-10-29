namespace StockRadar.Tests.Notifications;

/// <summary>Controls deadlines without sleeping; callbacks run outside the clock lock.</summary>
internal sealed class DongHoGia : TimeProvider
{
    private readonly object khoa = new();
    private readonly List<HenGio> cacHen = [];
    private long thoiDiem;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (khoa) return thoiDiem; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var hen = new HenGio(this, callback, state);
        hen.Change(dueTime, period);
        return hen;
    }

    /// <summary>Advances to each due callback, then to the requested time.</summary>
    internal void Tien(TimeSpan khoang)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(khoang, TimeSpan.Zero);
        var dich = checked(GetTimestamp() + khoang.Ticks);
        while (true)
        {
            HenGio? hen;
            lock (khoa)
            {
                hen = cacHen.Where(muc => !muc.DaHuy && muc.LucChay <= dich)
                    .OrderBy(muc => muc.LucChay).FirstOrDefault();
                if (hen is null) { thoiDiem = dich; return; }
                thoiDiem = hen.LucChay;
                hen.LucChay = hen.ChuKy > 0 ? checked(thoiDiem + hen.ChuKy) : long.MaxValue;
            }
            hen.Chay();
        }
    }

    private sealed class HenGio(DongHoGia dongHo, TimerCallback goi, object? trangThai) : ITimer
    {
        internal bool DaHuy { get; private set; }
        internal long LucChay { get; set; } = long.MaxValue;
        internal long ChuKy { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime < Timeout.InfiniteTimeSpan || period < Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            lock (dongHo.khoa)
            {
                if (DaHuy) return false;
                if (!dongHo.cacHen.Contains(this)) dongHo.cacHen.Add(this);
                LucChay = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(dongHo.thoiDiem + dueTime.Ticks);
                ChuKy = period.Ticks;
                return true;
            }
        }

        internal void Chay() => goi(trangThai);
        public void Dispose()
        {
            lock (dongHo.khoa)
            {
                DaHuy = true;
                dongHo.cacHen.Remove(this);
            }
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
