using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Tests.MarketData;

/// <summary>
/// Spec 018 Q1 — biên khung giờ lấy bảng giá KBS 08:59–11:31 và 12:59–15:10 (tính cả đầu mút),
/// chỉ ngày giao dịch. Ngoài khung thì KBS sync bỏ qua, không ghi nến giả ban đêm.
/// </summary>
public sealed class VietnamMarketCalendarQuoteSyncWindowTests
{
    // 2026-11-12 là thứ Năm, không nghỉ trong TradingSessionMath.VietnamHolidays.
    private static readonly DateOnly NgayGiaoDich = new(2026, 11, 12);

    [Theory]
    [InlineData(8, 58, 59, false)]   // ngay trước giờ mở khung sáng
    [InlineData(8, 59, 0, true)]     // đầu mút dưới sáng (Q1)
    [InlineData(11, 31, 0, true)]    // đầu mút trên sáng (Q1)
    [InlineData(11, 31, 1, false)]   // vừa hết khung sáng
    [InlineData(12, 0, 0, false)]    // giữa nghỉ trưa, không có giá mới
    [InlineData(12, 58, 59, false)]  // ngay trước khung chiều
    [InlineData(12, 59, 0, true)]    // đầu mút dưới chiều (Q1)
    [InlineData(15, 10, 0, true)]    // đầu mút trên chiều — bảng giá đã có ATC
    [InlineData(15, 10, 1, false)]   // vừa hết khung chiều
    [InlineData(3, 0, 0, false)]     // ban đêm — nguồn gốc nến giả
    public void NgayGiaoDich_KiemTraBienKhung(int hour, int minute, int second, bool expected)
    {
        var now = new DateTime(NgayGiaoDich.Year, NgayGiaoDich.Month, NgayGiaoDich.Day, hour, minute, second);

        Assert.Equal(expected, VietnamMarketCalendar.IsInQuoteSyncWindow(now));
    }

    [Fact]
    public void ThuBay_NgayNghiKhongPhaiLe_NgoaiKhung()
    {
        // 2026-11-14 là thứ Bảy.
        var now = new DateTime(2026, 11, 14, 10, 0, 0);

        Assert.False(VietnamMarketCalendar.IsInQuoteSyncWindow(now));
    }

    [Fact]
    public void ChuNhat_NgayNghiKhongPhaiLe_NgoaiKhung()
    {
        var now = new DateTime(2026, 11, 15, 13, 30, 0);

        Assert.False(VietnamMarketCalendar.IsInQuoteSyncWindow(now));
    }

    [Fact]
    public void NghiLe_TrongLichCuaIsTradingDay_VanNgoaiKhung()
    {
        // 31/08/2026 là thứ Hai nhưng có trong VietnamHolidays.
        var ngayLe = new DateOnly(2026, 8, 31);
        Assert.False(StockRadar.Domain.Services.TradingSessionMath.IsTradingDay(ngayLe));

        var now = new DateTime(2026, 8, 31, 10, 0, 0);
        Assert.False(VietnamMarketCalendar.IsInQuoteSyncWindow(now));
    }
}
