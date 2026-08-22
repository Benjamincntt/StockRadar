using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface INguonSuKienQuyen
{
    IReadOnlyList<SuKienQuyen> LayTheoMa(string ma);
    SuKienQuyen Them(SuKienQuyen suKien);
}

/// <summary>Nguồn bộ nhớ cho test / ctor mặc định.</summary>
public sealed class NguonSuKienQuyenDanhSach : INguonSuKienQuyen
{
    public static readonly NguonSuKienQuyenDanhSach Rong = new([]);

    private readonly Dictionary<string, List<SuKienQuyen>> _theoMa;

    public NguonSuKienQuyenDanhSach(IEnumerable<SuKienQuyen> suKien)
    {
        _theoMa = suKien
            .GroupBy(s => s.Ma, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SuKienQuyen> LayTheoMa(string ma) =>
        string.IsNullOrWhiteSpace(ma)
            ? []
            : _theoMa.TryGetValue(ma.Trim(), out var ds) ? ds.ToList() : [];

    public SuKienQuyen Them(SuKienQuyen suKien)
    {
        var ma = suKien.Ma.Trim();
        if (!_theoMa.TryGetValue(ma, out var ds))
        {
            ds = [];
            _theoMa[ma] = ds;
        }

        ds.RemoveAll(s => s.NgayKhongHuongQuyen == suKien.NgayKhongHuongQuyen);
        ds.Add(suKien);
        return suKien;
    }
}
