using System.Text.Json;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

/// <summary>
/// Nghiệp vụ nhật ký giao dịch thật (spec 010). UserId luôn lấy từ token ở controller — service nhận tường minh để dễ test.
/// </summary>
public sealed class NhatKyService(
    INhatKyRepository repo,
    INhatKyTinHieuRepository tinHieu,
    IOptions<RealizedPnlOptions> options)
{
    private static readonly string[] LoaiLenhHopLe = ["Mua", "Ban", "DieuChinh"];
    private static readonly string[] NguonHopLe = ["V3"];

    private readonly RealizedPnlOptions _fees = options.Value;

    public async Task<NhatKyDanhSachResponse> LayDanhSachAsync(
        Guid userId, string? symbol, DateTime? tuUtc, DateTime? denUtc, string? loaiLenh,
        int page, int size, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var (items, total) = await repo.LayDanhSachAsync(userId, symbol, tuUtc, denUtc, loaiLenh, page, size, ct);
        var binhQuan = LapMapBinhQuanKhop(await repo.LayToanBoLenhAsync(userId, ct));
        var dto = await GanKetQuaTinHieuAsync(items.Select(x => ToDto(x, binhQuan)).ToList(), ct);
        return new NhatKyDanhSachResponse(dto, total, page, size);
    }

    public async Task<LenhDto?> LayMotAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var row = await repo.LayMotAsync(userId, id, ct);
        if (row is null)
            return null;
        var binhQuan = LapMapBinhQuanKhop(await repo.LayToanBoLenhAsync(userId, ct));
        return (await GanKetQuaTinHieuAsync([ToDto(row, binhQuan)], ct))[0];
    }

    public async Task<LenhDto> TaoLenhAsync(Guid userId, TaoLenhRequest req, CancellationToken ct = default)
    {
        var normalized = ChuanHoaVaKiemTra(req);
        await KiemTraBan(userId, normalized, loaiId: null, ct);

        var nguon = normalized.NguonTinHieu;
        var maTinHieu = normalized.MaTinHieu;
        var thuTruoc = new Dictionary<Guid, (string Chieu, int ThuTu)>();
        if (nguon is not null)
        {
            // BUSINESS-RULE: gắn yêu cầu tín hiệu còn tồn tại và có giá vào (mua điểm 1) — ngược lại 404.
            await LayNhomGiaHoac404Async(nguon, maTinHieu!, ct);
            await repo.XoaBoQuaAsync(userId, nguon, maTinHieu!, ct);
            thuTruoc = await LayThuTruocAsync(userId, nguon, maTinHieu, ct);
        }

        var row = new NhatKyLenhRow(
            Guid.NewGuid(), userId, normalized.MaChungKhoan, normalized.Chieu,
            normalized.ThoiDiemKhop, normalized.GiaKhop, normalized.KhoiLuong,
            normalized.Phi, normalized.NguonTinHieu, normalized.MaTinHieu, null, // N8: bậc quyết giá chụp bên dưới.
            normalized.GhiChu, normalized.LyDo, normalized.KLieuChinh, normalized.VonDieuChinh,
            DateTime.UtcNow);

        await repo.ThemAsync(row, ct);
        await ReChupNhomAsync(userId, nguon, maTinHieu, thuTruoc, ct);
        var luu = await repo.LayMotAsync(userId, row.Id, ct) ?? row;
        return (await GanKetQuaTinHieuAsync([ToDto(luu, LapMapBinhQuanKhop(await repo.LayToanBoLenhAsync(userId, ct)))], ct))[0];
    }

    public async Task<LenhDto?> SuaLenhAsync(Guid userId, Guid id, TaoLenhRequest req, CancellationToken ct = default)
    {
        var existing = await repo.LayMotAsync(userId, id, ct);
        if (existing is null)
            return null;

        var normalized = ChuanHoaVaKiemTra(req);
        await KiemTraBan(userId, normalized, loaiId: id, ct);

        var doiTinHieu = normalized.NguonTinHieu != existing.NguonTinHieu || normalized.MaTinHieu != existing.MaTinHieu;
        if (doiTinHieu && normalized.NguonTinHieu is not null)
        {
            await LayNhomGiaHoac404Async(normalized.NguonTinHieu, normalized.MaTinHieu!, ct);
            await repo.XoaBoQuaAsync(userId, normalized.NguonTinHieu, normalized.MaTinHieu!, ct);
        }

        var truocDoi = await repo.LayToanBoLenhAsync(userId, ct);
        var thuMoi = NhomThuTu(truocDoi, normalized.NguonTinHieu, normalized.MaTinHieu).ThuTu;
        var thuCu = NhomThuTu(truocDoi, existing.NguonTinHieu, existing.MaTinHieu).ThuTu;

        // BUSINESS-RULE N8: cùng tín hiệu và không đổi bậc → giữ nguyên giá đã chụp, không đọc lại nguồn.
        var row = existing with
        {
            MaChungKhoan = normalized.MaChungKhoan,
            Chieu = normalized.Chieu,
            ThoiDiemKhop = normalized.ThoiDiemKhop,
            GiaKhop = normalized.GiaKhop,
            KhoiLuong = normalized.KhoiLuong,
            Phi = normalized.Phi,
            NguonTinHieu = normalized.NguonTinHieu,
            MaTinHieu = normalized.MaTinHieu,
            GiaTinHieuChup = doiTinHieu ? null : existing.GiaTinHieuChup,
            GhiChu = normalized.GhiChu,
            LyDo = normalized.LyDo,
            KLieuChinh = normalized.KLieuChinh,
            VonDieuChinh = normalized.VonDieuChinh,
        };

        await repo.SuaAsync(row, ct);
        await ReChupNhomAsync(userId, normalized.NguonTinHieu, normalized.MaTinHieu, thuMoi, ct);
        if (doiTinHieu)
            await ReChupNhomAsync(userId, existing.NguonTinHieu, existing.MaTinHieu, thuCu, ct);

        var luu = await repo.LayMotAsync(userId, id, ct) ?? row;
        return (await GanKetQuaTinHieuAsync([ToDto(luu, LapMapBinhQuanKhop(await repo.LayToanBoLenhAsync(userId, ct)))], ct))[0];
    }

    public async Task<bool> XoaLenhAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var existing = await repo.LayMotAsync(userId, id, ct);
        if (existing is null)
            return false;
        var thuCu = await LayThuTruocAsync(userId, existing.NguonTinHieu, existing.MaTinHieu, ct);
        await repo.XoaAsync(userId, id, ct);
        await ReChupNhomAsync(userId, existing.NguonTinHieu, existing.MaTinHieu, thuCu, ct);
        return true;
    }

    /// <summary>US3: bước nội bộ — một nhịp bán + lãi ròng VND chưa làm tròn (dùng cho tổng tiền và bucket theo kỳ).</summary>
    private sealed record NhipBanSo(DateOnly Ngay, decimal GiaBan, decimal KhoiLuong, decimal GiaVonTaiNhip, decimal LaiRongPhanTram, decimal LoiNhuanVND);

    private sealed record ViTheSo(
        string Symbol, decimal KhoiLuongMo, decimal GiaVonBinhQuan, decimal TienVonDangKe, DateOnly NgayMo,
        int SoLenhDaBan, string? NguonGan, string? MaGan,
        bool DaDong, DateOnly? NgayDong, decimal? ReturnOnDeployedPercent, decimal? LoiNhuanRongVND,
        IReadOnlyList<NhipBanSo> Nhip);

    public async Task<IReadOnlyList<ViTheDto>> LayViTheAsync(Guid userId, CancellationToken ct = default)
    {
        var ds = await LayViTheSoAsync(userId, ct);
        return ds.Select(v => new ViTheDto(
            v.Symbol, v.KhoiLuongMo, v.GiaVonBinhQuan, v.TienVonDangKe, v.NgayMo, v.SoLenhDaBan, v.NguonGan, v.MaGan,
            v.DaDong, v.NgayDong, v.ReturnOnDeployedPercent, v.LoiNhuanRongVND,
            v.Nhip.Select(n => new NhipBanDto(
                n.Ngay, n.GiaBan, n.KhoiLuong,
                Math.Round(n.GiaVonTaiNhip, 4, MidpointRounding.AwayFromZero),
                Math.Round(n.LaiRongPhanTram, 2, MidpointRounding.AwayFromZero))).ToList())).ToList();
    }

    /// <summary>FR-006 — thống kê theo kỳ của user hiện tại: vị thế đóng đếm theo nhịp bán cuối, tiền theo nhịp bán trong kỳ.</summary>
    public async Task<ThongKeDto> LayThongKeAsync(Guid userId, string? ky, CancellationToken ct = default)
    {
        var cutoff = TinhCatMoc(ky);
        var cutoffDate = cutoff is null ? (DateOnly?)null : DateOnly.FromDateTime(cutoff.Value);
        var viThe = await LayViTheSoAsync(userId, ct);
        var rows = await repo.LayToanBoLenhAsync(userId, ct);
        var binhQuan = LapMapBinhQuanKhop(rows);

        var dongKy = viThe
            .Where(v => v.DaDong && v.NgayDong is not null && (cutoffDate is null || v.NgayDong >= cutoffDate))
            .Select(v => (Ngay: v.NgayDong!.Value, Bucket: v.ReturnOnDeployedPercent is null ? null : RealizedPnlMath.Classify(v.ReturnOnDeployedPercent.Value, _fees.WinThresholdPercent)))
            .ToList();
        var tang = dongKy.Count(b => b.Bucket == OutcomeBucketNames.Good);
        var thua = dongKy.Count(b => b.Bucket == OutcomeBucketNames.Failed);
        var ngang = dongKy.Count(b => b.Bucket == OutcomeBucketNames.Flat);
        var soDongCoKetQua = dongKy.Count(b => b.Bucket is not null);
        decimal? tyLeThang = soDongCoKetQua > 0
            ? Math.Round(tang * 100m / soDongCoKetQua, 2, MidpointRounding.AwayFromZero)
            : null;
        var choDo = viThe.Count(v => !v.DaDong);

        var nhipKy = viThe.SelectMany(v => v.Nhip).Where(n => cutoffDate is null || n.Ngay >= cutoffDate).ToList();
        var tongLoiNhuan = Math.Round(nhipKy.Sum(n => n.LoiNhuanVND), 2, MidpointRounding.AwayFromZero);
        var tongDoLoso = Math.Round(nhipKy.Sum(n => n.GiaVonTaiNhip * n.KhoiLuong), 2, MidpointRounding.AwayFromZero);
        var theoKy = nhipKy.Select(n => NgayDauThang(n.Ngay)).Union(dongKy.Select(b => NgayDauThang(b.Ngay)))
            .Distinct().OrderBy(d => d)
            .Select(k => new ThongKeTheoKyDto(
                k,
                dongKy.Count(b => NgayDauThang(b.Ngay) == k && b.Bucket == OutcomeBucketNames.Good),
                dongKy.Count(b => NgayDauThang(b.Ngay) == k && b.Bucket == OutcomeBucketNames.Failed),
                Math.Round(nhipKy.Where(n => NgayDauThang(n.Ngay) == k).Sum(n => n.LoiNhuanVND), 2, MidpointRounding.AwayFromZero)))
            .ToList();

        var lech = rows
            .Where(x => x.NguonTinHieu is not null && x.Chieu != "DieuChinh" && (cutoff is null || x.ThoiDiemKhop >= cutoff.Value))
            .Select(x => (x.Chieu, Lech: TinhLech(x, binhQuan).LechPhanTram))
            .Where(t => t.Lech is not null)
            .ToList();
        decimal? trungBinh(string? chieu) =>
            lech.Count(t => chieu is null || t.Chieu == chieu) > 0
                ? Math.Round(lech.Where(t => chieu is null || t.Chieu == chieu).Sum(t => t.Lech!.Value) / lech.Count(t => chieu is null || t.Chieu == chieu), 2, MidpointRounding.AwayFromZero)
                : null;

        var daGan = rows
            .Where(x => x.NguonTinHieu is not null && x.MaTinHieu is not null && (cutoff is null || x.ThoiDiemKhop >= cutoff.Value))
            .Select(x => $"{x.NguonTinHieu}|{x.MaTinHieu}").Distinct().Count();
        var boQuaKy = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nguon in NguonHopLe)
            foreach (var b in await repo.LayBoQuaTheoNguonAsync(userId, nguon, ct))
                if (cutoff is null || b.BoQuaNgay >= cutoff.Value)
                    boQuaKy.Add($"{nguon}|{b.MaTinHieu}");
        var tongTinHieu = daGan + boQuaKy.Count;
        decimal? tyLeKhop = tongTinHieu > 0
            ? Math.Round(daGan * 100m / tongTinHieu, 2, MidpointRounding.AwayFromZero)
            : null;

        return new ThongKeDto(
            new ThongKeViTheDto(tang, thua, ngang, choDo, tyLeThang),
            rows.Count(x => cutoff is null || x.ThoiDiemKhop >= cutoff.Value),
            tongLoiNhuan, tongDoLoso, _fees.WinThresholdPercent,
            tyLeKhop, daGan, boQuaKy.Count,
            trungBinh(null), trungBinh("Mua"), trungBinh("Ban"),
            theoKy);
    }

    /// <summary>Kỳ theo convention HieuQua: tuần −7 / tháng −30 / quý −90 ngày; còn lại = tất cả.</summary>
    private static DateTime? TinhCatMoc(string? ky) => ky?.Trim().ToLowerInvariant() switch
    {
        "week" => DateTime.UtcNow.AddDays(-7),
        "month" => DateTime.UtcNow.AddDays(-30),
        "quarter" => DateTime.UtcNow.AddDays(-90),
        _ => null,
    };

    private static DateOnly NgayDauThang(DateOnly d) => new(d.Year, d.Month, 1);

    /// <summary>US3: đi tuần từng mã — gồm cả vị thế đã đóng; lãi ròng mỗi nhịp bán dùng RealizedPnlMath trên giá vốn BQ tại nhịp.</summary>
    private async Task<List<ViTheSo>> LayViTheSoAsync(Guid userId, CancellationToken ct)
    {
        var rows = await repo.LayToanBoLenhAsync(userId, ct);
        var ketQua = new List<ViTheSo>();

        foreach (var nhom in rows.GroupBy(x => x.MaChungKhoan))
        {
            var duongDi = TinhGiaVonBinhQuan.Tinh(nhom.Select(ToLenhBinhQuan).ToList());
            var nhipBan = duongDi.NhipBan ?? [];
            if (duongDi.KhoiLuongMo <= 0m && nhipBan.Count == 0)
                continue; // không còn vị thế và chưa từng bán → không hiển thị

            var nhip = new List<NhipBanSo>();
            var weighted = 0m;
            var tongKL = 0m;
            var tongVnd = 0m;
            foreach (var n in nhipBan)
            {
                // GIÁ VỐN BQ đã gồm phí mua → FeeProfile chỉ trừ phí+thuế bán (theo lệnh nếu có Phi, ngược lại config).
                var r = RealizedPnlMath.NetLegReturnPercent(n.GiaVonTaiNhip, n.GiaBan, new FeeProfile(0m, n.BanFeeTaxPercent, 0m));
                if (r is null)
                    continue;
                var vnd = r.Value / 100m * n.GiaVonTaiNhip * n.KhoiLuong;
                nhip.Add(new NhipBanSo(n.Ngay, n.GiaBan, n.KhoiLuong, n.GiaVonTaiNhip, r.Value, vnd));
                weighted += n.KhoiLuong * r.Value;
                tongKL += n.KhoiLuong;
                tongVnd += vnd;
            }

            // NguonTinHieuGan lấy từ nhịp Mua gần nhất còn tham chiếu tín hiệu.
            var gan = nhom.OrderByDescending(x => x.ThoiDiemKhop)
                .FirstOrDefault(x => x.Chieu == "Mua" && !string.IsNullOrWhiteSpace(x.NguonTinHieu));

            var daDong = duongDi.KhoiLuongMo <= 0m;
            ketQua.Add(new ViTheSo(
                nhom.Key,
                duongDi.KhoiLuongMo,
                duongDi.GiaVonBinhQuan,
                duongDi.TienVonDangKe,
                duongDi.NgayMo ?? DateOnly.FromDateTime(nhom.Min(x => x.ThoiDiemKhop)),
                duongDi.SoLenhDaBan,
                gan?.NguonTinHieu,
                gan?.MaTinHieu,
                daDong,
                daDong && nhip.Count > 0 ? nhip.Max(x => x.Ngay) : null,
                tongKL > 0 ? Math.Round(weighted / tongKL, 2, MidpointRounding.AwayFromZero) : null,
                nhip.Count > 0 ? Math.Round(tongVnd, 2, MidpointRounding.AwayFromZero) : null,
                nhip));
        }

        return ketQua.OrderBy(x => x.Symbol).ToList();
    }

    // ===== US2 — picker tín hiệu (chỉ đọc) + đánh dấu bỏ qua =====

    public async Task<TinHieuPickerResponse> LayTinHieuV3Async(
        Guid userId, DateTime? tuUtc, DateTime? denUtc, int page = 1, int size = 30, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var (rows, total) = await tinHieu.LayTinHieuV3Async(tuUtc, denUtc, page, size, ct);
        var (daCoLenh, daBoQua) = await LayCotTinHieuCuaUserAsync(userId, "V3", ct);
        var items = rows.Select(r => ToItemV3(r, daCoLenh, daBoQua)).ToList();
        return new TinHieuPickerResponse(items, total, page, size);
    }

    /// <summary>Upsert idempotent đánh dấu bỏ qua/không khớp (Q3). 404 nếu tín hiệu không tồn tại ở nguồn.</summary>
    public async Task DatBoQuaAsync(Guid userId, string nguon, string maTinHieu, string? lyDo, CancellationToken ct = default)
    {
        var (normalizedNguon, normalizedMa) = ChuanHoaTinHieuKey(nguon, maTinHieu);
        var symbol = await LaySymbolTinHieuAsync(normalizedNguon, normalizedMa, ct);
        var row = new NhatKyBoQuaRow(
            Guid.NewGuid(), userId, normalizedNguon, normalizedMa, symbol,
            string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim(), DateTime.UtcNow);
        await repo.ThemBoQuaAsync(row, ct);
    }

    public async Task XoaBoQuaAsync(Guid userId, string nguon, string maTinHieu, CancellationToken ct = default)
    {
        var (normalizedNguon, normalizedMa) = ChuanHoaTinHieuKey(nguon, maTinHieu);
        await repo.XoaBoQuaAsync(userId, normalizedNguon, normalizedMa, ct);
    }

    private static (string Nguon, string Ma) ChuanHoaTinHieuKey(string? nguon, string? maTinHieu)
    {
        var n = (nguon ?? string.Empty).Trim().ToUpperInvariant();
        var m = (maTinHieu ?? string.Empty).Trim();
        if (!NguonHopLe.Contains(n) || m.Length == 0)
            throw new NhatKyTinHieuNotFoundException(nguon ?? string.Empty, maTinHieu ?? string.Empty);
        return (n, m);
    }

    private async Task<string> LaySymbolTinHieuAsync(string nguon, string ma, CancellationToken ct)
    {
        return nguon == "V3"
            && long.TryParse(ma, out var idV3)
            && await tinHieu.LayMotTinHieuV3Async(idV3, ct) is { } v3
                ? v3.Symbol
                : throw new NhatKyTinHieuNotFoundException(nguon, ma);
    }

    private async Task<(HashSet<string> DaCoLenh, HashSet<string> DaBoQua)> LayCotTinHieuCuaUserAsync(
        Guid userId, string nguon, CancellationToken ct)
    {
        var lenh = await repo.LayToanBoLenhAsync(userId, ct);
        var daCoLenh = lenh.Where(x => x.NguonTinHieu == nguon && x.MaTinHieu is not null)
            .Select(x => x.MaTinHieu!).ToHashSet(StringComparer.Ordinal);
        var boQua = await repo.LayBoQuaTheoNguonAsync(userId, nguon, ct);
        return (daCoLenh, boQua.Select(x => x.MaTinHieu).ToHashSet(StringComparer.Ordinal));
    }

    private static TinHieuPickerItem ToItemV3(TinHieuV3Row r, HashSet<string> daCoLenh, HashSet<string> daBoQua)
    {
        // FR-040: giá chụp theo chiều lệnh V3 — mua → giá vào; bán → giá thoát.
        var giaTinHieu = r.GiaVao > 0 ? r.GiaVao : r.GiaThoat;
        return new TinHieuPickerItem(
            "V3", r.Id.ToString(), r.Symbol, r.LoaiTinHieu,
            r.NgayTinHieu,
            giaTinHieu > 0 ? giaTinHieu : null,
            null,
            r.KetQua, null,
            daCoLenh.Contains(r.Id.ToString()), daBoQua.Contains(r.Id.ToString()));
    }

    private static KeHoachGiaoDich? GiaiKeHoach(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<KeHoachGiaoDich>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>N8 — bốn mức giá chụp trong một chu kỳ: mua1/mua2 cho lệnh Mua, ban1/ban2 cho lệnh Bán; thiếu giá ở bậc nào → null bậc đó.</summary>
    private sealed record NhomGiaTinHieu(decimal? Mua1, decimal? Mua2, decimal? Ban1, decimal? Ban2);

    private static decimal? GiaDuong(decimal? v) => v > 0m ? v : null;

    private static decimal? ChupTheoGan(NhomGiaTinHieu g, string chieu, int thuTu) => (chieu, thuTu) switch
    {
        ("Mua", 0) => g.Mua1,
        ("Mua", 1) => g.Mua2,
        ("Ban", 0) => g.Ban1,
        ("Ban", > 0) => g.Ban2,
        _ => null,
    };

    /// <summary>Đọc cụm giá N8 từ nguồn tín hiệu (chỉ SELECT); null nếu tín hiệu không còn ở nguồn.</summary>
    private async Task<NhomGiaTinHieu?> LayNhomGiaAsync(string nguon, string maTinHieu, CancellationToken ct)
    {
        if (nguon == "V3")
        {
            if (!long.TryParse(maTinHieu, out var idV3) || await tinHieu.LayMotTinHieuV3Async(idV3, ct) is not { } v3)
                return null;
            // V3: một lệnh mua + một lệnh bán → Mua1 = giá vào, Ban1 = giá thoát; không có bậc 2.
            return new NhomGiaTinHieu(
                GiaDuong(v3.GiaVao),
                null,
                GiaDuong(v3.GiaThoat) ?? GiaDuong(v3.GiaVao),
                GiaDuong(v3.GiaThoat));
        }
        return null;
    }

    private async Task<NhomGiaTinHieu> LayNhomGiaHoac404Async(string nguon, string maTinHieu, CancellationToken ct)
    {
        var nhom = await LayNhomGiaAsync(nguon, maTinHieu, ct);
        if (nhom is null || nhom.Mua1 is null)
            throw new NhatKyTinHieuNotFoundException(nguon, maTinHieu);
        return nhom;
    }

    /// <summary>N8: thứ tự = ThoiDiemKhop tăng dần (TaoNgay phá hoà), đếm riêng theo chiều trong cùng nhóm tín hiệu.</summary>
    private static (List<NhatKyLenhRow> Rows, Dictionary<Guid, (string Chieu, int ThuTu)> ThuTu) NhomThuTu(
        IReadOnlyList<NhatKyLenhRow> all, string? nguon, string? ma)
    {
        var rows = all
            .Where(x => x.NguonTinHieu == nguon && x.MaTinHieu == ma && x.Chieu != "DieuChinh")
            .OrderBy(x => x.ThoiDiemKhop).ThenBy(x => x.TaoNgay)
            .ToList();
        var dem = new Dictionary<string, int>(StringComparer.Ordinal);
        var map = new Dictionary<Guid, (string, int)>();
        foreach (var x in rows)
        {
            dem.TryGetValue(x.Chieu, out var n);
            dem[x.Chieu] = n + 1;
            map[x.Id] = (x.Chieu, n);
        }
        return (rows, map);
    }

    private async Task<Dictionary<Guid, (string Chieu, int ThuTu)>> LayThuTruocAsync(
        Guid userId, string? nguon, string? ma, CancellationToken ct)
        => nguon is null || ma is null
            ? new Dictionary<Guid, (string, int)>()
            : NhomThuTu(await repo.LayToanBoLenhAsync(userId, ct), nguon, ma).ThuTu;

    /// <summary>
    /// N8: sau thêm/sửa/xoá lệnh, chỉ chụp lại các lệnh bị đổi (chiều, bậc) trong nhóm; lệnh giữ bậc giữ giá đã chụp.
    /// Nguồn đã mất → không chụp lại, giữ nguyên snapshot (data-model validation #4).
    /// </summary>
    private async Task ReChupNhomAsync(
        Guid userId, string? nguon, string? ma,
        IReadOnlyDictionary<Guid, (string Chieu, int ThuTu)> thuTruoc, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(nguon) || string.IsNullOrEmpty(ma))
            return;
        var (rowsSau, thuSau) = NhomThuTu(await repo.LayToanBoLenhAsync(userId, ct), nguon, ma);
        var biDich = rowsSau.Where(x => !thuTruoc.TryGetValue(x.Id, out var cu) || cu != thuSau[x.Id]).ToList();
        if (biDich.Count == 0)
            return;
        var gia = await LayNhomGiaAsync(nguon, ma, ct);
        if (gia is null)
            return;
        foreach (var x in biDich)
        {
            var moi = ChupTheoGan(gia, x.Chieu, thuSau[x.Id].ThuTu);
            if (x.GiaTinHieuChup != moi)
                await repo.SuaAsync(x with { GiaTinHieuChup = moi }, ct);
        }
    }

    /// <summary>Chuẩn hoá + validation data-model #1/#3/#5; trả row đã sạch ( ném NhatKyValidationException → 400).</summary>
    private static NhatKyLenhRow ChuanHoaVaKiemTra(TaoLenhRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Symbol))
            throw new NhatKyValidationException("Thiếu mã chứng khoán.");
        if (!LoaiLenhHopLe.Contains(req.LoaiLenh))
            throw new NhatKyValidationException("loaiLenh phải là Mua, Ban hoặc DieuChinh.");
        if (req.ThoiDiemKhop > DateTime.UtcNow)
            throw new NhatKyValidationException("Thời điểm khớp không được ở tương lai.");
        if (req.Phi < 0)
            throw new NhatKyValidationException("Phí không được âm.");

        var symbol = req.Symbol.Trim().ToUpperInvariant();

        if (req.LoaiLenh == "DieuChinh")
        {
            if (req.GiaKhop is not null || req.KhoiLuong is not null || req.Phi is not null)
                throw new NhatKyValidationException("Lệnh điều chỉnh không có giá khớp/khối lượng/phí.");
            if (string.IsNullOrWhiteSpace(req.GhiChu))
                throw new NhatKyValidationException("Lệnh điều chỉnh bắt buộc ghi chú.");
            if ((req.KLieuChinh ?? 0m) == 0m && (req.VonDieuChinh ?? 0m) == 0m)
                throw new NhatKyValidationException("Lệnh điều chỉnh cần KLieuChinh hoặc VonDieuChinh khác 0.");
        }
        else
        {
            if (req.GiaKhop is null || req.GiaKhop <= 0m)
                throw new NhatKyValidationException("Thiếu giá khớp.");
            if (req.KhoiLuong is null || req.KhoiLuong <= 0m)
                throw new NhatKyValidationException("Thiếu khối lượng.");
        }

        var nguon = string.IsNullOrWhiteSpace(req.NguonTinHieu) ? null : req.NguonTinHieu.Trim().ToUpperInvariant();
        var maTinHieu = string.IsNullOrWhiteSpace(req.MaTinHieu) ? null : req.MaTinHieu.Trim();
        if (nguon is null != maTinHieu is null)
            throw new NhatKyValidationException("nguonTinHieu và maTinHieu phải đi cùng cặp.");

        return new NhatKyLenhRow(
            Guid.Empty, Guid.Empty, symbol, req.LoaiLenh, req.ThoiDiemKhop,
            req.LoaiLenh == "DieuChinh" ? 0m : req.GiaKhop!.Value,
            req.LoaiLenh == "DieuChinh" ? 0m : req.KhoiLuong!.Value,
            req.Phi, nguon, maTinHieu, null,
            string.IsNullOrWhiteSpace(req.GhiChu) ? null : req.GhiChu.Trim(),
            string.IsNullOrWhiteSpace(req.LyDo) ? null : req.LyDo.Trim(),
            req.KLieuChinh, req.VonDieuChinh, default);
    }

    /// <summary>Q7: bán quá vị thế / bán khi chưa có mua, và điều chỉnh làm tồn kho âm → chặn cứng, không lưu.</summary>
    private async Task KiemTraBan(Guid userId, NhatKyLenhRow lenhMoi, Guid? loaiId, CancellationToken ct)
    {
        if (lenhMoi.Chieu is not ("Ban" or "DieuChinh"))
            return;

        var rows = (await repo.LayToanBoLenhAsync(userId, ct))
            .Where(x => x.MaChungKhoan == lenhMoi.MaChungKhoan && (loaiId is null || x.Id != loaiId.Value))
            .OrderBy(x => x.ThoiDiemKhop)
            .ThenBy(x => x.TaoNgay)
            .ToList();

        // Chèn lệnh đang kiểm vào đúng sau các lệnh có ThoiDiemKhop nhỏ hơn nó (cùng thời điểm → kiểm sau).
        var chenVao = rows.FindLastIndex(x => x.ThoiDiemKhop < lenhMoi.ThoiDiemKhop) + 1;
        var truoc = TinhGiaVonBinhQuan.Tinh(rows.Take(chenVao).Select(ToLenhBinhQuan).ToList()).KhoiLuongMo;

        if (lenhMoi.Chieu == "Ban")
        {
            if (truoc <= 0m || lenhMoi.KhoiLuong > truoc)
                throw new NhatKyVuotBanException(lenhMoi.MaChungKhoan, truoc, lenhMoi.KhoiLuong);
            return;
        }

        // DieuChinh: đi tuần toàn bộ (kèm lệnh mới) — HopLe fail khi tồn kho âm tại bất kỳ nhịp nào.
        var duongDiLenh = rows.Select(ToLenhBinhQuan).ToList();
        duongDiLenh.Insert(chenVao, ToLenhBinhQuan(lenhMoi));
        var duongDi = TinhGiaVonBinhQuan.Tinh(duongDiLenh);
        if (!duongDi.HopLe || duongDi.KhoiLuongMo < 0m)
            throw new NhatKyVuotBanException(lenhMoi.MaChungKhoan, truoc, -(lenhMoi.KLieuChinh ?? 0m));
    }

    private LenhBinhQuan ToLenhBinhQuan(NhatKyLenhRow x)
    {
        var buyFeePercent = _fees.BuyFeePercent;
        if (x.Chieu == "Mua" && x.Phi is > 0 && x.GiaKhop > 0 && x.KhoiLuong > 0)
            buyFeePercent = Math.Round(x.Phi.Value / (x.GiaKhop * x.KhoiLuong) * 100m, 4, MidpointRounding.AwayFromZero);

        // US3: phí bán + thuế — theo lệnh nếu người dùng nhập Phi, không thì cấu hình RealizedPnl.
        var banFeeTaxPercent = _fees.SellFeePercent + _fees.SellTaxPercent;
        if (x.Chieu == "Ban" && x.Phi is > 0 && x.GiaKhop > 0 && x.KhoiLuong > 0)
            banFeeTaxPercent = Math.Round(x.Phi.Value / (x.GiaKhop * x.KhoiLuong) * 100m, 4, MidpointRounding.AwayFromZero);

        return new LenhBinhQuan(x.Chieu, x.ThoiDiemKhop, x.GiaKhop, x.KhoiLuong, buyFeePercent, x.KLieuChinh, x.VonDieuChinh, banFeeTaxPercent);
    }

    /// <summary>N8: BQ giá khớp theo KL chỉ gộp lệnh CÙNG CHIỀU và CÙNG mức giá chụp trong một tín hiệu (mua so mua, bán so bán).</summary>
    private static Dictionary<string, decimal> LapMapBinhQuanKhop(IReadOnlyList<NhatKyLenhRow> allLenh)
    {
        var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var nhom in allLenh
                     .Where(x => x.NguonTinHieu is not null && x.GiaTinHieuChup is > 0 && x.Chieu != "DieuChinh" && x.KhoiLuong > 0)
                     .GroupBy(x => $"{x.NguonTinHieu}|{x.MaTinHieu}|{x.Chieu}|{x.GiaTinHieuChup}"))
        {
            var tongKL = nhom.Sum(x => x.KhoiLuong);
            if (tongKL > 0)
                map[nhom.Key] = nhom.Sum(x => x.GiaKhop * x.KhoiLuong) / tongKL;
        }
        return map;
    }

    /// <summary>US3: gắn kết quả tín hiệu V3 (bucket + %) vào các lệnh đã gắn để so cạnh kết quả thật.</summary>
    private async Task<IReadOnlyList<LenhDto>> GanKetQuaTinHieuAsync(IReadOnlyList<LenhDto> items, CancellationToken ct)
    {
        var v3Ids = items.Where(x => x.NguonTinHieu == "V3" && long.TryParse(x.MaTinHieu, out _))
            .Select(x => long.Parse(x.MaTinHieu!)).Distinct().ToList();
        if (v3Ids.Count == 0)
            return items;

        var v3 = (await tinHieu.LayKetQuaV3Async(v3Ids, ct)).ToDictionary(x => x.Id);
        return items.Select(x => x.NguonTinHieu switch
        {
            "V3" when long.TryParse(x.MaTinHieu, out var id) && v3.TryGetValue(id, out var k)
                => x with { KetQuaTinHieu = k.KetQua, KetQuaTinHieuPhanTram = k.PhanTramLoiNhuan },
            _ => x,
        }).ToList();
    }

    /// <summary>N8: BQ chỉ gộp lệnh CÙNG CHIỀU + CÙNG mức giá chụp → lệch tính trên BQ KL của nhóm đó.</summary>
    private static (decimal? Lech, decimal? LechPhanTram) TinhLech(NhatKyLenhRow x, IReadOnlyDictionary<string, decimal>? binhQuanKhop)
    {
        if (x.GiaTinHieuChup is null or <= 0 || x.Chieu == "DieuChinh")
            return (null, null);
        var giaChieu = x.GiaKhop;
        if (binhQuanKhop is not null && binhQuanKhop.TryGetValue($"{x.NguonTinHieu}|{x.MaTinHieu}|{x.Chieu}|{x.GiaTinHieuChup}", out var bq))
            giaChieu = bq;
        return (
            Math.Round(giaChieu - x.GiaTinHieuChup.Value, 2, MidpointRounding.AwayFromZero),
            Math.Round((giaChieu - x.GiaTinHieuChup.Value) / x.GiaTinHieuChup.Value * 100m, 2, MidpointRounding.AwayFromZero));
    }

    private static LenhDto ToDto(NhatKyLenhRow x, IReadOnlyDictionary<string, decimal>? binhQuanKhop = null)
    {
        var laDieuChinh = x.Chieu == "DieuChinh";
        var (lech, lechPhanTram) = TinhLech(x, binhQuanKhop);

        return new LenhDto(
            x.Id, x.MaChungKhoan, x.Chieu, x.ThoiDiemKhop,
            laDieuChinh ? 0m : x.GiaKhop,
            laDieuChinh ? 0m : x.KhoiLuong,
            x.Phi, x.NguonTinHieu, x.MaTinHieu, x.GiaTinHieuChup, lech, lechPhanTram,
            x.LyDo, x.GhiChu, x.KLieuChinh, x.VonDieuChinh, x.TaoNgay);
    }
}
