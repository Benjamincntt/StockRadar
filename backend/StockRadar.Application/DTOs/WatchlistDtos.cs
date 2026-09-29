namespace StockRadar.Application.DTOs;

/// <summary>Danh sách theo dõi (mặc định / ngành tự động / tùy chỉnh) cho API multi-watchlist.</summary>
/// <param name="Id">Mã danh sách.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="LaDanhSachNganh">true = danh sách ngành tự động (items động theo <paramref name="MaNganh"/>).</param>
/// <param name="MaNganh">Tên ngành nếu là danh sách ngành, null nếu là danh sách thường.</param>
/// <param name="LaMacDinh">true = danh sách mặc định của user (không xóa được).</param>
/// <param name="SoLuongMa">Số mã trong danh sách (ngành: đếm mã active của ngành; thường: đếm items).</param>
/// <param name="CreatedAt">Thời điểm tạo.</param>
public record WatchlistDto(
    int Id,
    string Name,
    bool LaDanhSachNganh,
    string? MaNganh,
    bool LaMacDinh,
    int SoLuongMa,
    DateTime CreatedAt);

public record CreateWatchlistRequest(string Name);

public record RenameWatchlistRequest(string Name);
