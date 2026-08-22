namespace StockRadar.Domain.ValueObjects;

/// <summary>Một đợt không hưởng quyền — tiền + pha loãng + quyền mua trả tiền.</summary>
public sealed record SuKienQuyen(
    string Ma,
    DateOnly NgayKhongHuongQuyen,
    decimal TienMat = 0,
    decimal HeSoPhaLoang = 1m,
    int SoCoCu = 0,
    int SoCoMoi = 0,
    decimal GiaPhatHanh = 0)
{
    /// <summary>4:1 → 0.25. Không quyền mua → 0.</summary>
    public decimal TyLeQuyenMua =>
        SoCoCu > 0 && SoCoMoi > 0 ? (decimal)SoCoMoi / SoCoCu : 0;
}
