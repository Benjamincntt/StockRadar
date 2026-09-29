using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Kịch bản Nổ hướng lên (Breakout):
/// Cổ phiếu tích lũy trong hộp/nền giá, volume teo dần, rồi phá đỉnh với volume nổ.
///
/// Bối cảnh: EMA20 > EMA50, ADX > 25 (có xu hướng tăng rõ ràng).
/// Hình thái: Bollinger co hẹp + Volume teo &lt; 0.7× TB20 (đang nén lại).
/// Cò kích hoạt: Giá vượt đỉnh hộp + Volume > 1.5× TB20 + MACD histogram mở rộng.
/// Rủi ro: SL = đáy hộp − 0.5×ATR, TP1 = chiều cao hộp × 1.
///
/// Ghi chú phân lớp: interface <see cref="IKichBanDanhGia"/> và options nằm ở tầng
/// Application nên bộ đánh giá đặt tại Application/Services (Domain không tham chiếu Application).
/// </summary>
public class KichBanNoHuongLen : IKichBanDanhGia
{
    /// <summary>Số phiên dùng để xác định hộp/nền giá (đỉnh/đáy).</summary>
    private const int HopLookback = 20;

    /// <summary>Chu kỳ Bollinger đo độ co hẹp của nền giá.</summary>
    private const int BollingerPeriod = 20;

    /// <summary>Chu kỳ ATR dùng cho biên dừng lỗ / vùng vào lệnh.</summary>
    private const int AtrPeriod = 14;

    /// <summary>Chu kỳ ADX đo độ mạnh xu hướng.</summary>
    private const int AdxPeriod = 14;

    /// <summary>Số phiên tối thiểu để mọi chỉ báo (EMA50, ADX, Bollinger) đủ tin cậy.</summary>
    private const int SoPhienToiThieu = 60;

    private readonly KichBanNoHuongLenOptions _options;

    public LoaiKichBan LoaiKichBan => LoaiKichBan.NoHuongLen;

    /// <summary>Đây là kịch bản BUY (không phải bán).</summary>
    public bool LaKichBanBan => false;

    public KichBanNoHuongLen(KichBanNoHuongLenOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Đánh giá bối cảnh — xu hướng tăng rõ ràng: EMA20 > EMA50 VÀ ADX > ngưỡng.
    /// </summary>
    public KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.BoiCanh);

        var ema20 = IndicatorMath.Ema(history, 20);
        var ema50 = IndicatorMath.Ema(history, 50);
        var (adx, _, _) = IndicatorMath.Adx(history, AdxPeriod);

        var datEma = ema20 > ema50;
        var datAdx = adx > _options.MinAdx;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "EMA20 nằm trên EMA50 (xu hướng tăng)",
                GiaTriThucTe = $"EMA20={ema20:F2}, EMA50={ema50:F2}",
                Nguong = "EMA20 > EMA50",
                Dat = datEma
            },
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "ADX xác nhận xu hướng đủ mạnh",
                GiaTriThucTe = $"ADX={adx:F1}",
                Nguong = $"ADX > {_options.MinAdx:F0}",
                Dat = datAdx
            }
        };

        return new KetQuaVaiTro(datEma && datAdx, bangChungs);
    }

    /// <summary>
    /// Đánh giá hình thái — nền giá đang nén lại: Bollinger co hẹp VÀ volume teo.
    /// </summary>
    public KetQuaVaiTro DanhGiaHinhThai(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.HinhThai);

        var (upper, mid, lower, _, _) = IndicatorMath.Bollinger(history, BollingerPeriod);
        var width = mid > 0 ? (upper - lower) / mid : 0m;
        var datBollinger = width < _options.BollingerCompressionThreshold;

        var avgVol5 = IndicatorMath.AverageVolume(history, 5);
        var avgVol20 = IndicatorMath.AverageVolume(history, BollingerPeriod);
        var volumeRatio = avgVol20 > 0 ? avgVol5 / avgVol20 : 0m;
        var datVolumeTeo = volumeRatio < _options.MaxVolumeContraction;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Bollinger co hẹp (nền giá nén lại)",
                GiaTriThucTe = $"Width={width:F4}",
                Nguong = $"Width < {_options.BollingerCompressionThreshold:F4}",
                Dat = datBollinger
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Volume teo trong giai đoạn tích lũy (TB5 so với TB20)",
                GiaTriThucTe = $"TB5/TB20={volumeRatio:F2}",
                Nguong = $"< {_options.MaxVolumeContraction:F2}×",
                Dat = datVolumeTeo
            }
        };

        return new KetQuaVaiTro(datBollinger && datVolumeTeo, bangChungs);
    }

    /// <summary>
    /// Kiểm tra cò kích hoạt — breakout: giá vượt đỉnh hộp + volume nổ + MACD histogram mở rộng.
    /// </summary>
    public KetQuaVaiTro KiemTraCoKichHoat(
        IReadOnlyList<OhlcvBar> history,
        decimal giaHienTai,
        long volumeHienTai)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.CoKichHoat);

        // Đỉnh hộp = max High của 20 phiên gần nhất (không gồm phiên hiện tại — giaHienTai tách biệt).
        var dinhHop = MaxHigh(history, HopLookback);
        var datVuotDinh = giaHienTai > dinhHop;

        var avgVol20 = IndicatorMath.AverageVolume(history, BollingerPeriod);
        var volumeRatio = avgVol20 > 0 ? volumeHienTai / avgVol20 : 0m;
        var datVolumeNo = volumeRatio > _options.MinVolumeRatio;

        // MACD histogram phiên hiện tại > phiên trước (đà đang mở rộng).
        var (_, _, histHienTai) = IndicatorMath.Macd(history);
        var truocHienTai = history.Take(history.Count - 1).ToList();
        var (_, _, histTruoc) = IndicatorMath.Macd(truocHienTai);
        var datMacd = histHienTai > histTruoc;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Giá vượt đỉnh hộp 20 phiên",
                GiaTriThucTe = $"Giá={giaHienTai:F2}, Đỉnh hộp={dinhHop:F2}",
                Nguong = "Giá > Đỉnh hộp",
                Dat = datVuotDinh
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Volume nổ so với TB20",
                GiaTriThucTe = $"{volumeRatio:F2}×",
                Nguong = $"> {_options.MinVolumeRatio:F2}×",
                Dat = datVolumeNo
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "MACD histogram mở rộng",
                GiaTriThucTe = $"Hist={histHienTai:F4} (trước={histTruoc:F4})",
                Nguong = "Hist hiện tại > Hist phiên trước",
                Dat = datMacd
            }
        };

        return new KetQuaVaiTro(datVuotDinh && datVolumeNo && datMacd, bangChungs);
    }

    /// <summary>
    /// Tính kế hoạch giao dịch (Entry/SL/TP1/TP2) từ đáy/đỉnh hộp + ATR.
    /// </summary>
    public KeHoachGiaoDich TinhKeHoach(IReadOnlyList<OhlcvBar> history, decimal giaHienTai)
    {
        var dayHop = MinLow(history, HopLookback);
        var dinhHop = MaxHigh(history, HopLookback);
        var chieuCaoHop = dinhHop - dayHop;
        var atr = IndicatorMath.Atr(history, AtrPeriod);

        var giaVaoLenhMin = giaHienTai;
        var giaVaoLenhMax = giaHienTai + 0.3m * atr;
        var giaDungLo = dayHop - 0.5m * atr;
        var giaChotLoi1 = dinhHop + chieuCaoHop * 1.0m;
        var giaChotLoi2 = dinhHop + chieuCaoHop * 1.5m;

        return new KeHoachGiaoDich
        {
            GiaVaoLenhMin = giaVaoLenhMin,
            GiaVaoLenhMax = giaVaoLenhMax,
            GiaDungLo = giaDungLo,
            GiaChotLoi1 = giaChotLoi1,
            GiaChotLoi2 = giaChotLoi2,
            DieuKienHuy = $"Đóng cửa dưới {giaDungLo:F2} (đáy hộp trừ ATR)"
        };
    }

    /// <summary>Max High của <paramref name="lookback"/> phiên gần nhất.</summary>
    private static decimal MaxHigh(IReadOnlyList<OhlcvBar> history, int lookback)
    {
        var start = Math.Max(0, history.Count - lookback);
        var max = decimal.MinValue;
        for (var i = start; i < history.Count; i++)
            if (history[i].High > max) max = history[i].High;
        return max == decimal.MinValue ? 0m : max;
    }

    /// <summary>Min Low của <paramref name="lookback"/> phiên gần nhất.</summary>
    private static decimal MinLow(IReadOnlyList<OhlcvBar> history, int lookback)
    {
        var start = Math.Max(0, history.Count - lookback);
        var min = decimal.MaxValue;
        for (var i = start; i < history.Count; i++)
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
