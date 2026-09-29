namespace StockRadar.Domain.Enums;

/// <summary>
/// Hành động giao dịch được đề xuất khi kịch bản kích hoạt.
/// </summary>
public enum LoaiHanhDong
{
    /// <summary>Mua điểm 1 — vào lệnh lần đầu (50% position)</summary>
    MuaDiem1 = 1,

    /// <summary>Mua điểm 2 — scale-in khi xác nhận tiếp (100% position)</summary>
    MuaDiem2 = 2,

    /// <summary>Bán nửa — giảm 50% vị thế (kịch bản Kiệt sức)</summary>
    BanNua = 3,

    /// <summary>Bán hết — thoát 100% vị thế (kịch bản Gãy nền)</summary>
    BanHet = 4
}
