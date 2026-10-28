using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.MarketData;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Máy chia sẻ cho 4 runner V3 + lần nạp khi deploy. TẢI DỮ LIỆU chia hai chế độ (owner duyệt):
/// <list type="bullet">
/// <item><description><see cref="V3CheDoTai.DayDu"/> (16:00, backfill): quét toàn bộ universe, dựng lại chuỗi chỉ số,
/// ghi <c>ChiSoNgayV3</c> (chỉ số hôm nay + danh sách UNI cho phiên sau).</description></item>
/// <item><description><see cref="V3CheDoTai.NhipNhe"/> (08:30, 14:25): chỉ nạp lịch sử + giá realtime cho các mã
/// trong UNI đã lưu (~200 mã), MA50 đọc từ chuỗi đã lưu (ATC cộng thêm điểm hôm nay), KHÔNG ghi lại bảng chỉ số.</description></item>
/// </list>
/// Nhịp mỗi phút không dùng máy chỉ số gì cả: chỉ lấy giá mã đang giữ so với ngưỡng đã lưu.
/// </summary>
internal sealed class V3Engine(
    IJobStockRepository stockRepo,
    KbsPriceBoardClient kbs,
    ApplicationDbContext db,
    IOptions<V3Options> options,
    ILogger<V3Engine> logger)
{
    private const int BatchSize = 50;

    /// <summary>Số phiên chỉ số đọc từ <c>ChiSoNgayV3</c> để tính MA50 (cửa sổ 50 + dự phòng).</summary>
    private const int SoPhienChuoiDoc = 60;

    public V3CauHinh Config => options.Value.ToCauHinh();
    public V3Options Raw => options.Value;

    /// <summary>
    /// NẠP ĐẦY ĐỦ mọi mã (kể cả inactive — universe UNI cần đủ).
    /// LIMIT: chỉ dùng ở nhịp 16:00 và lần nạp một lần khi deploy; nhịp trong ngày phải dùng
    /// <see cref="LayMaNhipNheAsync"/> (đọc UNI đã lưu) hoặc giá của vị thế đang giữ.
    /// </summary>
    public async Task<IReadOnlyList<Stock>> LayCacMaAsync(CancellationToken ct) =>
        await stockRepo.GetAllForUniverseScreeningAsync(ct);

    /// <summary>
    /// Danh sách UNI đã lưu cho phiên <paramref name="homNay"/> = cột <c>DanhSachUni</c> của hàng
    /// <c>ChiSoNgayV3</c> gần nhất TRƯỚC hôm nay (nhịp 16:00 phiên trước ghi vào).
    /// </summary>
    public async Task<IReadOnlyList<string>> LayDanhSachUniDaLuuAsync(DateOnly homNay, CancellationToken ct)
    {
        var row = await db.ChiSoNgayV3.AsNoTracking()
            .Where(x => x.Ngay < homNay && x.DanhSachUni != null)
            .OrderByDescending(x => x.Ngay)
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return Array.Empty<string>();
        return row.DanhSachUni!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Nạp lịch sử cho nhịp nhẹ (08:30 / 14:25): đúng các mã trong UNI đã lưu (~200 mã), không quét
    /// toàn universe. Chưa có danh sách lưu (chưa backfill / ngày đầu deploy) → fallback nạp đầy đủ
    /// và ghi log cảnh báo để không mất tín hiệu.
    /// </summary>
    public async Task<IReadOnlyList<Stock>> LayMaNhipNheAsync(DateOnly homNay, CancellationToken ct)
    {
        var uni = await LayDanhSachUniDaLuuAsync(homNay, ct);
        if (uni.Count == 0)
        {
            logger.LogWarning(
                "V3: chưa có danh sách UNI đã lưu trước {Ngay} — nhịp nhẹ phải quét đầy đủ (chưa chạy backfill?).",
                homNay);
            return await LayCacMaAsync(ct);
        }
        return await stockRepo.GetBySymbolsAsync(uni, ct);
    }

    /// <summary>Chuỗi MỨC chỉ số đã lưu (tăng dần theo ngày, tối đa <see cref="SoPhienChuoiDoc"/> phiên trước hôm nay).</summary>
    public async Task<IReadOnlyList<decimal>> LayChuoiChiSoDaLuuAsync(DateOnly homNay, CancellationToken ct)
    {
        var rows = await db.ChiSoNgayV3.AsNoTracking()
            .Where(x => x.Ngay < homNay)
            .OrderByDescending(x => x.Ngay)
            .Take(SoPhienChuoiDoc)
            .Select(x => x.ChiSo)
            .ToListAsync(ct);
        rows.Reverse();
        return rows;
    }

    /// <summary>
    /// Snapshot chỉ báo toàn thị trường tại phiên tín hiệu. <paramref name="homNay"/> = hôm nay (giờ VN).
    /// <paramref name="quoteGia"/> null → nhịp trước phiên: nến hoàn thành tới hôm qua (loại nến giữ chỗ hôm nay).
    /// có quote → nhịp trong phiên/ATC: ghép giá khớp hôm nay thay đóng cửa, kèm
    /// <paramref name="quoteVolume"/> = KHỐI LƯỢNG KHỚP TÍCH LŨY của phiên (không được truyền 0 —
    /// TV20/UNI của phiên hôm nay sẽ sai nếu nến ghép thiếu khối lượng).
    /// <paramref name="cheDo"/>: <see cref="V3CheDoTai.DayDu"/> ghi <see cref="ChiSoNgayV3Entity"/>;
    /// <see cref="V3CheDoTai.NhipNhe"/> chỉ đọc chuỗi đã lưu để thay (ChiSo, MA50, % lệch).
    /// </summary>
    public async Task<V3ChiSoToanThiTruong.KetQua> LayChiSoAsync(
        IReadOnlyList<Stock> cacMa,
        DateOnly homNay,
        Dictionary<string, decimal>? quoteGia,
        Dictionary<string, long>? quoteVolume,
        V3CheDoTai cheDo,
        CancellationToken ct)
    {
        IReadOnlyList<Stock> chuanHoa;
        if (quoteGia is null)
        {
            // Nhịp trước phiên: chỉ dùng nến hoàn thành tới hôm qua (loại nến giữ chỗ hôm nay).
            chuanHoa = V3CotNen.ChuyenHistory(cacMa, bars => V3CotNen.NenHoanThanh(bars, homNay));
        }
        else
        {
            // Nhịp trong phiên/ATC: ghép giá khớp + khối lượng khớp tích lũy hôm nay thay đóng cửa cho từng mã (nếu có).
            var ds = new List<Stock>(cacMa.Count);
            foreach (var s in cacMa)
            {
                if (quoteGia.TryGetValue(s.Symbol, out var gia) && gia > 0m)
                {
                    var vl = quoteVolume?.GetValueOrDefault(s.Symbol) ?? 0L;
                    ds.Add(s with { History = V3CotNen.GheGiaKhopTrongPhien(s.History, homNay, gia, vl) });
                }
                else
                    ds.Add(s with { History = V3CotNen.NenHoanThanh(s.History, homNay) });
            }
            chuanHoa = ds;
        }

        var ketQua = V3ChiSoToanThiTruong.Tinh(chuanHoa, Config);

        if (cheDo == V3CheDoTai.DayDu)
        {
            await GhiChiSoNgayAsync(ketQua, ct);
            return ketQua;
        }

        // Nhịp nhẹ: chỉ số/MA50 lấy từ chuỗi đã lưu — 08:30 dùng nguyên chuỗi (phiên tín hiệu = hôm qua),
        // 14:25 (có giá khớp) cộng thêm điểm hôm nay = mức cuối chuỗi × (1 + lợi nhuận phiên hôm nay).
        var chuoi = await LayChuoiChiSoDaLuuAsync(homNay, ct);
        if (chuoi.Count == 0)
        {
            logger.LogWarning(
                "V3: chuỗi chỉ số đã lưu rỗng trước {Ngay} — dùng chỉ số tính lại tại chỗ (nên chạy nạp một lần).",
                homNay);
            return ketQua;
        }
        return GanChiSoTuChuoi(ketQua, chuoi, themDiemHomNay: quoteGia is not null);
    }

    /// <summary>Thay bộ ba (ChiSo, MA50, % lệch MA50) bằng chuỗi đã lưu + (tuỳ chọn) điểm hôm nay.</summary>
    private static V3ChiSoToanThiTruong.KetQua GanChiSoTuChuoi(
        V3ChiSoToanThiTruong.KetQua kt, IReadOnlyList<decimal> chuoiDaLuu, bool themDiemHomNay)
    {
        var muc = new List<decimal>(chuoiDaLuu);
        if (themDiemHomNay)
            muc.Add(muc[^1] * (1m + kt.ChiSoLoiNhuanHomNay));

        var window = Math.Min(50, muc.Count);
        var chiSo = muc[^1];
        decimal tong = 0m;
        for (var i = muc.Count - window; i < muc.Count; i++)
            tong += muc[i];
        var ma50 = tong / window;
        var lech = ma50 > 0m ? (chiSo / ma50 - 1m) * 100m : 0m;

        return kt with
        {
            ChiSo = Math.Round(chiSo, 6),
            ChiSoMa50 = Math.Round(ma50, 6),
            ChiSoDoLechMa50PhanTram = Math.Round(lech, 4),
        };
    }

    /// <summary>
    /// Upsert hàng loạt điểm chuỗi chỉ số theo ngày (lần nạp MỘT LẦN khi deploy).
    /// LIMIT: chỉ ghi <c>ChiSoNgayV3</c> — KHÔNG tạo tín hiệu hay vị thế cho các ngày đã qua.
    /// Trả số phiên đã ghi/cập nhật.
    /// </summary>
    public async Task<int> GhiChuoiChiSoAsync(
        IReadOnlyList<V3ChiSoToanThiTruong.DiemChuoi> chuoi, CancellationToken ct)
    {
        if (chuoi.Count == 0)
            return 0;

        var tu = chuoi[0].Ngay;
        var den = chuoi[^1].Ngay;
        var existingRows = await db.ChiSoNgayV3
            .Where(x => x.Ngay >= tu && x.Ngay <= den)
            .ToListAsync(ct);
        var existing = new Dictionary<DateOnly, ChiSoNgayV3Entity>();
        foreach (var row in existingRows)
            existing[row.Ngay] = row;

        foreach (var d in chuoi)
        {
            var (left, tdm) = V3LichPhien.LeftVaNgayTrongThang(d.Ngay);
            var danhSachUni = string.Join(",", d.DanhSachUni);
            if (existing.TryGetValue(d.Ngay, out var row))
            {
                row.ChiSo = d.ChiSo;
                row.ChiSoMa50 = d.ChiSoMa50;
                row.ChiSoDoLechMa50PhanTram = d.ChiSoDoLechMa50PhanTram;
                row.SoMaUni = d.SoMaUni;
                row.Breadth50 = d.Breadth50;
                row.Left = left;
                row.Tdm = tdm;
                row.DanhSachUni = danhSachUni;
            }
            else
            {
                db.ChiSoNgayV3.Add(new ChiSoNgayV3Entity
                {
                    Ngay = d.Ngay,
                    ChiSo = d.ChiSo,
                    ChiSoMa50 = d.ChiSoMa50,
                    ChiSoDoLechMa50PhanTram = d.ChiSoDoLechMa50PhanTram,
                    SoMaUni = d.SoMaUni,
                    Breadth50 = d.Breadth50,
                    Left = left,
                    Tdm = tdm,
                    DanhSachUni = danhSachUni,
                });
            }
        }
        await db.SaveChangesAsync(ct);
        return chuoi.Count;
    }

    private async Task GhiChiSoNgayAsync(V3ChiSoToanThiTruong.KetQua kt, CancellationToken ct)
    {
        if (kt.NgayTinHieu == default)
            return;
        var danhSachUni = string.Join(",", kt.DanhSachUni);
        var existing = await db.ChiSoNgayV3.FirstOrDefaultAsync(x => x.Ngay == kt.NgayTinHieu, ct);
        if (existing is null)
        {
            db.ChiSoNgayV3.Add(new ChiSoNgayV3Entity
            {
                Ngay = kt.NgayTinHieu,
                SoMaUni = kt.SoMaUni,
                Breadth50 = kt.Breadth50,
                ChiSo = kt.ChiSo,
                ChiSoMa50 = kt.ChiSoMa50,
                ChiSoDoLechMa50PhanTram = kt.ChiSoDoLechMa50PhanTram,
                Left = kt.Left,
                Tdm = kt.Tdm,
                DanhSachUni = danhSachUni,
            });
        }
        else
        {
            existing.SoMaUni = kt.SoMaUni;
            existing.Breadth50 = kt.Breadth50;
            existing.ChiSo = kt.ChiSo;
            existing.ChiSoMa50 = kt.ChiSoMa50;
            existing.ChiSoDoLechMa50PhanTram = kt.ChiSoDoLechMa50PhanTram;
            existing.Left = kt.Left;
            existing.Tdm = kt.Tdm;
            existing.DanhSachUni = danhSachUni;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Giá khớp realtime (đvt nghìn đồng) — giống luồng V2 đã dùng bảng giá KBS.</summary>
    public async Task<(
        Dictionary<string, decimal> Gia,
        Dictionary<string, long> Volume,
        Dictionary<string, decimal> High,
        Dictionary<string, decimal> Low)> LayRealtimeQuotesAsync(
        IReadOnlyList<string> symbols, CancellationToken ct)
    {
        var gia = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var volume = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var high = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var low = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < symbols.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = symbols.Skip(i).Take(BatchSize).ToList();
            var board = await kbs.FetchAsync(batch, ct);
            foreach (var row in board)
            {
                if (row.Close > 0)
                {
                    gia[row.Symbol] = row.Close;
                    volume[row.Symbol] = row.SessionVolume;
                    high[row.Symbol] = row.High > 0 ? row.High : row.Close;
                    low[row.Symbol] = row.Low > 0 ? row.Low : row.Close;
                }
            }
        }
        return (gia, volume, high, low);
    }

    /// <summary>Danh mục đang giữ (Chờ mua/Đang giữ) — chưa thoát.</summary>
    public async Task<List<ViTheV3Entity>> LayViTheDangMoAsync(CancellationToken ct) =>
        await db.ViTheV3
            .Where(x => x.TrangThai == TrangThaiViTheV3.DangGiu || x.TrangThai == TrangThaiViTheV3.ChoMua)
            .ToListAsync(ct);

    /// <summary>
    /// Trạng thái danh mục theo Phụ lục C. Vốn ban đầu = V0 (Raw.VonBanDau). Giá tham chiếu hiện hành =
    /// <paramref name="refGia"/> nếu có (giá khớp), ngược lại giá đóng cửa gần nhất của mã.
    /// </summary>
    public async Task<V3DanhMucXepHang.TrangThaiDanhMuc> LayTrangThaiDanhMucAsync(
        Dictionary<string, decimal> refGia, CancellationToken ct)
    {
        var all = await db.ViTheV3.ToListAsync(ct);
        var dangGiu = all.Where(x => x.TrangThai == TrangThaiViTheV3.DangGiu).ToList();
        var daKhop = all.Where(x => x.TrangThai is TrangThaiViTheV3.DangGiu or TrangThaiViTheV3.DaThoat
                                    && x.GiaTriLenh != null).ToList();

        var vonBanDau = Raw.VonBanDau;
        // Tiền đã thu về từ vị thế đã thoát (giá thoát × khối lượng).
        decimal thuVe = all.Where(x => x.TrangThai == TrangThaiViTheV3.DaThoat && x.GiaThoat != null && x.KhoiLuong != null)
            .Sum(x => (x.GiaThoat ?? 0m) * (x.KhoiLuong ?? 0m));
        // Vị thế đang giữ tính theo giá tham chiếu hiện hành.
        decimal giaTriGiu = dangGiu.Sum(x =>
        {
            var kl = x.KhoiLuong ?? 0m;
            var refP = refGia.TryGetValue(x.Symbol, out var g) && g > 0 ? g : x.GiaVao ?? 0m;
            return kl * refP;
        });
        // Tiền mặt = V0 − chi phí mua của MỌI lệnh đã khớp + tiền thu bán đã thoát.
        decimal tongMua = daKhop.Sum(x => x.GiaTriLenh ?? 0m);
        decimal tienMat = vonBanDau - tongMua + thuVe;
        if (tienMat < 0m) tienMat = 0m;
        decimal giaTriDanhMuc = tienMat + giaTriGiu;

        var maDangGiu = all
            .Where(x => x.TrangThai is TrangThaiViTheV3.ChoMua or TrangThaiViTheV3.DangGiu)
            .Select(x => x.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int choTrong = Math.Max(0, Config.MaxViThe - dangGiu.Count);
        return new V3DanhMucXepHang.TrangThaiDanhMuc(giaTriDanhMuc, tienMat, choTrong, maDangGiu);
    }

    /// <summary>Upsert tín hiệu V3 cho (Symbol, Loai, NgayTinHieu). Trả id.</summary>
    public async Task<TinHieuV3Entity> GhiTinHieuAsync(
        V3BoKichBan.UngVien uv, V3ChiSoToanThiTruong.KetQua kt, string snapshotJson, CancellationToken ct)
    {
        var row = await db.TinHieuV3.FirstOrDefaultAsync(
            x => x.Symbol == uv.Symbol && x.Loai == uv.Loai && x.NgayTinHieu == kt.NgayTinHieu, ct);
        if (row is null)
        {
            row = new TinHieuV3Entity
            {
                Symbol = uv.Symbol,
                Loai = uv.Loai,
                NgayTinHieu = kt.NgayTinHieu,
                GiaTinHieu = uv.GiaTinHieu,
                Hang = uv.Hang,
                GiaTriXepHang = uv.GiaTriThuocXepHang,
                MuaMoCuaPhienSau = uv.MuaMoCuaPhienSau,
                TrangThai = TrangThaiTinHieuV3.DaBao,
                SnapshotJson = snapshotJson,
                CreatedAt = DateTime.UtcNow,
            };
            db.TinHieuV3.Add(row);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            row.Hang = uv.Hang;
            row.GiaTinHieu = uv.GiaTinHieu;
            row.GiaTriXepHang = uv.GiaTriThuocXepHang;
            row.MuaMoCuaPhienSau = uv.MuaMoCuaPhienSau;
            row.SnapshotJson = snapshotJson;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return row;
    }

    /// <summary>
    /// Ngưỡng bán lưu vào vị thế. Lệnh thường = max(TB n−1 giá đóng cửa TRƯỚC hôm nay, giá vào × (1+phí)).
    /// Lệnh hoảng loạn sâu (FR-015a) = giá vào × 1,15 (chốt cố định, không dùng MA).
    /// N3 không có ngưỡng giá → null. Nhịp mỗi phút chỉ so giá khớp với con số này.
    /// </summary>
    public decimal? TinhNguongBan(
        LoaiKichBanV3 loai, decimal giaVao, IReadOnlyList<decimal> closesTruocHomNay, bool laHoangLoanSau = false)
    {
        if (loai == LoaiKichBanV3.N3)
            return null;
        // BUSINESS-RULE: hoảng loạn sâu chốt thẳng giá vào × 1,15, bỏ ngưỡng MA (FR-015a).
        if (laHoangLoanSau)
            return giaVao * Config.HoangLoanSauChotLoi;
        var nguongMa = V3LuatBanThucTe.NguongChamMa(loai, closesTruocHomNay);
        var nguongPhi = giaVao * Config.BanPhanTramGiaVao;
        return Math.Max(nguongMa, nguongPhi);
    }

    /// <summary>Mở vị thế từ tín hiệu (giá khớp thực, sau khi loại giá trần). Trả false nếu bị từ chối.</summary>
    public ViTheV3Entity MoViThe(
        TinHieuV3Entity tinHieu, DateOnly ngayVao, decimal giaKhop, decimal khoiLuong, decimal giaTriLenh,
        string? exchange, decimal giaThamChieu, decimal? nguongChamMa, bool laHoangLoanSau = false)
    {
        var vt = new ViTheV3Entity
        {
            TinHieuV3Id = tinHieu.Id,
            Symbol = tinHieu.Symbol,
            Loai = tinHieu.Loai,
            NgayTinHieu = tinHieu.NgayTinHieu,
            TrangThai = TrangThaiViTheV3.DangGiu,
            NgayVao = ngayVao,
            GiaVao = giaKhop,
            KhoiLuong = khoiLuong,
            GiaTriLenh = giaTriLenh,
            SoPhienDaGi = 0,
            Exchange = exchange,
            GiaThamChieu = giaThamChieu,
            NguongChamMa = nguongChamMa,
            LaHoangLoanSau = laHoangLoanSau,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ViTheV3.Add(vt);
        tinHieu.TrangThai = TrangThaiTinHieuV3.DaVaoLenh;
        tinHieu.GiaVao = giaKhop;
        tinHieu.UpdatedAt = DateTime.UtcNow;
        return vt;
    }

    /// <summary>Đánh dấu tín hiệu bị cắt vì danh mục (không mở lệnh).</summary>
    public void CatTinHieu(TinHieuV3Entity tinHieu)
    {
        if (tinHieu.TrangThai == TrangThaiTinHieuV3.DaBao)
        {
            tinHieu.TrangThai = TrangThaiTinHieuV3.BiCatDanhMuc;
            tinHieu.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tạo vị thế CHỜ MUA cho lệnh ATO/mở cửa phiên sau (KB1/KB2 bắn 08:30). Chưa khớp giá.
    /// Kèm giá tham chiếu + sàn của phiên khớp để nhịp mỗi phút xử lý trần/sàn mà không nạp lịch sử.
    /// Đánh dấu tín hiệu đã vào lệnh. Trả vị thế (chưa SaveChanges).
    /// </summary>
    public ViTheV3Entity TaoChoMua(
        TinHieuV3Entity tinHieu, decimal giaTriLenh, string? exchange, decimal giaThamChieu,
        bool laHoangLoanSau = false)
    {
        var vt = new ViTheV3Entity
        {
            TinHieuV3Id = tinHieu.Id,
            Symbol = tinHieu.Symbol,
            Loai = tinHieu.Loai,
            NgayTinHieu = tinHieu.NgayTinHieu,
            TrangThai = TrangThaiViTheV3.ChoMua,
            GiaTriLenh = giaTriLenh,
            SoPhienDaGi = 0,
            Exchange = exchange,
            GiaThamChieu = giaThamChieu,
            LaHoangLoanSau = laHoangLoanSau,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ViTheV3.Add(vt);
        tinHieu.TrangThai = TrangThaiTinHieuV3.DaVaoLenh;
        tinHieu.UpdatedAt = DateTime.UtcNow;
        return vt;
    }

    /// <summary>Khớp một vị thế CHỜ MUA thành ĐANG GIỮ tại giá khớp (ATO/mở cửa). Mirror giá vào vào tín hiệu.</summary>
    public async Task KhopChoMuaAsync(
        ViTheV3Entity vt, DateOnly ngayVao, decimal giaKhop, decimal khoiLuong, CancellationToken ct)
    {
        vt.TrangThai = TrangThaiViTheV3.DangGiu;
        vt.NgayVao = ngayVao;
        vt.GiaVao = giaKhop;
        vt.KhoiLuong = khoiLuong;
        vt.GiaTriLenh = giaKhop * khoiLuong;
        vt.SoPhienDaGi = 0;
        // Nhịp này không có lịch sử → chỉ chốt được phần phí; phần TB n−1 đóng cửa trước do nhịp 16:00 ghi.
        var nguongPhi = giaKhop * Config.BanPhanTramGiaVao;
        if (vt.Loai != LoaiKichBanV3.N3)
        {
            // BUSINESS-RULE: hoảng loạn sâu gán THẲNG giá khớp × 1,15 (không lấy max với giá trị rác — FR-015a).
            vt.NguongChamMa = vt.LaHoangLoanSau
                ? giaKhop * Config.HoangLoanSauChotLoi
                : Math.Max(vt.NguongChamMa ?? 0m, nguongPhi);
        }
        vt.UpdatedAt = DateTime.UtcNow;
        var th = await db.TinHieuV3.FirstOrDefaultAsync(x => x.Id == vt.TinHieuV3Id, ct);
        if (th is not null)
        {
            th.GiaVao = giaKhop;
            th.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Hủy lệnh CHỜ MUA vì giá mở cửa rơi đúng giá trần (FR-014) — không mở vị thế.
    /// Đánh dấu cả vị thế và tín hiệu HỦY.
    /// </summary>
    public async Task HuyLenhTranAsync(ViTheV3Entity vt, CancellationToken ct)
    {
        vt.TrangThai = TrangThaiViTheV3.BiHuy;
        vt.UpdatedAt = DateTime.UtcNow;
        var th = await db.TinHieuV3.FirstOrDefaultAsync(x => x.Id == vt.TinHieuV3Id, ct);
        if (th is not null)
        {
            th.TrangThai = TrangThaiTinHieuV3.BiHuy;
            th.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// FR-014a: hủy lệnh CHỜ MUA quá phiên mua (16:00) hoặc không đủ 1 lô lúc khớp.
    /// Đánh dấu cả vị thế và tín hiệu HỦY để bỏ chặn mã. Trả tín hiệu kèm (null nếu không có) để runner bắn tin.
    /// </summary>
    public async Task<TinHieuV3Entity?> HuyChoMuaAsync(ViTheV3Entity vt, CancellationToken ct)
    {
        vt.TrangThai = TrangThaiViTheV3.BiHuy;
        vt.UpdatedAt = DateTime.UtcNow;
        var th = await db.TinHieuV3.FirstOrDefaultAsync(x => x.Id == vt.TinHieuV3Id, ct);
        if (th is not null)
        {
            th.TrangThai = TrangThaiTinHieuV3.BiHuy;
            th.UpdatedAt = DateTime.UtcNow;
        }
        return th;
    }

    /// <summary>
    /// Khớp BÁN một vị thế đang giữ: chấm kết quả (lãi ròng % + xếp loại), đóng vị thế,
    /// mirror giá thoát/lãi/kết quả sang tín hiệu. Giải phóng slot + tiền mặt (tính ở LayTrangThaiDanhMucAsync).
    /// </summary>
    public async Task KhopBanAsync(
        ViTheV3Entity vt, DateOnly ngayThoat, decimal giaThoat, string lyDoThoat, CancellationToken ct)
    {
        var (lai, xepLoai) = V3ChamKetQua.Cham(vt.GiaVao ?? 0m, giaThoat, Config);
        vt.TrangThai = TrangThaiViTheV3.DaThoat;
        vt.NgayThoat = ngayThoat;
        vt.GiaThoat = giaThoat;
        vt.LyDoThoat = lyDoThoat;
        vt.PhanTramLoiNhuan = Math.Round(lai * 100m, 4);
        vt.KetQua = xepLoai.ToString();
        vt.DaBanTinThoat = true;
        vt.UpdatedAt = DateTime.UtcNow;
        var th = await db.TinHieuV3.FirstOrDefaultAsync(x => x.Id == vt.TinHieuV3Id, ct);
        if (th is not null)
        {
            th.GiaThoat = giaThoat;
            th.PhanTramLoiNhuan = vt.PhanTramLoiNhuan;
            th.KetQua = vt.KetQua;
            th.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Lưu mọi thay đổi DB của nhịp runner.</summary>
    public Task LuuAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>Tìm tín hiệu của một vị thế (để bắn tin/bổ sung ngữ cảnh).</summary>
    public Task<TinHieuV3Entity?> TimTinHieuAsync(long tinHieuId, CancellationToken ct) =>
        db.TinHieuV3.FirstOrDefaultAsync(x => x.Id == tinHieuId, ct);

    /// <summary>Snapshot chỉ báo quyết định (JSON gọn — FR-024).</summary>
    public static string ChupSnapshot(V3ChiSoToanThiTruong.ChiTieuMa c, V3ChiSoToanThiTruong.KetQua kt) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            c.Symbol, c.Sector, tv20 = c.Tv20Ty, close = c.Close, rsi14 = c.Rsi14, ret20 = c.Ret20,
            secr20 = c.Secr20, rs63 = c.Rs63, rs126 = c.Rs126, floor20 = c.Floor20, laSan = c.LaPhienSan,
            breadth50 = kt.Breadth50, chiSoMa50Lech = kt.ChiSoDoLechMa50PhanTram, left = kt.Left, tdm = kt.Tdm,
            soMaUni = kt.SoMaUni,
        });
}

/// <summary>
/// Chế độ tải dữ liệu của một nhịp V3 (owner duyệt): <see cref="DayDu"/> quét toàn universe và
/// ghi lại bảng chỉ số ngày; <see cref="NhipNhe"/> chỉ nạp đúng các mã UNI đã lưu và đọc chuỗi
/// chỉ số đã lưu (không ghi gì vào <c>ChiSoNgayV3</c>).
/// </summary>
internal enum V3CheDoTai
{
    /// <summary>Nhịp 16:00 / lần nạp khi deploy — nạp đầy đủ, ghi ChiSoNgayV3.</summary>
    DayDu,
    /// <summary>Nhịp 08:30 / 14:25 — nạp theo UNI đã lưu, MA50 đọc từ chuỗi đã lưu.</summary>
    NhipNhe,
}
