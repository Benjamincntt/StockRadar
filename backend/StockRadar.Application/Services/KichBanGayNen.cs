using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Kịch bản Gãy nền (Breakdown — EXIT/SELL):
/// Giá phá vỡ đáy nền, mất hỗ trợ, momentum xấu → BÁN 100% (thoát vị thế).
///
/// Bối cảnh: luôn đạt nếu đang giữ vị thế (kiểm tra ở caller) — chỉ ghi nhận bằng chứng.
/// Hình thái: có đáy nền VÀ giá mất ít nhất 1 hỗ trợ quan trọng (đáy nền / EMA20 / VWAP).
/// Cò kích hoạt: volume bán mạnh + MACD âm liên tiếp + đóng cửa dưới đáy nền (gãy thật).
/// Kế hoạch: SELL nên Entry/SL/TP = 0; gãy nền là thoát — KHÔNG có điều kiện hủy.
///
/// Ghi chú phân lớp: interface <see cref="IKichBanDanhGia"/> và options nằm ở tầng
/// Application nên bộ đánh giá đặt tại Application/Services (Domain không tham chiếu Application).
/// </summary>
public class KichBanGayNen : IKichBanDanhGia
{
    /// <summary>Cửa sổ xác định đáy nền 20 phiên.</summary>
    private const int DayNenLookback = 20;

    /// <summary>Chu kỳ EMA20 làm hỗ trợ động.</summary>
    private const int Ema20Period = 20;

    /// <summary>Số phiên tối thiểu để EMA20/VWAP/MACD đủ tin cậy.</summary>
    private const int SoPhienToiThieu = 60;

    private readonly KichBanGayNenOptions _options;

    public LoaiKichBan LoaiKichBan => LoaiKichBan.GayNen;

    /// <summary>Đây là kịch bản BÁN (thoát vị thế).</summary>
    public bool LaKichBanBan => true;

    public KichBanGayNen(KichBanGayNenOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Đánh giá bối cảnh — gãy nền áp dụng khi đang giữ vị thế (kiểm tra ở caller).
    /// Ở đây luôn đạt với bằng chứng ghi nhận trạng thái.
    /// </summary>
    public KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history)
    {
        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "Đang giữ vị thế (điều kiện tiên quyết kiểm tra ở caller)",
                GiaTriThucTe = history.Count > 0 ? $"Close={history[^1].Close:F2}" : "không có dữ liệu",
                Nguong = "Có vị thế",
                Dat = true
            }
        };

        return new KetQuaVaiTro(true, bangChungs);
    }

    /// <summary>
    /// Đánh giá hình thái — có đáy nền VÀ giá mất ít nhất 1 hỗ trợ quan trọng
    /// (đáy nền / EMA20 / VWAP). Chỉ cần mất 1 hỗ trợ là đủ lo ngại.
    /// </summary>
    public KetQuaVaiTro DanhGiaHinhThai(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.HinhThai);

        var close = history[^1].Close;
        var dayNen = DayNen(history);
        var ema20 = IndicatorMath.Ema(history, Ema20Period);
        var vwap = IndicatorMath.Vwap(history);

        var datCoDayNen = dayNen > 0;
        var matDayNen = datCoDayNen && close < dayNen;
        var matEma20 = close < ema20;
        var matVwap = vwap > 0 && close < vwap;
        var matHoTro = matDayNen || matEma20 || matVwap;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Tồn tại đáy nền 20 phiên",
                GiaTriThucTe = $"Đáy nền={dayNen:F2}",
                Nguong = "Đáy nền > 0",
                Dat = datCoDayNen
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Mất ít nhất 1 hỗ trợ quan trọng (đáy nền / EMA20 / VWAP)",
                GiaTriThucTe = $"Close={close:F2}, Đáy nền={dayNen:F2}, EMA20={ema20:F2}, VWAP={vwap:F2}",
                Nguong = "Close < 1 trong 3 hỗ trợ",
                Dat = matHoTro
            }
        };

        return new KetQuaVaiTro(datCoDayNen && matHoTro, bangChungs);
    }

    /// <summary>
    /// Kiểm tra cò kích hoạt — gãy thật (không phải quét): volume bán mạnh +
    /// MACD histogram âm liên tiếp + đóng cửa dưới đáy nền. Cả 3 phải đạt.
    /// </summary>
    public KetQuaVaiTro KiemTraCoKichHoat(
        IReadOnlyList<OhlcvBar> history,
        decimal giaHienTai,
        long volumeHienTai)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.CoKichHoat);

        var avgVol20 = IndicatorMath.AverageVolume(history, DayNenLookback);
        var volumeRatio = avgVol20 > 0 ? volumeHienTai / avgVol20 : 0m;
        var datVolume = volumeRatio > _options.MinSellVolumeRatio;

        var soPhienMacdAm = DemMacdAmLienTiep(history);
        var datMacd = soPhienMacdAm >= _options.MinMacdNegativeDays;

        var dayNen = DayNen(history);
        var datGayThat = dayNen > 0 && giaHienTai < dayNen;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Volume bán mạnh so với TB20",
                GiaTriThucTe = $"{volumeRatio:F2}×",
                Nguong = $"> {_options.MinSellVolumeRatio:F1}×",
                Dat = datVolume
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "MACD histogram âm liên tiếp (momentum xấu)",
                GiaTriThucTe = $"{soPhienMacdAm} phiên",
                Nguong = $"≥ {_options.MinMacdNegativeDays} phiên",
                Dat = datMacd
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Đóng cửa dưới đáy nền (gãy thật, không phải quét)",
                GiaTriThucTe = $"Giá={giaHienTai:F2}, Đáy nền={dayNen:F2}",
                Nguong = "Giá < Đáy nền",
                Dat = datGayThat
            }
        };

        return new KetQuaVaiTro(datVolume && datMacd && datGayThat, bangChungs);
    }

    /// <summary>
    /// Tính kế hoạch — đây là SELL/EXIT nên Entry/SL/TP = 0 (không áp dụng).
    /// Gãy nền là thoát — KHÔNG có điều kiện hủy.
    /// </summary>
    public KeHoachGiaoDich TinhKeHoach(IReadOnlyList<OhlcvBar> history, decimal giaHienTai)
    {
        return new KeHoachGiaoDich
        {
            GiaVaoLenhMin = 0m,
            GiaVaoLenhMax = 0m,
            GiaDungLo = 0m,
            GiaChotLoi1 = 0m,
            GiaChotLoi2 = 0m,
            DieuKienHuy = "KHÔNG CÓ — gãy nền là thoát"
        };
    }

    /// <summary>
    /// Đếm số phiên MACD histogram âm liên tiếp tính từ phiên gần nhất lùi về trước.
    /// </summary>
    private static int DemMacdAmLienTiep(IReadOnlyList<OhlcvBar> history)
    {
        var count = 0;
        for (var i = history.Count; i >= 1; i--)
        {
            var slice = history.Take(i).ToList();
            var (_, _, hist) = IndicatorMath.Macd(slice);
            if (hist < 0) count++;
            else break;
        }
        return count;
    }

    /// <summary>
    /// Đáy nền = min Low của <see cref="DayNenLookback"/> phiên TRƯỚC (không gồm phiên hiện tại).
    /// Loại phiên hiện tại để "giá đóng cửa &lt; đáy nền" có nghĩa (gãy hỗ trợ thật).
    /// </summary>
    private static decimal DayNen(IReadOnlyList<OhlcvBar> history)
    {
        var end = history.Count - 1; // exclusive — bỏ phiên hiện tại
        var start = Math.Max(0, end - DayNenLookback);
        if (end <= start)
            return 0m;

        var min = decimal.MaxValue;
        for (var i = start; i < end; i++)
            if (history[i].Low < min) min = history[i].Low;
        return min == decimal.MaxValue ? 0m : min;
    }

    /// <summary>Kết quả "không đạt" khi thiếu dữ liệu lịch sử tối thiểu.</summary>
    private static KetQuaVaiTro KhongDuLieu(VaiTroChiBao vaiTro) =>
        new(false, new List<BangChung>
        {
            new()
            {
                VaiTro = vaiTro,
                MoTa = "Không đủ lịch sử tối thiểu để đánh giá",
                GiaTriThucTe = "< 60 phiên",
                Nguong = "≥ 60 phiên",
                Dat = false
            }
        });
}
