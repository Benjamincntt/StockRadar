namespace StockRadar.Application.Abstractions;

/// <summary>
/// Giá hiện tại theo mã, đọc thẳng cột <c>Stocks.LastClose</c> (KBS sync cập nhật mỗi 60 giây trong phiên,
/// Job 2 sau phiên chốt giá đóng cửa). Một truy vấn cho cả giỏ, không đụng <c>HistoryJson</c>, không gọi KBS.
/// </summary>
public interface IGiaHienTaiRepository
{
    /// <summary>Giá hiện tại (nghìn đồng) của các mã yêu cầu — mã không có trong DB thì vắng mặt.</summary>
    /// <param name="symbols">Mã cần lấy giá; mã rỗng và trùng bị bỏ qua.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    Task<IReadOnlyDictionary<string, decimal>> LayLastCloseAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);
}
