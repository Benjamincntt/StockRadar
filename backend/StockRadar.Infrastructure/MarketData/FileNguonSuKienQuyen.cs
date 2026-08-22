using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class FileNguonSuKienQuyen : INguonSuKienQuyen
{
    private static readonly JsonSerializerOptions JsonTuyChon = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _khoa = new();
    private readonly string? _duongDanChay;
    private readonly string? _duongDanNguon;
    private readonly ILogger? _nhatKy;
    private Dictionary<string, List<SuKienQuyen>> _theoMa;

    public FileNguonSuKienQuyen(
        IHostEnvironment moiTruong,
        IOptions<SuKienQuyenOptions> tuyChon,
        ILogger<FileNguonSuKienQuyen> nhatKy)
    {
        _nhatKy = nhatKy;
        _duongDanChay = GiaiDuongDan(moiTruong.ContentRootPath, tuyChon.Value.FilePath);
        _duongDanNguon = TimFileNguonTrongRepo(moiTruong.ContentRootPath);
        _theoMa = ToMutable(PhanTich(DocNoiDung(_duongDanChay, nhatKy), nhatKy));
    }

    internal FileNguonSuKienQuyen(string? json, ILogger? nhatKy, string? duongDanGhi = null)
    {
        _nhatKy = nhatKy;
        _duongDanChay = duongDanGhi;
        _duongDanNguon = null;
        _theoMa = ToMutable(PhanTich(json, nhatKy));
    }

    public IReadOnlyList<SuKienQuyen> LayTheoMa(string ma)
    {
        lock (_khoa)
        {
            return string.IsNullOrWhiteSpace(ma)
                ? []
                : _theoMa.TryGetValue(ma.Trim(), out var ds) ? ds.ToList() : [];
        }
    }

    public SuKienQuyen Them(SuKienQuyen suKien)
    {
        lock (_khoa)
        {
            var ma = suKien.Ma.Trim();
            if (!_theoMa.TryGetValue(ma, out var ds))
            {
                ds = [];
                _theoMa[ma] = ds;
            }

            ds.RemoveAll(s => s.NgayKhongHuongQuyen == suKien.NgayKhongHuongQuyen);
            ds.Add(suKien);
            GhiFile();
            return suKien;
        }
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<SuKienQuyen>> PhanTich(
        string? json,
        ILogger? nhatKy)
    {
        var raw = ToMutable(PhanTichNoiBo(json, nhatKy));
        return raw.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<SuKienQuyen>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, List<SuKienQuyen>> PhanTichNoiBo(string? json, ILogger? nhatKy)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, List<SuKienQuyen>>(StringComparer.OrdinalIgnoreCase);

        TaiLieuSuKienQuyen? taiLieu;
        try
        {
            taiLieu = JsonSerializer.Deserialize<TaiLieuSuKienQuyen>(json, JsonTuyChon);
        }
        catch (JsonException ex)
        {
            nhatKy?.LogError(ex, "Seed sự kiện quyền JSON hỏng — dùng nguồn rỗng");
            return new Dictionary<string, List<SuKienQuyen>>(StringComparer.OrdinalIgnoreCase);
        }

        var hopLe = new List<SuKienQuyen>();
        foreach (var dong in taiLieu?.SuKien ?? [])
        {
            if (string.IsNullOrWhiteSpace(dong.Ma) || dong.NgayKhongHuongQuyen is null)
            {
                nhatKy?.LogWarning("Bỏ sự kiện quyền thiếu ma hoặc ngayKhongHuongQuyen");
                continue;
            }

            if (dong.HeSoPhaLoang <= 0)
            {
                nhatKy?.LogWarning("Bỏ sự kiện quyền {Ma} {Ngay}: heSoPhaLoang ≤ 0", dong.Ma, dong.NgayKhongHuongQuyen);
                continue;
            }

            hopLe.Add(new SuKienQuyen(
                dong.Ma.Trim(),
                dong.NgayKhongHuongQuyen.Value,
                dong.TienMat,
                dong.HeSoPhaLoang));
        }

        return hopLe
            .GroupBy(s => s.Ma, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, List<SuKienQuyen>> ToMutable(
        IReadOnlyDictionary<string, IReadOnlyList<SuKienQuyen>> nguon) =>
        nguon.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, List<SuKienQuyen>> ToMutable(
        Dictionary<string, List<SuKienQuyen>> nguon) => nguon;

    private void GhiFile()
    {
        var taiLieu = new TaiLieuSuKienQuyen
        {
            SuKien = _theoMa.Values
                .SelectMany(ds => ds)
                .OrderBy(s => s.Ma, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.NgayKhongHuongQuyen)
                .Select(s => new DongSuKienQuyen
                {
                    Ma = s.Ma,
                    NgayKhongHuongQuyen = s.NgayKhongHuongQuyen,
                    TienMat = s.TienMat,
                    HeSoPhaLoang = s.HeSoPhaLoang
                })
                .ToList()
        };
        var json = JsonSerializer.Serialize(taiLieu, JsonTuyChon);

        foreach (var path in new[] { _duongDanChay, _duongDanNguon }.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            try
            {
                var thuMuc = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(thuMuc))
                    Directory.CreateDirectory(thuMuc);
                File.WriteAllText(path!, json);
            }
            catch (Exception ex)
            {
                _nhatKy?.LogError(ex, "Không ghi được file sự kiện quyền {Path}", path);
            }
        }
    }

    private static string GiaiDuongDan(string contentRoot, string duongDanTuongDoi) =>
        Path.IsPathRooted(duongDanTuongDoi)
            ? duongDanTuongDoi
            : Path.Combine(contentRoot, duongDanTuongDoi);

    private static string? TimFileNguonTrongRepo(string contentRoot)
    {
        var thuMucApi = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", ".."));
        var path = Path.Combine(thuMucApi, "Data", "su-kien-quyen.json");
        return File.Exists(path) ? path : null;
    }

    private static string? DocNoiDung(string? duongDan, ILogger nhatKy)
    {
        if (string.IsNullOrWhiteSpace(duongDan) || !File.Exists(duongDan))
        {
            nhatKy.LogWarning("Không thấy file sự kiện quyền {Path} — mọi mã dùng dãy thô", duongDan);
            return null;
        }

        try
        {
            return File.ReadAllText(duongDan);
        }
        catch (Exception ex)
        {
            nhatKy.LogError(ex, "Không đọc được file sự kiện quyền {Path}", duongDan);
            return null;
        }
    }

    private sealed class TaiLieuSuKienQuyen
    {
        public List<DongSuKienQuyen> SuKien { get; set; } = [];
    }

    private sealed class DongSuKienQuyen
    {
        public string? Ma { get; set; }
        public DateOnly? NgayKhongHuongQuyen { get; set; }
        public decimal TienMat { get; set; }
        public decimal HeSoPhaLoang { get; set; } = 1m;
    }
}
