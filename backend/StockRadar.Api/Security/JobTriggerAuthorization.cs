using StockRadar.Application.Abstractions;

namespace StockRadar.Api.Security;

/// <summary>
/// Quyết định quyền kích pipeline job. Tách thành hàm thuần để test được mà không cần dựng HTTP.
/// </summary>
public static class JobTriggerAuthorization
{
    /// <summary>
    /// Cho phép khi (a) người dùng đã đăng nhập là admin, hoặc (b) sync key cấu hình khác rỗng và header khớp.
    /// So khớp chuỗi theo StringComparison.Ordinal như MlController.
    /// </summary>
    /// <param name="isAuthenticated">User hiện tại đã đăng nhập chưa.</param>
    /// <param name="userId">Id user hiện tại (từ JWT), có thể là Guest khi chưa đăng nhập.</param>
    /// <param name="syncKeyHeader">Giá trị header X-Sync-Key, có thể null.</param>
    /// <param name="configuredSyncKey">SyncApiKey trong cấu hình MarketData, có thể rỗng.</param>
    public static bool IsAllowed(
        bool isAuthenticated,
        Guid userId,
        string? syncKeyHeader,
        string? configuredSyncKey)
    {
        if (isAuthenticated && userId == AdminUser.Id)
            return true;

        return !string.IsNullOrWhiteSpace(configuredSyncKey)
            && string.Equals(syncKeyHeader, configuredSyncKey, StringComparison.Ordinal);
    }
}
