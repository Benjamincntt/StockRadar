using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

internal sealed class BoThuLogFcm : ILogger<KenhDayTinFcm>
{
    internal sealed record DongLog(LogLevel Muc, string NoiDung, Exception? Loi,
        IReadOnlyList<KeyValuePair<string, object?>> ThuocTinh);

    internal ConcurrentQueue<DongLog> CacDong { get; } = new();
    internal static DayTinNhan TinThu => new("THỬ · JUICE", "Offline notification", "v3-signal", null);

    /// <summary>Creates options pointing to an existing noncredential assembly for injected initialization only.</summary>
    /// <returns>Enabled options that never require reading or creating a credential file.</returns>
    internal static IOptions<PushNotifyOptions> CauHinhGia() => Options.Create(new PushNotifyOptions
    {
        Firebase = new CauHinhFirebase { CredentialPath = typeof(BoThuLogFcm).Assembly.Location }
    });

    /// <summary>Counts captured events at the requested severity.</summary>
    /// <param name="muc">The severity to count.</param>
    /// <returns>The number of matching events.</returns>
    internal int Dem(LogLevel muc) => CacDong.Count(dong => dong.Muc == muc);

    /// <summary>Verifies that neither rendered nor structured logging exposes synthetic sensitive values.</summary>
    /// <param name="cacGiaTriCam">Synthetic values that must not appear in any event.</param>
    internal void KiemTraKhongLo(params string[] cacGiaTriCam)
    {
        Assert.All(CacDong, dong =>
        {
            Assert.Null(dong.Loi);
            Assert.All(dong.ThuocTinh, cap =>
            {
                Assert.Contains(cap.Key, new[] { "{OriginalFormat}", "GiaiDoan", "LoaiLoi", "KieuLoi", "MessagingErrorCode",
                    "ThoiGianMs", "SoMayNhan", "SoDaGui", "TokenHongSoLuong", "TokenDuoi", "Symbol" });
                if (cap.Key == "GiaiDoan")
                    Assert.Contains(cap.Value, new[] { "khoi_tao", "doc_token", "gui", "don_token" });
                if (cap.Key == "LoaiLoi")
                    Assert.Contains(cap.Value, new[] { "qua_han", "mang", "xac_thuc", "payload", "token", "token_hoac_payload" });
                if (cap.Key == "TokenDuoi")
                    Assert.True(cap.Value is string duoi && (duoi == "[redacted]" || duoi.Length == 5));
            });
            foreach (var giaTri in cacGiaTriCam)
            {
                Assert.False(dong.NoiDung.Contains(giaTri, StringComparison.Ordinal), "Sensitive log value withheld.");
                Assert.All(dong.ThuocTinh, thuocTinh =>
                    Assert.False(thuocTinh.Value?.ToString()?.Contains(giaTri, StringComparison.Ordinal) == true,
                        "Sensitive structured log value withheld."));
            }
        });
    }

    /// <summary>Uses no logging scope resources.</summary>
    /// <typeparam name="TState">The framework scope state type.</typeparam>
    /// <param name="trangThai">The scope state.</param>
    /// <returns>No scope handle.</returns>
    public IDisposable? BeginScope<TState>(TState trangThai) where TState : notnull => null;

    /// <summary>Enables every concrete log severity for assertions.</summary>
    /// <param name="muc">The requested severity.</param>
    /// <returns>Whether the severity is a concrete event level.</returns>
    public bool IsEnabled(LogLevel muc) => muc != LogLevel.None;

    /// <summary>Captures rendered text, structured state, and the exception separately.</summary>
    /// <typeparam name="TState">The framework event state type.</typeparam>
    /// <param name="muc">The event severity.</param>
    /// <param name="maSuKien">The framework event identifier.</param>
    /// <param name="trangThai">The structured event state.</param>
    /// <param name="loi">The optional exception, which production must never supply.</param>
    /// <param name="dinhDang">The framework message formatter.</param>
    public void Log<TState>(LogLevel muc, EventId maSuKien, TState trangThai,
        Exception? loi, Func<TState, Exception?, string> dinhDang)
    {
        var thuocTinh = trangThai is IEnumerable<KeyValuePair<string, object?>> cacThuocTinh
            ? cacThuocTinh.ToArray() : [];
        CacDong.Enqueue(new DongLog(muc, dinhDang(trangThai, loi), loi, thuocTinh));
    }
}
