using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Kịch bản Kiệt sức (Exhaustion — SELL):
/// Giá đã tăng nóng, chạy quá xa EMA20, hết đà → BÁN 50% (hành động xử lý ở publisher).
///
/// Bối cảnh: đã lãi > 8% so với SMA20 của 10 phiên trước + giãn > 2×ATR so với EMA20.
/// Hình thái: RSI quá mua / volume climax / MACD histogram giảm (đạt ≥ 2/3).
/// Cò kích hoạt: phân kỳ âm HOẶC giá quay đầu dưới Bollinger Upper (đạt ≥ 1/2).
/// Kế hoạch: SELL nên Entry/SL/TP = 0 (không áp dụng); điều kiện hủy = tín hiệu giữ lại.
///
/// Ghi chú phân lớp: interface <see cref="IKichBanDanhGia"/> và options nằm ở tầng
/// Application nên bộ đánh giá đặt tại Application/Services (Domain không tham chiếu Application).
/// </summary>
public class KichBanKietSuc : IKichBanDanhGia
{
    /// <summary>Chu kỳ RSI đo trạng thái quá mua.</summary>
    private const int RsiPeriod = 14;

    /// <summary>Chu kỳ ATR đo độ giãn so với EMA20.</summary>
    private const int AtrPeriod = 14;

    /// <summary>Chu kỳ Bollinger đo biên trên.</summary>
    private const int BollingerPeriod = 20;

    /// <summary>Số phiên "giá vào lệnh ước tính" — SMA20 tại thời điểm 10 phiên trước.</summary>
    private const int EntryProxyOffset = 10;

    /// <summary>Chu kỳ SMA làm proxy giá entry (SMA20).</summary>
    private const int EntryProxyPeriod = 20;

    /// <summary>Số phiên so sánh đỉnh/RSI để phát hiện phân kỳ âm.</summary>
    private const int PhanKyLookback = 5;

    /// <summary>Số phiên tối thiểu để SMA20 tại offset 10 + các chỉ báo đủ tin cậy.</summary>
    private const int SoPhienToiThieu = 60;

    private readonly KichBanKietSucOptions _options;

    public LoaiKichBan LoaiKichBan => LoaiKichBan.KietSuc;

    /// <summary>Đây là kịch bản BÁN.</summary>
    public bool LaKichBanBan => true;

    public KichBanKietSuc(KichBanKietSucOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Đánh giá bối cảnh — đã tăng nóng: lãi > ngưỡng so với SMA20 của 10 phiên trước
    /// VÀ giá giãn > MinExtensionAtr × ATR so với EMA20.
    /// </summary>
    public KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history) =>
        DanhGiaBoiCanh(history, giaVaoLenhThucTe: null);

    /// <summary>
    /// Overload cho máy nhận kịch bản bán — khi có <paramref name="giaVaoLenhThucTe"/> (giá vào lệnh
    /// thực tế của vị thế) thì tính % gain từ đó; ngược lại fallback proxy SMA20 của 10 phiên trước.
    /// </summary>
    public KetQuaVaiTro DanhGiaBoiCanh(IReadOnlyList<OhlcvBar> history, decimal? giaVaoLenhThucTe)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.BoiCanh);

        var close = history[^1].Close;

        // Giá entry: thực tế (nếu có) hoặc proxy SMA20 tại thời điểm 10 phiên trước.
        decimal giaEntry;
        string tenGiaEntry;
        if (giaVaoLenhThucTe is > 0)
        {
            giaEntry = giaVaoLenhThucTe.Value;
            tenGiaEntry = "Giá vào lệnh";
        }
        else
        {
            var entryIndex = history.Count - 1 - EntryProxyOffset;
            giaEntry = IndicatorMath.SmaAt(history, entryIndex, EntryProxyPeriod);
            tenGiaEntry = "SMA20(-10)";
        }
        var gain = giaEntry > 0 ? (close - giaEntry) / giaEntry : 0m;
        var datGain = gain > _options.MinGainFromEntry;

        // Độ giãn so với EMA20 tính theo ATR.
        var ema20 = IndicatorMath.Ema(history, 20);
        var atr = IndicatorMath.Atr(history, AtrPeriod);
        var extensionAtr = atr > 0 ? (close - ema20) / atr : 0m;
        var datGian = extensionAtr > _options.MinExtensionAtr;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = $"Đã tăng nóng so với {tenGiaEntry.ToLowerInvariant()}",
                GiaTriThucTe = $"Close={close:F2}, {tenGiaEntry}={giaEntry:F2}, lãi={gain:P1}",
                Nguong = $"> {_options.MinGainFromEntry:P0}",
                Dat = datGain
            },
            new()
            {
                VaiTro = VaiTroChiBao.BoiCanh,
                MoTa = "Giá đã chạy quá xa EMA20 (giãn theo ATR)",
                GiaTriThucTe = $"Close={close:F2}, EMA20={ema20:F2}, giãn={extensionAtr:F2}×ATR",
                Nguong = $"> {_options.MinExtensionAtr:F1}×ATR",
                Dat = datGian
            }
        };

        return new KetQuaVaiTro(datGain && datGian, bangChungs);
    }

    /// <summary>
    /// Đánh giá hình thái — dấu hiệu phân phối: RSI quá mua / volume climax / MACD histogram giảm.
    /// Linh hoạt: đạt khi ≥ 2/3 điều kiện (sell signal cần nhạy).
    /// </summary>
    public KetQuaVaiTro DanhGiaHinhThai(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.HinhThai);

        var truocHienTai = history.Take(history.Count - 1).ToList();

        var rsi = IndicatorMath.Rsi(history, RsiPeriod);
        var datRsi = rsi > _options.MinRsi;

        var volHienTai = history[^1].Volume;
        var avgVol20 = IndicatorMath.AverageVolume(history, BollingerPeriod);
        var volumeRatio = avgVol20 > 0 ? volHienTai / avgVol20 : 0m;
        var datVolumeClimax = volumeRatio > _options.MinVolumeClimax;

        var (_, _, histHienTai) = IndicatorMath.Macd(history);
        var (_, _, histTruoc) = IndicatorMath.Macd(truocHienTai);
        var datMacdGiam = histHienTai < histTruoc;

        var soDieuKienDat = (datRsi ? 1 : 0) + (datVolumeClimax ? 1 : 0) + (datMacdGiam ? 1 : 0);

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "RSI quá mua",
                GiaTriThucTe = $"RSI={rsi:F1}",
                Nguong = $"> {_options.MinRsi:F0}",
                Dat = datRsi
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "Volume đạt đỉnh (phân phối)",
                GiaTriThucTe = $"{volumeRatio:F2}×",
                Nguong = $"> {_options.MinVolumeClimax:F1}×",
                Dat = datVolumeClimax
            },
            new()
            {
                VaiTro = VaiTroChiBao.HinhThai,
                MoTa = "MACD histogram đang giảm (momentum yếu đi)",
                GiaTriThucTe = $"Hist={histHienTai:F4} (trước={histTruoc:F4})",
                Nguong = "Hist hiện tại < Hist phiên trước",
                Dat = datMacdGiam
            }
        };

        return new KetQuaVaiTro(soDieuKienDat >= 2, bangChungs);
    }

    /// <summary>
    /// Kiểm tra cò kích hoạt — quay đầu: phân kỳ âm (giá đỉnh mới nhưng RSI thấp hơn)
    /// HOẶC giá đóng cửa dưới Bollinger Upper (chạm biên trên rồi quay đầu). Đạt ≥ 1/2.
    /// </summary>
    public KetQuaVaiTro KiemTraCoKichHoat(
        IReadOnlyList<OhlcvBar> history,
        decimal giaHienTai,
        long volumeHienTai)
    {
        if (history.Count < SoPhienToiThieu)
            return KhongDuLieu(VaiTroChiBao.CoKichHoat);

        // Phân kỳ âm: giá tạo đỉnh mới nhưng RSI thấp hơn đỉnh RSI 5 phiên trước.
        var highHienTai = history[^1].High;
        var rsiHienTai = IndicatorMath.Rsi(history, RsiPeriod);
        var dinhCu = decimal.MinValue;
        var rsiCaoNhat = decimal.MinValue;
        for (var i = history.Count - 1 - PhanKyLookback; i < history.Count - 1; i++)
        {
            if (i < 0) continue;
            if (history[i].High > dinhCu) dinhCu = history[i].High;
            var rsiTai = IndicatorMath.Rsi(history.Take(i + 1).ToList(), RsiPeriod);
            if (rsiTai > rsiCaoNhat) rsiCaoNhat = rsiTai;
        }
        var datPhanKyAm = highHienTai > dinhCu && rsiHienTai < rsiCaoNhat;

        // Quay đầu dưới Bollinger Upper.
        var (upper, _, _, _, _) = IndicatorMath.Bollinger(history, BollingerPeriod);
        var datQuayDau = giaHienTai < upper;

        var bangChungs = new List<BangChung>
        {
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "RSI phân kỳ âm (giá đỉnh mới, RSI thấp hơn)",
                GiaTriThucTe = $"High={highHienTai:F2} (đỉnh cũ={dinhCu:F2}), RSI={rsiHienTai:F1} (max={rsiCaoNhat:F1})",
                Nguong = "High > đỉnh cũ VÀ RSI < RSI max 5 phiên trước",
                Dat = datPhanKyAm
            },
            new()
            {
                VaiTro = VaiTroChiBao.CoKichHoat,
                MoTa = "Giá quay đầu dưới Bollinger Upper (chạm biên trên rồi lùi)",
                GiaTriThucTe = $"Giá={giaHienTai:F2}, Upper={upper:F2}",
                Nguong = "Giá < Bollinger Upper",
                Dat = datQuayDau
            }
        };

        return new KetQuaVaiTro(datPhanKyAm || datQuayDau, bangChungs);
    }

    /// <summary>
    /// Tính kế hoạch — đây là SELL nên Entry/SL/TP = 0 (không áp dụng).
    /// Điều kiện hủy = tín hiệu cho thấy nên GIỮ lại, không bán.
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
            DieuKienHuy = "RSI giảm dưới 60 + giá đóng cửa trên EMA10 → giữ lại, không bán"
        };
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
