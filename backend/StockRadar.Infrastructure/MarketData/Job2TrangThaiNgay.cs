namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Trạng thái trong bộ nhớ của Job 2 trong một ngày phiên: danh sách mã KBS không trả nến,
/// để hai lần thử lại 15:15/15:30 chỉ lấy đúng mã còn thiếu (spec 018 Q4).
/// Không xuống DB vì chỉ có tác dụng trong 25 phút; mất khi khởi động lại thì lần thử rơi về chạy đầy đủ.
/// </summary>
internal sealed class Job2TrangThaiNgay
{
    /// <summary>Ngày phiên mà <see cref="MaConThieu"/> đang mô tả; null nếu chưa có lần chạy nào hôm nay.</summary>
    public DateOnly? NgayPhien { get; private set; }

    /// <summary>Các mã có trong lô gửi KBS nhưng không nhận được dòng nến nào.</summary>
    public HashSet<string> MaConThieu { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Số lần đã thực sự lấy bảng giá hôm nay (lần chính = 1, thử lại 15:15 = 2, 15:30 = 3).</summary>
    public int LanThu { get; private set; }

    /// <summary>Kiểm tra trạng thái hiện có thuộc đúng ngày phiên cần dùng.</summary>
    /// <param name="ngayPhien">Ngày phiên đang xét.</param>
    /// <returns>true nếu đã có lần chạy ghi trạng thái cho ngày này.</returns>
    public bool CoTrangThaiCuaNgay(DateOnly ngayPhien) => NgayPhien == ngayPhien;

    /// <summary>Lần chạy đầy đủ (chính hoặc sau khởi động lại) ghi kết quả và đặt lại đếm lần thử.</summary>
    /// <param name="ngayPhien">Ngày phiên vừa chạy.</param>
    /// <param name="cacMaThieu">Các mã KBS không trả nến trong lần chạy.</param>
    public void GhiSauLanDayDu(DateOnly ngayPhien, IEnumerable<string> cacMaThieu)
    {
        NgayPhien = ngayPhien;
        MaConThieu.Clear();
        ThemMaThieu(cacMaThieu);
        LanThu = 1;
    }

    /// <summary>Lần thử lại cập nhật danh sách còn thiếu và tăng đếm lần thử.</summary>
    /// <param name="cacMaThieu">Các mã vẫn chưa có nến sau lần thử này.</param>
    public void GhiSauLanThuLai(IEnumerable<string> cacMaThieu)
    {
        MaConThieu.Clear();
        ThemMaThieu(cacMaThieu);
        LanThu++;
    }

    private void ThemMaThieu(IEnumerable<string> cacMaThieu)
    {
        foreach (var symbol in cacMaThieu)
        {
            if (!string.IsNullOrWhiteSpace(symbol))
                MaConThieu.Add(symbol.Trim().ToUpperInvariant());
        }
    }
}
