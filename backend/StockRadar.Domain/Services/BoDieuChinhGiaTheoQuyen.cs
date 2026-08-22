using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>Công thức thống nhất tiền + pha loãng. Nến thô vào, dãy chấm điểm ra.</summary>
public sealed class BoDieuChinhGiaTheoQuyen(INguonSuKienQuyen nguon)
{
    public decimal TinhGiaThamChieu(
        decimal giaTruocQuyen,
        decimal tienMat,
        decimal heSoPhaLoang,
        decimal tyLeQuyenMua = 0,
        decimal giaPhatHanh = 0)
    {
        var mau = heSoPhaLoang + tyLeQuyenMua;
        if (giaTruocQuyen <= 0 || mau <= 0)
            return 0;
        return (giaTruocQuyen - tienMat + tyLeQuyenMua * giaPhatHanh) / mau;
    }

    public decimal TinhHeSoNgayQuyen(
        decimal giaTruocQuyen,
        decimal tienMat,
        decimal heSoPhaLoang,
        decimal tyLeQuyenMua = 0,
        decimal giaPhatHanh = 0)
    {
        var giaThamChieu = TinhGiaThamChieu(
            giaTruocQuyen, tienMat, heSoPhaLoang, tyLeQuyenMua, giaPhatHanh);
        if (giaTruocQuyen <= 0 || giaThamChieu <= 0)
            return 1;
        return giaThamChieu / giaTruocQuyen;
    }

    public IReadOnlyList<OhlcvBar> TaoDayGiaDieuChinh(string ma, IReadOnlyList<OhlcvBar> lichSuTho)
    {
        if (lichSuTho.Count == 0)
            return lichSuTho;

        var suKien = nguon.LayTheoMa(ma);
        if (suKien.Count == 0)
            return lichSuTho;

        var heSoHopLe = new List<(DateOnly Ngay, decimal HeSo)>();
        foreach (var sk in suKien)
        {
            if (sk.HeSoPhaLoang <= 0)
                continue;
            if (sk.SoCoMoi > 0 && sk.SoCoCu <= 0)
                continue;
            if (sk.GiaPhatHanh < 0)
                continue;

            var giaTruoc = CloseThoTruoc(lichSuTho, sk.NgayKhongHuongQuyen);
            if (giaTruoc <= 0)
                continue;

            var heSo = TinhHeSoNgayQuyen(
                giaTruoc, sk.TienMat, sk.HeSoPhaLoang, sk.TyLeQuyenMua, sk.GiaPhatHanh);
            if (heSo <= 0 || heSo == 1)
                continue;

            heSoHopLe.Add((sk.NgayKhongHuongQuyen, heSo));
        }

        if (heSoHopLe.Count == 0)
            return lichSuTho;

        var kq = new OhlcvBar[lichSuTho.Count];
        for (var i = 0; i < lichSuTho.Count; i++)
        {
            var nen = lichSuTho[i];
            decimal tich = 1;
            foreach (var (ngay, heSo) in heSoHopLe)
            {
                if (nen.Date < ngay)
                    tich *= heSo;
            }

            kq[i] = tich == 1
                ? nen
                : nen with
                {
                    Open = nen.Open * tich,
                    High = nen.High * tich,
                    Low = nen.Low * tich,
                    Close = nen.Close * tich
                };
        }

        return kq;
    }

    private static decimal CloseThoTruoc(IReadOnlyList<OhlcvBar> lichSuTho, DateOnly ngayQuyen)
    {
        for (var i = lichSuTho.Count - 1; i >= 0; i--)
        {
            if (lichSuTho[i].Date < ngayQuyen && lichSuTho[i].Close > 0)
                return lichSuTho[i].Close;
        }

        return 0;
    }
}
