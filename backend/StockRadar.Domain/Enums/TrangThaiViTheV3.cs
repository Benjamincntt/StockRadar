namespace StockRadar.Domain.Enums;

/// <summary>
/// Trạng thái vòng đời một vị thế V3. Tín hiệu (chưa vào lệnh) nằm ở bảng riêng,
/// enum này chỉ mô tả vị thế đã được danh mục chọn.
/// </summary>
public enum TrangThaiViTheV3
{
    /// <summary>Đã chọn nhưng chưa khớp — chờ mua ATO/mở cửa phiên sau (KB1/KB2).</summary>
    ChoMua = 0,

    /// <summary>Đang giữ — đã khớp lệnh mua, xét bán realtime mỗi phút.</summary>
    DangGiu = 1,

    /// <summary>Đã thoát hết — khớp điều kiện bán hoặc hết hạn giữ / ATO theo lịch.</summary>
    DaThoat = 2,

    /// <summary>Hủy — giá mua rơi đúng giá trần nên bỏ lệnh, không mở vị thế.</summary>
    BiHuy = 3
}
