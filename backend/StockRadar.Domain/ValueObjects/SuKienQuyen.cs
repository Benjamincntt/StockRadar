namespace StockRadar.Domain.ValueObjects;

/// <summary>Một đợt không hưởng quyền — hai tham số, không có trường loại.</summary>
public sealed record SuKienQuyen(
    string Ma,
    DateOnly NgayKhongHuongQuyen,
    decimal TienMat = 0,
    decimal HeSoPhaLoang = 1m);
