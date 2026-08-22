using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class DichVuSuKienQuyen(INguonSuKienQuyen nguon) : IDichVuSuKienQuyen
{
    public IReadOnlyList<BanGhiSuKienQuyenDto> LayTheoMa(string ma)
    {
        var maChuan = (ma ?? "").Trim().ToUpperInvariant();
        return nguon.LayTheoMa(maChuan)
            .OrderByDescending(s => s.NgayKhongHuongQuyen)
            .Select(Map)
            .ToList();
    }

    public BanGhiSuKienQuyenDto Them(string ma, ThemSuKienQuyenRequest yeuCau)
    {
        var maChuan = (ma ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(maChuan))
            throw new AppException("Bad Request", "Thiếu mã cổ phiếu.", 400);
        if (yeuCau.Dilution <= 0)
            throw new AppException("Bad Request", "Hệ số pha loãng phải > 0 (thưởng 5:1 = 1.2; không thưởng = 1).", 400);
        if (yeuCau.Cash < 0)
            throw new AppException("Bad Request", "Cổ tức tiền không được âm.", 400);
        if (yeuCau.Cash >= 100)
            throw new AppException(
                "Bad Request",
                "Cổ tức phải cùng thang Close: 1.000đ = 1.0, không ghi 1000.",
                400);
        if (yeuCau.NewShares > 0 && yeuCau.OldShares <= 0)
            throw new AppException(
                "Bad Request",
                "Quyền mua cần tỷ lệ n:m (vd. 4 cổ cũ được mua 1 cổ mới).",
                400);
        if (yeuCau.IssuePrice < 0)
            throw new AppException("Bad Request", "Giá phát hành không được âm. 10.000đ = 10.0.", 400);
        if (yeuCau.OldShares < 0 || yeuCau.NewShares < 0)
            throw new AppException("Bad Request", "Tỷ lệ quyền mua không được âm.", 400);

        var daLuu = nguon.Them(new SuKienQuyen(
            maChuan,
            yeuCau.ExDate,
            yeuCau.Cash,
            yeuCau.Dilution,
            yeuCau.OldShares,
            yeuCau.NewShares,
            yeuCau.IssuePrice));
        return Map(daLuu);
    }

    private static BanGhiSuKienQuyenDto Map(SuKienQuyen s) =>
        new(s.Ma, s.NgayKhongHuongQuyen, s.TienMat, s.HeSoPhaLoang, s.SoCoCu, s.SoCoMoi, s.GiaPhatHanh);
}
