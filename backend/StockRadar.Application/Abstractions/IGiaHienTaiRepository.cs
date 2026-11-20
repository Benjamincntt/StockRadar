namespace StockRadar.Application.Abstractions;

/// <summary>
/// Giá hiện tại theo mã, đọc thẳng cột Stocks.LastClose (KBS sync cập nhật mỗi 60 giây trong phiên,
/// Job 2 sau phiên chốt giá đóng cửa), và giá tham chiếu phiên trước đọc từ HistoryJson. Không gọi KBS.
/// </summary>
public interface IGiaHienTaiRepository
{
    /// <summary>Giá hiện tại (nghìn đồng) của các mã yêu cầu — mã không có trong DB thì vắng mặt.</summary>
    /// <param name="symbols">Mã cần lấy giá; mã rỗng và trùng bị bỏ qua.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    Task<IReadOnlyDictionary<string, decimal>> LayLastCloseAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Giá tham chiếu để tính lãi/lỗ trong ngày: đóng cửa của nến liền trước nến mới nhất đã lưu
    /// (nến mới nhất là phiên của LastClose). Đọc HistoryJson nên chỉ gọi cho vài mã.
    /// </summary>
    /// <param name="symbols">Mã cần lấy; mã rỗng và trùng bị bỏ qua.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Theo mã: ngày phiên mới nhất và giá tham chiếu. Mã không có hoặc có dưới 2 nến thì vắng mặt.</returns>
    Task<IReadOnlyDictionary<string, GiaThamChieuPhien>> LayGiaThamChieuAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);
}
