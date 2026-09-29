using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Kịch bản Quét thanh khoản (Liquidity Sweep — BUY):
/// Giá quét xuống dưới đáy nền (ăn stop-loss đám đông) rồi giành lại — Smart Money gom hàng.
///
/// Bối cảnh: uptrend (EMA20 > EMA50) hoặc sideways tích lũy (Bollinger hẹp), giá không rơi tự do.
/// Hình thái: tồn tại đáy nền rõ ràng + Low phiên gần nhất đã xuyên qua đáy nền.
/// Cò kích hoạt: Close giành lại trên đáy nền + volume xác nhận mạnh (bonus: trên VWAP).
/// Rủi ro: SL = đáy quét − 0.3×ATR, TP1 = đỉnh hộp 20 phiên.
///
/// Ghi chú phân lớp: interface <see cref="IKichBanDanhGia"/> và options nằm ở tầng
/// Application nên bộ đánh giá đặt tại Application/Services (Domain không tham chiếu Application).
/// </summary>
public class KichBanQuetThanhKhoan : IKichBanDanhGia
{
    /// <summary>Cửa sổ xác định hộp (đáy/đỉnh) 20 phiên.</summary>
    private const int HopLookback = 20;

    /// <summary>Số phiên gần nhất loại trừ khi tìm đáy nền (chính là vùng đang quét).</summary>
    private const int LoaiTruGanNhat = 5;

    /// <summary>Chu kỳ Bollinger đo trạng thái sideways tích lũy.</summary>
    private const int BollingerPeriod = 20;

    /// <summary>Ngưỡng Bollinger width để coi là sideways tích lũy.</summary>
    private const decimal BollingerSidewaysThreshold = 0.05m;

    /// <summary>Chu kỳ ATR dùng cho biên dừng lỗ.</summary>
    private const int AtrPeriod = 14;

    /// <summary>Số phiên tối thiểu để Bollinger/EMA50/MACD đủ tin cậy.</summary>
    private const int SoPhienToiThieu = 60;

    /// <summary>Biên ± tính từ giá hiện tại cho vùng vào lệnh (0.3%).</summary>
    private const decimal EntryBand = 0.003m;

    private readonly KichBanQuetThanhKhoanOptions _options;

    public LoaiKichBan LoaiKichBan => LoaiKichBan.QuetThanhKhoan;

    /// <summary>Đây là kịch bản BUY (không phải bán).</summary>
    public bool LaKichBanBan => false;

    public KichBanQuetThanhKhoan(KichBanQuetThanhKhoanOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Đánh giá bối cảnh — uptrend hoặc sideways tích lũy, giá không rơi tự do.
    /// Đạt khi (EMA20 > EMA50 HOẶC Bollinger width hẹp) VÀ Close > EMA50.
    /// </summary>
    public KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.BoiCanh);

        var close = history[^1].Close;
        var ema20 = IndicatorMath.Ema(history, 20);
        var ema50 = IndicatorMath.Ema(history, 50);
        var (upper, _, lower, _, _) = IndicatorMath.Bollinger(history, BollingerPeriod);
        var mid = (upper + lower) / 2m;
        var width = mid > 0 ? (upper - lower) / mid : 0m;

        var datUptrend = ema20 > ema50;
        var datSideways = width < BollingerSidewaysThreshold;
        var datKhongRoiTuDo = close > ema50;
        var datCauTruc = datUptrend || datSideways;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "Uptrend (EMA20 > EMA50) hoặc sideways tích lũy (Bollinger hẹp)",
                GiaTriThucTe = $"EMA20={ema20:F2}, EMA50={ema50:F2}, BBWidth={width:F4}",
                Nguong = $"EMA20 > EMA50 hoặc BBWidth < {BollingerSidewaysThreshold:F2}",
                Dat = datCauTruc
            },
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "Giá không rơi tự do (đóng cửa trên EMA50)",
                GiaTriThucTe = $"Close={close:F2}, EMA50={ema50:F2}",
                Nguong = "Close > EMA50",
                Dat = datKhongRoiTuDo
            }
        };

        return new KetQuaVaiTro(datCauTruc && datKhongRoiTuDo, bangChungs);
    }

    /// <summary>
    /// Đánh giá hình thái — có đáy nền rõ ràng và Low phiên gần nhất đã xuyên qua đáy nền.
    /// Đáy nền = min Low của 20 phiên trước (không gồm 5 phiên gần nhất — chính là vùng quét).
    /// </summary>
    public KetQuaVaiTro DanhGiaHinhThai(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.HinhThai);

        var dayNen = DayNen(history);
        var lowGanNhat = history[^1].Low;

        var datCoDayNen = dayNen > 0;
        var datXuyenDay = datCoDayNen && lowGanNhat < dayNen;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Tồn tại đáy nền rõ ràng (min Low 20 phiên trước, loại 5 phiên gần nhất)",
                GiaTriThucTe = $"Đáy nền={dayNen:F2}",
                Nguong = "Đáy nền > 0",
                Dat = datCoDayNen
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Low phiên gần nhất xuyên qua đáy nền (quét stop-loss)",
                GiaTriThucTe = $"Low={lowGanNhat:F2}, Đáy nền={dayNen:F2}",
                Nguong = "Low < Đáy nền",
                Dat = datXuyenDay
            }
        };

        return new KetQuaVaiTro(datCoDayNen && datXuyenDay, bangChungs);
    }

    /// <summary>
    /// Kiểm tra cò kích hoạt — giành lại trên đáy nền với volume mạnh.
    /// Điều kiện 1 (reclaim) + 2 (volume) BẮT BUỘC; điều kiện 3 (trên VWAP) là bonus.
    /// </summary>
    public KetQuaVaiTro KiemTraCoKichHoat(
        IReadOnlyList<OhlcvBar> history,
        decimal giaHienTai,
        long volumeHienTai)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.CoKichHoat);

        var dayNen = DayNen(history);
        var nguongReclaim = dayNen * (1 + _options.ReclaimMargin);
        var datReclaim = dayNen > 0 && giaHienTai > nguongReclaim;

        var avgVol20 = IndicatorMath.AverageVolume(history, HopLookback);
        var volumeRatio = avgVol20 > 0 ? volumeHienTai / avgVol20 : 0m;
        var datVolume = volumeRatio > _options.MinVolumeSpike;

        // Bonus: giá trên VWAP (nếu tính được) — đạt thì thêm bằng chứng, không bắt buộc.
        var vwap = IndicatorMath.Vwap(history);
        var datVwap = vwap > 0 && giaHienTai > vwap;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Giá giành lại trên đáy nền",
                GiaTriThucTe = $"Giá={giaHienTai:F2}, Ngưỡng={nguongReclaim:F2}",
                Nguong = $"Giá > Đáy nền × (1 + {_options.ReclaimMargin:P1})",
                Dat = datReclaim
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Volume xác nhận mạnh so với TB20",
                GiaTriThucTe = $"{volumeRatio:F2}×",
                Nguong = $"> {_options.MinVolumeSpike:F2}×",
                Dat = datVolume
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Giá trên VWAP (bonus — không bắt buộc)",
                GiaTriThucTe = $"Giá={giaHienTai:F2}, VWAP={vwap:F2}",
                Nguong = "Giá > VWAP",
                Dat = datVwap
            }
        };

        // Chỉ 2 điều kiện bắt buộc quyết định kết quả; VWAP là bonus.
        return new KetQuaVaiTro(datReclaim && datVolume, bangChungs);
    }

    /// <summary>
    /// Tính kế hoạch giao dịch (Entry/SL/TP1/TP2):
    /// Entry quanh giá hiện tại ±0.3%, SL dưới đáy quét trừ ATR, TP1 = đỉnh hộp 20 phiên.
    /// </summary>
    public KeHoachGiaoDich TinhKeHoach(IReadOnlyList<OhlcvBar> history, decimal giaHienTai)
    {
        var lowQuet = MinLow(history, LoaiTruGanNhat);
        var dinhHop = MaxHigh(history, HopLookback);
        var atr = IndicatorMath.Atr(history, AtrPeriod);

        var giaVaoLenhMin = giaHienTai * (1 - EntryBand);
        var giaVaoLenhMax = giaHienTai * (1 + EntryBand);
        var giaDungLo = lowQuet - 0.3m * atr;
        var giaChotLoi1 = dinhHop;
        var giaChotLoi2 = giaChotLoi1 + (giaChotLoi1 - giaVaoLenhMin) * 0.5m;

        return new KeHoachGiaoDich
        {
            GiaVaoLenhMin = giaVaoLenhMin,
            GiaVaoLenhMax = giaVaoLenhMax,
            GiaDungLo = giaDungLo,
            GiaChotLoi1 = giaChotLoi1,
            GiaChotLoi2 = giaChotLoi2,
            DieuKienHuy = $"Đóng cửa dưới {giaDungLo:F2} (đáy quét trừ ATR)"
        };
    }

    /// <summary>
    /// Đáy nền = min Low của 20 phiên trước, KHÔNG gồm 5 phiên gần nhất (vùng đang quét).
    /// Trả 0 khi không đủ dữ liệu.
    /// </summary>
    private static decimal DayNen(IReadOnlyList<OhlcvBar> history)
    {
        // endExclusive: chỉ số bắt đầu của 5 phiên gần nhất (loại trừ).
        var end = history.Count - LoaiTruGanNhat; // exclusive
        var start = Math.Max(0, end - HopLookback);
        if (end <= start)
            return 0m;

        var min = decimal.MaxValue;
        for (var i = start; i < end; i++)
            if (history[i].Low < min) min = history[i].Low;
        return min == decimal.MaxValue ? 0m : min;
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

    /// <summary>Max High của <paramref name="lookback"/> phiên gần nhất.</summary>
    private static decimal MaxHigh(IReadOnlyList<OhlcvBar> history, int lookback)
    {
        var start = Math.Max(0, history.Count - lookback);
        var max = decimal.MinValue;
        for (var i = start; i < history.Count; i++)
            if (history[i].High > max) max = history[i].High;
        return max == decimal.MinValue ? 0m : max;
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
