using StockRadar.Infrastructure.Persistence;
using Xunit;

namespace StockRadar.Tests.Security;

/// <summary>
/// Quyết định có gọi EnsureAdminUserAsync hay không theo Admin:Password. Không chạm DB/MigrateAsync.
/// </summary>
public sealed class AdminSeedPasswordTests
{
    [Theory]
    [InlineData("mat-khau-that")]
    [InlineData("CaU-hinh-khac-rong")]
    public void CoGiaTri_PhaiDatMatKhauAdmin(string password)
        => Assert.True(DatabaseInitializer.ShouldSetAdminPassword(password));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void rongHoacNull_KhongDatMatKhauAdmin(string? password)
        => Assert.False(DatabaseInitializer.ShouldSetAdminPassword(password));
}
