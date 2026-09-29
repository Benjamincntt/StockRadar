using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>
/// Máy nhận kịch bản — bộ điều phối chính của Scenario Engine V2.
/// Pha 1: đánh giá Bối cảnh + Hình thái cho tất cả kịch bản BUY (và SELL qua <see cref="DanhGiaBanAsync"/>).
/// Pha 2: kiểm tra cò kích hoạt trong phiên.
/// </summary>
public class MayNhanKichBanService : IMayNhanKichBan
{
    private readonly IReadOnlyList<IKichBanDanhGia> _danhSachKichBan;
    private readonly IJobStockRepository _stockRepo;
    private readonly BanChupChiBaoBuilder _snapshotBuilder;
    private readonly ILogger<MayNhanKichBanService> _logger;

    public MayNhanKichBanService(
        IEnumerable<IKichBanDanhGia> danhSachKichBan,
        IJobStockRepository stockRepo,
        BanChupChiBaoBuilder snapshotBuilder,
        ILogger<MayNhanKichBanService> logger)
    {
        _danhSachKichBan = danhSachKichBan.ToList();
        _stockRepo = stockRepo;
        _snapshotBuilder = snapshotBuilder;
        _logger = logger;
    }

    /// <summary>
    /// Đánh giá tất cả kịch bản BUY (Bối cảnh + Hình thái) cho một mã trước phiên.
    /// </summary>
    public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaTruocPhienAsync(
        string symbol,
        IReadOnlyList<OhlcvBar> history,
        CancellationToken ct = default)
    {
        var ketQua = new List<KetQuaKichBan>();

        foreach (var kichBan in _danhSachKichBan.Where(k => !k.LaKichBanBan))
        {
            ct.ThrowIfCancellationRequested();

            // Vai trò 1 — Bối cảnh: "Mã này có đáng để ý không?"
            var boiCanh = kichBan.DanhGiaBoiCanh(history);
            if (!boiCanh.Dat)
            {
                // Chưa đạt bối cảnh → WATCHING (0%).
                ketQua.Add(new KetQuaKichBan
                {
                    Symbol = symbol,
                    LoaiKichBan = kichBan.LoaiKichBan,
                    TrangThai = TrangThaiKichBan.DangTheoDoi,
                    DatBoiCanh = false,
                    DanhSachBangChung = boiCanh.BangChungs
                });
                continue;
            }

            // Vai trò 2 — Hình thái: "Đang có setup đẹp không?"
            var hinhThai = kichBan.DanhGiaHinhThai(history);
            if (!hinhThai.Dat)
            {
                // Đạt bối cảnh nhưng chưa đạt hình thái → WATCHING (33%).
                ketQua.Add(new KetQuaKichBan
                {
                    Symbol = symbol,
                    LoaiKichBan = kichBan.LoaiKichBan,
                    TrangThai = TrangThaiKichBan.DangTheoDoi,
                    DatBoiCanh = true,
                    DanhSachBangChung = Gop(boiCanh.BangChungs, hinhThai.BangChungs)
                });
                continue;
            }

            // Đạt cả bối cảnh + hình thái → FORMING (67%), chờ cò kích hoạt ở Pha 2.
            ketQua.Add(new KetQuaKichBan
            {
                Symbol = symbol,
                LoaiKichBan = kichBan.LoaiKichBan,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
                DanhSachBangChung = Gop(boiCanh.BangChungs, hinhThai.BangChungs)
            });
        }

        return Task.FromResult<IReadOnlyList<KetQuaKichBan>>(ketQua);
    }

    /// <summary>
    /// Kiểm tra cò kích hoạt cho các mã đang FORMING (Pha 2 — trong phiên).
    /// Lấy history cho từng symbol, gọi <see cref="IKichBanDanhGia.KiemTraCoKichHoat"/>,
    /// nếu đạt → tính kế hoạch + chụp snapshot chỉ báo.
    /// </summary>
    public async Task<IReadOnlyList<KetQuaKichBan>> KiemTraTriggerTrongPhienAsync(
        IReadOnlyList<KetQuaKichBan> dangHinhThanh,
        IReadOnlyDictionary<string, decimal> giaHienTai,
        IReadOnlyDictionary<string, long> volumeHienTai,
        CancellationToken ct = default)
    {
        var ketQua = new List<KetQuaKichBan>();

        foreach (var item in dangHinhThanh)
        {
            ct.ThrowIfCancellationRequested();

            if (!giaHienTai.TryGetValue(item.Symbol, out var gia) ||
                !volumeHienTai.TryGetValue(item.Symbol, out var vol))
                continue;

            var kichBan = _danhSachKichBan.FirstOrDefault(k => k.LoaiKichBan == item.LoaiKichBan);
            if (kichBan == null) continue;

            // Lấy history cho symbol từ repository
            var stock = await _stockRepo.GetBySymbolAsync(item.Symbol, ct);
            var history = stock?.History;
            if (history == null || history.Count == 0) continue;

            // Kiểm tra cò kích hoạt
            var trigger = kichBan.KiemTraCoKichHoat(history, gia, vol);

            if (!trigger.Dat)
                continue;

            // TRIGGERED! Tính kế hoạch giao dịch
            var keHoach = kichBan.TinhKeHoach(history, gia);

            item.DatCoKichHoat = true;
            item.TrangThai = TrangThaiKichBan.DaKichHoat;
            item.ThoiGianKichHoat = DateTime.UtcNow;
            item.KeHoach = keHoach;
            item.DanhSachBangChung.AddRange(trigger.BangChungs);

            // Chụp bản ghi toàn bộ chỉ báo tại thời điểm trigger
            item.BangChup = _snapshotBuilder.Chup(history, gia, vol);

            _logger.LogInformation(
                "Kịch bản {Loai} TRIGGERED cho {Symbol} — giá {Gia:F2}, volume {Vol}.",
                item.LoaiKichBan, item.Symbol, gia, vol);

            ketQua.Add(item);
        }

        return ketQua;
    }

    /// <summary>
    /// Đánh giá kịch bản SELL cho vị thế đang giữ (Pha 2 — trong phiên).
    /// Cùng logic Bối cảnh → Hình thái như BUY; cò kích hoạt sẽ nối ở Pha 2.
    /// <paramref name="giaVaoLenh"/> được chuyển cho kịch bản Kiệt sức tính % gain từ giá vào lệnh thực tế.
    /// </summary>
    public Task<IReadOnlyList<KetQuaKichBan>> DanhGiaBanAsync(
        string symbol,
        IReadOnlyList<OhlcvBar> history,
        decimal giaVaoLenh,
        CancellationToken ct = default)
    {
        var ketQua = new List<KetQuaKichBan>();

        foreach (var kichBan in _danhSachKichBan.Where(k => k.LaKichBanBan))
        {
            ct.ThrowIfCancellationRequested();

            // Vai trò 1 — Bối cảnh: riêng Kiệt sức nhận thêm giá vào lệnh thực tế để tính % gain.
            var boiCanh = kichBan is KichBanKietSuc kietSuc
                ? kietSuc.DanhGiaBoiCanh(history, giaVaoLenh)
                : kichBan.DanhGiaBoiCanh(history);
            if (!boiCanh.Dat)
            {
                ketQua.Add(new KetQuaKichBan
                {
                    Symbol = symbol,
                    LoaiKichBan = kichBan.LoaiKichBan,
                    TrangThai = TrangThaiKichBan.DangTheoDoi,
                    DatBoiCanh = false,
                    DanhSachBangChung = boiCanh.BangChungs
                });
                continue;
            }

            // Vai trò 2 — Hình thái.
            var hinhThai = kichBan.DanhGiaHinhThai(history);
            if (!hinhThai.Dat)
            {
                ketQua.Add(new KetQuaKichBan
                {
                    Symbol = symbol,
                    LoaiKichBan = kichBan.LoaiKichBan,
                    TrangThai = TrangThaiKichBan.DangTheoDoi,
                    DatBoiCanh = true,
                    DanhSachBangChung = Gop(boiCanh.BangChungs, hinhThai.BangChungs)
                });
                continue;
            }

            // Đạt cả bối cảnh + hình thái → FORMING (67%), chờ cò kích hoạt bán ở Pha 2.
            _logger.LogDebug(
                "Kịch bản bán {Loai} đang hình thành cho {Symbol} (giá vào lệnh {GiaVaoLenh:F2}).",
                kichBan.LoaiKichBan, symbol, giaVaoLenh);

            ketQua.Add(new KetQuaKichBan
            {
                Symbol = symbol,
                LoaiKichBan = kichBan.LoaiKichBan,
                TrangThai = TrangThaiKichBan.DangHinhThanh,
                DatBoiCanh = true,
                DatHinhThai = true,
                DanhSachBangChung = Gop(boiCanh.BangChungs, hinhThai.BangChungs)
            });
        }

        return Task.FromResult<IReadOnlyList<KetQuaKichBan>>(ketQua);
    }

    /// <summary>Gộp hai danh sách bằng chứng thành một (giữ thứ tự: bối cảnh → hình thái).</summary>
    private static List<Domain.ValueObjects.BangChung> Gop(
        IReadOnlyList<Domain.ValueObjects.BangChung> a,
        IReadOnlyList<Domain.ValueObjects.BangChung> b)
        => a.Concat(b).ToList();
}
