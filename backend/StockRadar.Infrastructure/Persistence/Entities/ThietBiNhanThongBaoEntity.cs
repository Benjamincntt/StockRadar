namespace StockRadar.Infrastructure.Persistence.Entities;

/// <summary>Registered device storage; see specs/013-mobile-push-notify/plan.md for schema and ownership rules.</summary>
public sealed class ThietBiNhanThongBaoEntity
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public string NenTang { get; set; } = "android";
    public DateTime NgayDangKy { get; set; }
    public DateTime LanCapNhatGanNhat { get; set; }
}
