namespace StockRadar.Application.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Cho phép tạo tài khoản qua POST /api/v1/users. Mặc định tắt — app một người dùng, không public.
    /// </summary>
    public bool ChoPhepDangKy { get; set; }
}
