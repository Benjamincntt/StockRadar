using StockRadar.Api.Security;
using StockRadar.Application.Abstractions;
using Xunit;

namespace StockRadar.Tests.Security;

/// <summary>
/// Quyền kích pipeline job: chỉ admin đã đăng nhập hoặc sync key khớp.
/// </summary>
public sealed class JobTriggerAuthorizationTests
{
    private const string ConfiguredKey = "dev-sync-key-change-me";
    private static readonly Guid RegularUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void AdminDangNhap_DuocPhep()
    {
        Assert.True(JobTriggerAuthorization.IsAllowed(
            isAuthenticated: true, AdminUser.Id, syncKeyHeader: null, ConfiguredKey));
    }

    [Fact]
    public void UserThuongDangNhap_BiChan()
    {
        Assert.False(JobTriggerAuthorization.IsAllowed(
            isAuthenticated: true, RegularUserId, syncKeyHeader: null, ConfiguredKey));
    }

    [Fact]
    public void ChuaDangNhapKhongKey_BiChan()
    {
        Assert.False(JobTriggerAuthorization.IsAllowed(
            isAuthenticated: false, GuestUser.Id, syncKeyHeader: null, ConfiguredKey));
    }

    [Fact]
    public void SyncKeyDung_DuocPhep()
    {
        Assert.True(JobTriggerAuthorization.IsAllowed(
            isAuthenticated: false, Guid.Empty, ConfiguredKey, ConfiguredKey));
    }

    [Fact]
    public void SyncKeySai_BiChan()
    {
        Assert.False(JobTriggerAuthorization.IsAllowed(
            isAuthenticated: false, Guid.Empty, "sai-key", ConfiguredKey));
    }

    [Fact]
    public void SyncKeyCauHinhRongHeaderRong_BiChan()
    {
        Assert.False(JobTriggerAuthorization.IsAllowed(
            isAuthenticated: false, Guid.Empty, syncKeyHeader: null, configuredSyncKey: ""));
    }
}
