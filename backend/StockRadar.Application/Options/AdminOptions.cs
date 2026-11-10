namespace StockRadar.Application.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Mật khẩu admin khi khởi tạo DB. Để trống thì không tạo/đổi mật khẩu admin
    /// (không đặt giá trị thật trong repo — khai báo trên server).
    /// </summary>
    public string Password { get; set; } = "";
}
