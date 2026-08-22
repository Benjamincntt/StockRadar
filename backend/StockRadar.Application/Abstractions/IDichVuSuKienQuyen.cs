using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IDichVuSuKienQuyen
{
    IReadOnlyList<BanGhiSuKienQuyenDto> LayTheoMa(string ma);
    BanGhiSuKienQuyenDto Them(string ma, ThemSuKienQuyenRequest yeuCau);
}
