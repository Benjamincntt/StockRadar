using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;

namespace StockRadar.Infrastructure.Notifications;

public sealed class KenhDayTinFcm : INguiTinDay, IDisposable
{
    private readonly object khoa = new();
    private readonly ConcurrentDictionary<string, byte> tokenDaCanhBao = new(StringComparer.Ordinal);
    private readonly TimeSpan khoangChoKhoiTao;
    private readonly string duongDan;
    private readonly ILogger<KenhDayTinFcm> ghiLog;
    private readonly TimeProvider dongHo;
    private readonly System.Action? donDepGia;
    private readonly Func<Func<Message, CancellationToken, Task<string>>> khoiTao;
    private Task<KetQuaKhoiTao>? viecKhoiTao;
    private FirebaseApp? ungDung;
    private LoiKenhDayTinFcm? loiKhoiTao;
    private DateTimeOffset thuLaiTu;
    private bool loiCoDinh;
    private bool coTaiNguyenGia;
    private bool daHuy;
    private int daCanhBao;
    private int soCongViec;

    /// <summary>Constructs the production channel without initializing Firebase.</summary>
    /// <param name="cauHinh">Push configuration.</param>
    /// <param name="ghiLog">The channel logger.</param>
    /// <param name="dongHo">The cooldown clock; system time is used when omitted.</param>
    public KenhDayTinFcm(IOptions<PushNotifyOptions> cauHinh, ILogger<KenhDayTinFcm> ghiLog, TimeProvider? dongHo = null)
        : this(cauHinh, ghiLog, null, dongHo) { }

    /// <summary>Constructs a channel with a synchronous factory executed only by the owned initialization worker.</summary>
    /// <param name="cauHinh">Push configuration.</param>
    /// <param name="ghiLog">The channel logger.</param>
    /// <param name="khoiTao">The offline sender factory; null selects production initialization.</param>
    /// <param name="dongHo">The clock controlling initialization cooldown.</param>
    /// <param name="donDep">An offline ownership probe, released once after successful initialization and all work completes.</param>
    internal KenhDayTinFcm(IOptions<PushNotifyOptions> cauHinh, ILogger<KenhDayTinFcm> ghiLog,
        Func<Func<Message, CancellationToken, Task<string>>>? khoiTao, TimeProvider? dongHo = null, System.Action? donDep = null)
    {
        this.dongHo = dongHo ?? TimeProvider.System;
        donDepGia = donDep;
        duongDan = cauHinh.Value.Firebase?.CredentialPath ?? "";
        this.ghiLog = ghiLog;
        this.khoiTao = khoiTao ?? KhoiTaoFirebase;
        var giayCho = cauHinh.Value.KhoiTaoLaiSauGiay;
        khoangChoKhoiTao = TimeSpan.FromSeconds(Math.Max(1, giayCho));
        loiCoDinh = !cauHinh.Value.Enabled || giayCho <= 0 || string.IsNullOrWhiteSpace(duongDan) || !File.Exists(duongDan);
    }

    /// <summary>Reports availability without initialization or logging.</summary>
    public bool DangHoatDong
    {
        get
        {
            lock (khoa)
                return !daHuy && !loiCoDinh;
        }
    }

    internal bool DangKhoiTao
    {
        get { lock (khoa) return viecKhoiTao is { IsCompleted: false }; }
    }

    /// <summary>Sends each device message once and returns unregistered devices only.</summary>
    /// <param name="cacToken">Device tokens to attempt sequentially.</param>
    /// <param name="tin">The notification content.</param>
    /// <param name="huy">Caller cancellation.</param>
    /// <returns>Success count and invalid registrations.</returns>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    /// <exception cref="Exception">A transport failure occurred.</exception>
    /// <exception cref="ObjectDisposedException">The channel has been disposed.</exception>
    public async Task<DayTinKetQua> GuiAsync(IReadOnlyList<string> cacToken, DayTinNhan tin,
        CancellationToken huy = default)
    {
        huy.ThrowIfCancellationRequested();
        lock (khoa)
            ObjectDisposedException.ThrowIf(daHuy, this);
        if (!KiemTraTin(tin))
        {
            ghiLog.LogWarning(
                "FCM message skipped: GiaiDoan={GiaiDoan}, LoaiLoi={LoaiLoi}, KieuLoi={KieuLoi}, Symbol={Symbol}",
                "gui", "payload", nameof(ArgumentException), tin?.Symbol);
            return new DayTinKetQua(0, []);
        }
        Task<KetQuaKhoiTao>? viec;
        lock (khoa)
        {
            ObjectDisposedException.ThrowIf(daHuy, this);
            viec = loiCoDinh ? null : LayViecKhoiTao();
            if (viec is not null)
                soCongViec++;
        }
        if (viec is null)
        {
            CanhBaoKenhTat();
            return new DayTinKetQua(0, []);
        }

        var batDau = dongHo.GetTimestamp();
        try
        {
            var ketQua = await viec.WaitAsync(huy).ConfigureAwait(false);
            huy.ThrowIfCancellationRequested();
            lock (khoa)
                ObjectDisposedException.ThrowIf(daHuy, this);
            if (ketQua.Loi is { LoaiLoi: "mang" or "qua_han" } loiTamThoi)
                throw loiTamThoi.SaoChep();
            if (ketQua.Gui is not { } gui)
            {
                CanhBaoKenhTat();
                return new DayTinKetQua(0, []);
            }
            var soDaGui = 0;
            var tokenHong = new List<string>();
            foreach (var token in cacToken)
            {
                huy.ThrowIfCancellationRequested();
                try
                {
                    await gui(TaoTinNhan(token, tin), huy).ConfigureAwait(false);
                    soDaGui++;
                }
                catch (FirebaseMessagingException loi) when (loi.MessagingErrorCode is
                    MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    if (loi.MessagingErrorCode == MessagingErrorCode.Unregistered)
                        tokenHong.Add(token);
                    // SECURITY: Full tokens in logs allow another caller to remove a device registration; retain only its suffix.
                    var duoiToken = token.Length > 5 ? token[^5..] : "[redacted]";
                    var muc = tokenDaCanhBao.TryAdd(token, 0) ? LogLevel.Warning : LogLevel.Debug;
                    var loaiLoi = loi.MessagingErrorCode == MessagingErrorCode.Unregistered ? "token" : "token_hoac_payload";
                    ghiLog.Log(muc, "FCM device rejection: GiaiDoan={GiaiDoan}, LoaiLoi={LoaiLoi}, MessagingErrorCode={MessagingErrorCode}, TokenDuoi={TokenDuoi}",
                        "gui", loaiLoi, loi.MessagingErrorCode, duoiToken);
                }
            }

            huy.ThrowIfCancellationRequested();
            ghiLog.LogInformation(
                "FCM delivery: GiaiDoan={GiaiDoan}, SoMayNhan={SoMayNhan}, SoDaGui={SoDaGui}, TokenHongSoLuong={TokenHongSoLuong}, ThoiGianMs={ThoiGianMs}",
                "gui", cacToken.Count, soDaGui, tokenHong.Count, dongHo.GetElapsedTime(batDau).TotalMilliseconds);
            return new DayTinKetQua(soDaGui, tokenHong);
        }
        finally
        {
            KetThucCongViec();
        }
    }

    internal void QuenCanhBaoTokenDaXoa(IReadOnlyList<string> cacToken)
    {
        // DO-NOT-CHANGE: Clear only after successful DB removal; clearing on rejection repeats warnings when cleanup fails.
        foreach (var maToken in cacToken)
            tokenDaCanhBao.TryRemove(maToken, out _);
    }

    /// <summary>Validates content and routing without initialization, logging, mutation, or exceptions for invalid input.</summary>
    internal static bool KiemTraTin(DayTinNhan tin)
    {
        if (tin is null || string.IsNullOrWhiteSpace(tin.TieuDe) || string.IsNullOrWhiteSpace(tin.NoiDung) ||
            tin.TieuDe.Length > 4096 || tin.NoiDung.Length > 4096)
            return false;
        var kenh = tin.Kenh;
        if (kenh == "he-thong")
        {
            if (tin.Symbol is not null || tin.LoaiV3 is not null)
                return false;
        }
        else if (kenh == "v3-signal")
        {
            if (tin.LoaiV3 is null)
            {
                if (tin.Symbol is not null)
                    return false;
            }
            else if (!Enum.IsDefined(tin.LoaiV3.Value) || tin.Symbol is not { Length: >= 1 and <= 16 } ma ||
                ma[0] is < 'A' or > 'Z' || !ma.All(kyTu => kyTu is >= 'A' and <= 'Z' or >= '0' and <= '9'))
                return false;
        }
        else
            return false;
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            if (utf8.GetByteCount(tin.TieuDe) + utf8.GetByteCount(tin.NoiDung) > 4096)
                return false;
            return DemByteTai(TaoTinNhan("", tin)) <= 4096;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// Uses the conservative UTF-8 serialized size of all actual Android/data fields, keys, values, and JSON punctuation.
    /// Includes the message envelope, priority, TTL, channel, title, and full body; excludes the routing token and HTTP/auth envelope.
    /// FCM counts keys and values, with a 4096-byte non-topic limit: https://firebase.google.com/docs/cloud-messaging/error-codes.
    /// JSON escaping can overcount decoded content; it cannot undercount these payload fields.
    /// </summary>
    private static int DemByteTai(Message tin)
    {
        var android = tin.Android;
        var tai = new Dictionary<string, object>
        {
            ["message"] = new Dictionary<string, object>
            {
                ["android"] = new Dictionary<string, object>
                {
                    ["priority"] = android.Priority == Priority.High ? "high" : "normal",
                    ["ttl"] = android.TimeToLive == TimeSpan.FromHours(1) ? "3600s" : "43200s",
                    ["notification"] = new Dictionary<string, string>
                    {
                        ["channel_id"] = android.Notification.ChannelId,
                        ["title"] = android.Notification.Title,
                        ["body"] = android.Notification.Body
                    }
                },
                ["data"] = tin.Data
            }
        };
        return JsonSerializer.SerializeToUtf8Bytes(tai,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Length;
    }

    /// <summary>Builds the Android and navigation payload for one device.</summary>
    /// <param name="token">The target device registration.</param>
    /// <param name="tin">Notification content and routing metadata.</param>
    /// <returns>The per-device SDK message.</returns>
    /// <exception cref="ArgumentException">A V3 signal has no stock symbol.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The V3 notification kind is unsupported.</exception>
    internal static Message TaoTinNhan(string token, DayTinNhan tin)
    {
        var kenh = tin.Kenh;
        var heThong = kenh == "he-thong";
        var duLieu = new Dictionary<string, string>();
        if (heThong)
            duLieu["nguon"] = "he_thong";
        else if (tin.LoaiV3 is { } loai)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tin.Symbol);
            duLieu["symbol"] = tin.Symbol;
            duLieu["loaiTin"] = loai switch
            {
                LoaiTinV3.Mua => "mua",
                LoaiTinV3.Ban => "ban",
                LoaiTinV3.HuyMua => "huy_mua",
                LoaiTinV3.AtoN3 => "ato_n3",
                _ => throw new ArgumentOutOfRangeException(nameof(tin))
            };
            duLieu["nguon"] = "v3";
        }
        else
            duLieu["nguon"] = "thu";

        return new Message
        {
            Token = token,
            Android = new AndroidConfig
            {
                Priority = heThong ? Priority.Normal : Priority.High,
                TimeToLive = TimeSpan.FromHours(heThong ? 12 : 1),
                Notification = new AndroidNotification
                {
                    ChannelId = heThong ? "he-thong" : "v3-signal",
                    Title = tin.TieuDe,
                    Body = tin.NoiDung
                }
            },
            Data = duLieu
        };
    }

    /// <summary>Selects one shared worker under the lifetime lock; only a later send can trigger a cooldown retry.</summary>
    private Task<KetQuaKhoiTao> LayViecKhoiTao()
    {
        if (viecKhoiTao is not null && (!viecKhoiTao.IsCompleted || loiKhoiTao is null || dongHo.GetUtcNow() < thuLaiTu))
            return viecKhoiTao;
        soCongViec++;
        loiKhoiTao = null;
        // DO-NOT-CHANGE: The worker carries its own lifetime lease and no caller token, so a canceled wait cannot abort shared initialization.
        viecKhoiTao = Task.Run(KhoiTaoAnToan);
        return viecKhoiTao;
    }

    /// <summary>Runs synchronous credential/app work off the caller stack and retains only sanitized failure data.</summary>
    private KetQuaKhoiTao KhoiTaoAnToan()
    {
        try
        {
            var gui = khoiTao() ?? throw new InvalidOperationException("FCM sender unavailable.");
            lock (khoa)
                coTaiNguyenGia = donDepGia is not null;
            return new KetQuaKhoiTao(gui, null);
        }
        catch (Exception loi)
        {
            var daLamSach = LoiKenhDayTinFcm.Tao(loi);
            lock (khoa)
            {
                loiKhoiTao = daLamSach;
                loiCoDinh = daLamSach.LoaiLoi is not ("mang" or "qua_han");
                thuLaiTu = dongHo.GetUtcNow() + khoangChoKhoiTao;
                DonTaiNguyen();
            }
            return new KetQuaKhoiTao(null, daLamSach);
        }
        finally
        {
            KetThucCongViec();
        }
    }

    private sealed record KetQuaKhoiTao(Func<Message, CancellationToken, Task<string>>? Gui, LoiKenhDayTinFcm? Loi);

    /// <summary>Creates a uniquely named, owned Firebase app from service-account credentials on first send.</summary>
    /// <returns>The SDK's cancellation-aware per-message send delegate, with no adapter retries.</returns>
    /// <exception cref="Exception">Credential loading or Firebase initialization failed.</exception>
    private Func<Message, CancellationToken, Task<string>> KhoiTaoFirebase()
    {
        if (new FileInfo(duongDan).Length == 0)
            throw new FormatException("FCM credential configuration is empty.");
        var xacThuc = CredentialFactory.FromFile(duongDan, "service_account");
        ungDung = FirebaseApp.Create(new AppOptions { Credential = xacThuc, HttpClientFactory = new BoTaoHttpFcm() },
            "stockradar-day-" + Guid.NewGuid().ToString("N"));
        var guiSdk = FirebaseMessaging.GetMessaging(ungDung);
        return (tin, huy) => guiSdk.SendAsync(tin, huy);
    }

    /// <summary>Warns exactly once per channel lifetime, only when an unavailable channel is sent through.</summary>
    private void CanhBaoKenhTat()
    {
        var muc = Interlocked.Exchange(ref daCanhBao, 1) == 0 ? LogLevel.Warning : LogLevel.Debug;
        ghiLog.Log(muc, "FCM channel unavailable: GiaiDoan={GiaiDoan}, LoaiLoi={LoaiLoi}, KieuLoi={KieuLoi}, MessagingErrorCode={MessagingErrorCode}",
            "khoi_tao", "xac_thuc", loiKhoiTao?.KieuLoi ?? nameof(InvalidOperationException), loiKhoiTao?.MessagingErrorCode);
    }

    private void KetThucCongViec()
    {
        lock (khoa)
        {
            soCongViec--;
            if (daHuy && soCongViec == 0)
                DonTaiNguyen();
        }
    }

    /// <summary>Releases an owned app exactly once, under the lifetime lock, only when no work can use it.</summary>
    private void DonTaiNguyen()
    {
        var canDon = ungDung;
        ungDung = null;
        var donGia = coTaiNguyenGia;
        coTaiNguyenGia = false;
        try
        {
            canDon?.Delete();
            if (donGia)
                donDepGia?.Invoke();
        }
        catch (Exception loi)
        {
            var daLamSach = LoiKenhDayTinFcm.Tao(loi);
            ghiLog.LogWarning("FCM cleanup failed: GiaiDoan={GiaiDoan}, LoaiLoi={LoaiLoi}, KieuLoi={KieuLoi}",
                "khoi_tao", daLamSach.LoaiLoi, daLamSach.KieuLoi);
        }
    }

    /// <summary>Defers app deletion until the initialization worker and all in-flight sends finish.</summary>
    public void Dispose()
    {
        lock (khoa)
        {
            if (daHuy)
                return;
            daHuy = true;
            if (soCongViec == 0)
                DonTaiNguyen();
        }
    }
}
