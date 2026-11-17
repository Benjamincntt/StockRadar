using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Chặn gửi lặp của giỏ Mùa Tết: mỗi (ngày Việt Nam, mức báo) chỉ gửi MỘT lần. Đợt 2a không có bảng nên
/// trạng thái chỉ nằm trong bộ nhớ — API khởi động lại giữa ngày có thể gửi lại một lần, chấp nhận được.
/// </summary>
internal sealed class MuaTetChanLap
{
    private readonly Dictionary<(DateOnly Ngay, MuaTetTheoDoi.MucBao Muc), byte> daGui = [];
    private readonly object cong = new();

    /// <summary>Đã gửi mức này trong ngày chưa.</summary>
    /// <param name="ngay">Ngày theo giờ Việt Nam.</param>
    /// <param name="muc">Mức báo cần kiểm.</param>
    public bool DaGui(DateOnly ngay, MuaTetTheoDoi.MucBao muc)
    {
        lock (cong)
            return daGui.ContainsKey((ngay, muc));
    }

    /// <summary>Ghi nhận đã gửi mức này trong ngày (mức khác vẫn được gửi riêng).</summary>
    /// <param name="ngay">Ngày theo giờ Việt Nam.</param>
    /// <param name="muc">Mức báo vừa gửi.</param>
    public void DanhDau(DateOnly ngay, MuaTetTheoDoi.MucBao muc)
    {
        lock (cong)
        {
            // LIMIT: không có bảng trạng thái nên phải tự gọn — chỉ giữ hôm nay, tránh phình bộ nhớ qua nhiều mùa.
            foreach (var khoa in daGui.Keys.Where(k => k.Ngay < ngay).ToArray())
                daGui.Remove(khoa);
            daGui[(ngay, muc)] = 1;
        }
    }
}
