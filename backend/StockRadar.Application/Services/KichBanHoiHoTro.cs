using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Kịch bản Hồi về hỗ trợ (Pullback — BUY):
/// Xu hướng tăng mạnh, giá tạm lùi về EMA20/VWAP rồi bật lại.
///
/// Bối cảnh: EMA20 > EMA50 (bắt buộc). EMA200 chỉ là bằng chứng bonus khi có đủ ≥ 200 phiên
/// — nhiều mã VN không đủ 200 phiên hoặc EMA200 không meaningful cho mid-cap nên không được veto.
/// Hình thái: Giá gần EMA20 (±3%) + RSI 40–50 (đã điều chỉnh, chưa quá bán) + volume bán teo.
/// Cò kích hoạt: RSI bật lên + MACD histogram tăng lại + giá reclaim trên EMA20.
/// Rủi ro: SL = EMA50 − 0.5×ATR, TP1 = đỉnh 20 phiên (đỉnh cũ trước pullback).
///
/// Ghi chú phân lớp: interface <see cref="IKichBanDanhGia"/> và options nằm ở tầng
/// Application nên bộ đánh giá đặt tại Application/Services (Domain không tham chiếu Application).
/// </summary>
public class KichBanHoiHoTro : IKichBanDanhGia
{
    /// <summary>Số phiên dùng để xác định đỉnh cũ trước pullback (TP1).</summary>
    private const int LookbackDinh = 20;

    /// <summary>Chu kỳ ATR dùng cho biên dừng lỗ.</summary>
    private const int AtrPeriod = 14;

    /// <summary>Chu kỳ RSI đo quán tính điều chỉnh.</summary>
    private const int RsiPeriod = 14;

    /// <summary>Số phiên tối thiểu để EMA50 và các chỉ báo đủ tin cậy.</summary>
    private const int SoPhienToiThieu = 60;

    /// <summary>Số phiên tối thiểu để EMA200 được coi là có ý nghĩa; thiếu thì bỏ qua hoàn toàn.</summary>
    private const int Ema200Period = 200;

    /// <summary>Volume TB3 phải dưới 0.8× TB20 để coi là "bán thấp, không phải bán tháo".</summary>
    private const decimal MaxVolumePullbackRatio = 0.8m;

    /// <summary>Biên ± tính từ EMA20 cho vùng vào lệnh (0.5%).</summary>
    private const decimal EntryBand = 0.005m;

    private readonly KichBanHoiHoTroOptions _options;

    public LoaiKichBan LoaiKichBan => LoaiKichBan.HoiHoTro;

    /// <summary>Đây là kịch bản BUY (không phải bán).</summary>
    public bool LaKichBanBan => false;

    public KichBanHoiHoTro(KichBanHoiHoTroOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Đánh giá bối cảnh — điều kiện BẮT BUỘC duy nhất: EMA20 > EMA50 (xu hướng tăng ngắn/trung hạn).
    ///
    /// EMA200 chỉ là bằng chứng bonus: khi có đủ ≥ 200 phiên và EMA50 > EMA200 thì ghi nhận thêm
    /// "Xu hướng dài hạn xác nhận ✓", nhưng KHÔNG ảnh hưởng kết quả <see cref="KetQuaVaiTro.Dat"/>.
    /// Lý do nới lỏng: nhiều mã VN không đủ 200 phiên (niêm yết mới) hoặc EMA200 không meaningful
    /// cho mid-cap, khiến gate dài hạn loại nhầm các pullback hợp lệ trong uptrend đã xác nhận.
    /// Nếu history &lt; 200 phiên → bỏ qua EMA200 hoàn toàn, không fail.
    /// </summary>
    public KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.BoiCanh);

        var ema20 = IndicatorMath.Ema(history, 20);
        var ema50 = IndicatorMath.Ema(history, 50);
        var datEma = ema20 > ema50;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "EMA20 nằm trên EMA50 (xu hướng tăng ngắn hạn)",
                GiaTriThucTe = $"EMA20={ema20:F2}, EMA50={ema50:F2}",
                Nguong = "EMA20 > EMA50",
                Dat = datEma
            }
        };

        // Bằng chứng bonus — chỉ thêm khi đủ lịch sử, không tham gia vào điều kiện Đạt.
        if (history.Count >= Ema200Period)
        {
            var ema200 = IndicatorMath.Ema(history, Ema200Period);
            var datDaiHan = ema50 > ema200;
            bangChungs.Add(new BangChung
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = datDaiHan
                    ? "Xu hướng dài hạn xác nhận ✓ (bằng chứng bonus — không ảnh hưởng kết quả)"
                    : "EMA50 dưới EMA200 (bằng chứng bonus — không ảnh hưởng kết quả)",
                GiaTriThucTe = $"EMA50={ema50:F2}, EMA200={ema200:F2}",
                Nguong = "Điểm cộng: EMA50 > EMA200",
                Dat = datDaiHan
            });
        }

        return new KetQuaVaiTro(datEma, bangChungs);
    }

    /// <summary>
    /// Đánh giá hình thái — pullback lành mạnh về hỗ trợ:
    /// giá gần EMA20 + RSI vùng điều chỉnh + volume bán teo (không phải bán tháo).
    /// </summary>
    public KetQuaVaiTro DanhGiaHinhThai(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.HinhThai);

        var close = history[^1].Close;
        var ema20 = IndicatorMath.Ema(history, 20);
        var distance = ema20 > 0 ? Math.Abs(close - ema20) / ema20 : decimal.MaxValue;
        var datGanHoTro = distance <= _options.MaxEma20Distance;

        var rsi = IndicatorMath.Rsi(history, RsiPeriod);
        var datRsi = rsi >= _options.RsiPullbackMin && rsi <= _options.RsiPullbackMax;

        // Volume TB 3 phiên gần nhất so với TB20 — bán thấp nghĩa là điều chỉnh kỹ thuật.
        var avgVol3 = IndicatorMath.AverageVolume(history, 3);
        var avgVol20 = IndicatorMath.AverageVolume(history, LookbackDinh);
        var volumeRatio = avgVol20 > 0 ? avgVol3 / avgVol20 : 0m;
        var datVolumeBanThap = volumeRatio < MaxVolumePullbackRatio;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Giá đóng cửa đang gần hỗ trợ EMA20",
                GiaTriThucTe = $"Close={close:F2}, EMA20={ema20:F2}, cách={distance:P1}",
                Nguong = $"≤ {_options.MaxEma20Distance:P0} EMA20",
                Dat = datGanHoTro
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "RSI vùng điều chỉnh (đã lùi, chưa quá bán)",
                GiaTriThucTe = $"RSI={rsi:F1}",
                Nguong = $"[{_options.RsiPullbackMin:F0}, {_options.RsiPullbackMax:F0}]",
                Dat = datRsi
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Volume bán thấp (TB3 so với TB20) — không phải bán tháo",
                GiaTriThucTe = $"TB3/TB20={volumeRatio:F2}",
                Nguong = $"< {MaxVolumePullbackRatio:F1}×",
                Dat = datVolumeBanThap
            }
        };

        return new KetQuaVaiTro(datGanHoTro && datRsi && datVolumeBanThap, bangChungs);
    }

    /// <summary>
    /// Kiểm tra cò kích hoạt — pullback kết thúc, giá bật lại:
    /// RSI quay đầu tăng + MACD histogram tăng lại + giá reclaim trên EMA20.
    /// </summary>
    public KetQuaVaiTro KiemTraCoKichHoat(
        IReadOnlyList<OhlcvBar> history,
        decimal giaHienTai,
        long volumeHienTai)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.CoKichHoat);

        var truocHienTai = history.Take(history.Count - 1).ToList();

        // RSI bật lên so với phiên trước.
        var rsiHienTai = IndicatorMath.Rsi(history, RsiPeriod);
        var rsiTruoc = IndicatorMath.Rsi(truocHienTai, RsiPeriod);
        var datRsiBatLen = rsiHienTai > rsiTruoc;

        // MACD histogram giảm rồi tăng lại.
        var (_, _, histHienTai) = IndicatorMath.Macd(history);
        var (_, _, histTruoc) = IndicatorMath.Macd(truocHienTai);
        var datMacd = histHienTai > histTruoc;

        // Giá đã reclaim trên EMA20.
        var ema20 = IndicatorMath.Ema(history, 20);
        var datReclaim = giaHienTai > ema20;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "RSI bật lên so với phiên trước",
                GiaTriThucTe = $"RSI={rsiHienTai:F1} (trước={rsiTruoc:F1})",
                Nguong = "RSI hiện tại > RSI phiên trước",
                Dat = datRsiBatLen
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "MACD histogram tăng lại (giảm rồi hồi)",
                GiaTriThucTe = $"Hist={histHienTai:F4} (trước={histTruoc:F4})",
                Nguong = "Hist hiện tại > Hist phiên trước",
                Dat = datMacd
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Giá reclaim trên EMA20",
                GiaTriThucTe = $"Giá={giaHienTai:F2}, EMA20={ema20:F2}",
                Nguong = "Giá > EMA20",
                Dat = datReclaim
            }
        };

        return new KetQuaVaiTro(datRsiBatLen && datMacd && datReclaim, bangChungs);
    }

    /// <summary>
    /// Tính kế hoạch giao dịch (Entry/SL/TP1/TP2):
    /// Entry quanh EMA20 ±0.5%, SL dưới EMA50 trừ nửa ATR, TP1 = đỉnh cũ trước pullback.
    /// </summary>
    public KeHoachGiaoDich TinhKeHoach(IReadOnlyList<OhlcvBar> history, decimal giaHienTai)
    {
        var ema20 = IndicatorMath.Ema(history, 20);
        var ema50 = IndicatorMath.Ema(history, 50);
        var atr = IndicatorMath.Atr(history, AtrPeriod);
        var dinh20 = MaxHigh(history, LookbackDinh);

        var giaVaoLenhMin = ema20 * (1 - EntryBand);
        var giaVaoLenhMax = ema20 * (1 + EntryBand);
        var giaDungLo = ema50 - 0.5m * atr;
        var giaChotLoi1 = dinh20;
        var giaChotLoi2 = giaChotLoi1 + (giaChotLoi1 - giaVaoLenhMin) * 0.5m;

        return new KeHoachGiaoDich
        {
            GiaVaoLenhMin = giaVaoLenhMin,
            GiaVaoLenhMax = giaVaoLenhMax,
            GiaDungLo = giaDungLo,
            GiaChotLoi1 = giaChotLoi1,
            GiaChotLoi2 = giaChotLoi2,
            DieuKienHuy = "Đóng cửa dưới EMA50 2 phiên liên tiếp"
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
