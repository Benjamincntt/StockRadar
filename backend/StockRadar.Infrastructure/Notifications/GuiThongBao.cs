using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;

namespace StockRadar.Infrastructure.Notifications;

internal sealed class GuiThongBao : IGuiThongBao
{
    private readonly ITelegramNotifier telegram;
    private readonly INguiTinDay day;
    private readonly PhamViDbThongBao phamViDb;
    private readonly TimeProvider dongHo;
    private readonly ILogger<GuiThongBao> ghiLog;
    private readonly TimeSpan hanTelegram;
    private readonly TimeSpan hanDay;
    private readonly List<TinDaGom> boDem = [];

    /// <summary>Creates the scoped notification dispatcher.</summary>
    /// <param name="telegram">Existing Telegram channel.</param>
    /// <param name="day">Push channel.</param>
    /// <param name="phamViDb">Isolated operation-owned database scopes and the shared capacity limit.</param>
    /// <param name="dongHo">Clock controlling independent channel deadlines.</param>
    /// <param name="cauHinhDay">Push waiting budget.</param>
    /// <param name="cauHinhTelegram">Telegram waiting budget.</param>
    /// <param name="ghiLog">Sanitized dispatcher event logger.</param>
    public GuiThongBao(ITelegramNotifier telegram, INguiTinDay day, PhamViDbThongBao phamViDb,
        IOptions<PushNotifyOptions> cauHinhDay, IOptions<TelegramNotifyOptions> cauHinhTelegram,
        ILogger<GuiThongBao> ghiLog, TimeProvider dongHo)
    {
        this.telegram = telegram;
        this.day = day;
        this.phamViDb = phamViDb;
        this.dongHo = dongHo;
        this.ghiLog = ghiLog;
        hanTelegram = TimeSpan.FromSeconds(cauHinhTelegram.Value.TimeoutGiay is > 0 and <= 3600
            ? cauHinhTelegram.Value.TimeoutGiay : 10);
        hanDay = TimeSpan.FromSeconds(cauHinhDay.Value.TimeoutGiay is > 0 and <= 3600
            ? cauHinhDay.Value.TimeoutGiay : 2);
    }

    /// <inheritdoc />
    public void DatTinV3(LoaiTinV3 loai, string symbol, LoaiKichBanV3 kichBan, string telegramHtml) =>
        boDem.Add(new(loai, symbol, kichBan, null, telegramHtml));

    /// <inheritdoc />
    public void DatTinHeThong(string tieuDeDay, string noiDung) =>
        boDem.Add(new(null, null, null, tieuDeDay, noiDung));

    /// <inheritdoc />
    public async Task GuiTatCaAsync(CancellationToken cancellationToken = default)
    {
        var cacTin = boDem.ToArray();
        boDem.Clear();
        if (cacTin.Length == 0) return;

        await Task.WhenAll(GuiVongTelegramAsync(cacTin, cancellationToken), GuiVongDayAsync(cacTin, cancellationToken));
        if (cancellationToken.IsCancellationRequested)
            ghiLog.LogInformation("Notification batch canceled; remaining buffered messages discarded.");
    }

    /// <inheritdoc />
    public Task GuiHeThongAsync(string tieuDeDay, string noiDung, CancellationToken cancellationToken = default)
    {
        DatTinHeThong(tieuDeDay, noiDung);
        return GuiTatCaAsync(cancellationToken);
    }

    private async Task GuiVongTelegramAsync(IReadOnlyList<TinDaGom> cacTin, CancellationToken huy)
    {
        foreach (var tin in cacTin)
        {
            if (huy.IsCancellationRequested) break;
            await GuiTelegramAsync(tin, huy);
        }
    }

    private async Task GuiVongDayAsync(IReadOnlyList<TinDaGom> cacTin, CancellationToken huy)
    {
        var dot = new TrangThaiDot();
        foreach (var tin in cacTin)
        {
            if (huy.IsCancellationRequested || dot.BoKenhDay) break;
            await GuiDayAsync(tin, dot, huy);
        }
    }

    /// <summary>Attempts Telegram within its own budget without exposing transport exceptions.</summary>
    /// <param name="tin">Unmodified buffered content.</param>
    /// <param name="huyNhip">Original tick cancellation, distinguished from a channel timeout.</param>
    /// <returns>Completion of the isolated Telegram attempt.</returns>
    private async Task GuiTelegramAsync(TinDaGom tin, CancellationToken huyNhip)
    {
        if (huyNhip.IsCancellationRequested) return;
        using var quaHan = new CancellationTokenSource(hanTelegram, dongHo);
        using var huyKenh = CancellationTokenSource.CreateLinkedTokenSource(huyNhip, quaHan.Token);
        var batDau = dongHo.GetTimestamp();
        try
        {
            await ChoCoHanAsync(telegram.SendAsync(tin.NoiDungTelegram, huyKenh.Token,
                tin.LoaiV3.HasValue ? "HTML" : null), huyKenh.Token);
        }
        catch (Exception loi)
        {
            // SECURITY: Raw transport errors can expose the bot URL and let a log reader control the Telegram bot.
            if (!huyNhip.IsCancellationRequested)
                GhiLoi(LogLevel.Warning, "gui", loi, batDau, 1, tin.Symbol);
        }
    }

    /// <summary>Queries once, sends push, and cleans invalid tokens within the push channel budget.</summary>
    /// <param name="tin">Buffered content to map to a push payload.</param>
    /// <param name="dot">Token cache and circuit state owned exclusively by this flush.</param>
    /// <param name="huyNhip">Original tick cancellation.</param>
    /// <returns>Completion of the isolated push attempt.</returns>
    private async Task GuiDayAsync(TinDaGom tin, TrangThaiDot dot, CancellationToken huyNhip)
    {
        if (dot.BoKenhDay || huyNhip.IsCancellationRequested) return;
        using var quaHan = new CancellationTokenSource(hanDay, dongHo);
        using var huyKenh = CancellationTokenSource.CreateLinkedTokenSource(huyNhip, quaHan.Token);
        var batDau = dongHo.GetTimestamp();
        var giaiDoan = "gui";
        var soMay = dot.CacToken?.Count ?? 0;
        try
        {
            DayTinNhan tinDay;
            try
            {
                tinDay = tin.LoaiV3 is { } loai
                    ? new DayTinNhan(V3PushFormatter.TieuDe(loai, tin.Symbol!, tin.KichBan!.Value),
                        V3PushFormatter.NoiDung(tin.NoiDungTelegram), "v3-signal", tin.Symbol, loai)
                    : new DayTinNhan(tin.TieuDeHeThong!, tin.NoiDungTelegram, "he-thong", null);
                if (!KenhDayTinFcm.KiemTraTin(tinDay)) throw new ArgumentException("Invalid push payload.");
            }
            catch (ArgumentException loi)
            {
                GhiLoi(LogLevel.Warning, "gui", loi, batDau, soMay, tin.Symbol);
                return;
            }

            if (!day.DangHoatDong)
            {
                giaiDoan = "khoi_tao";
                await ChoCoHanAsync(day.GuiAsync([], tinDay, huyKenh.Token), huyKenh.Token);
                return;
            }

            giaiDoan = "doc_token";
            if (dot.CacToken is null)
                dot.CacToken = (await ChoKetQuaAsync(phamViDb.ChayAsync(
                    (kho, huy) => kho.LayTokenCacMayAsync(huy), huyKenh.Token), huyKenh.Token)).ToList();
            huyKenh.Token.ThrowIfCancellationRequested();
            soMay = dot.CacToken.Count;
            if (soMay == 0) return;

            giaiDoan = "gui";
            var ketQua = await ChoKetQuaAsync(day.GuiAsync(dot.CacToken.ToArray(), tinDay, huyKenh.Token), huyKenh.Token);
            var tokenHong = ketQua.TokenHong.Intersect(dot.CacToken, StringComparer.Ordinal).ToArray();
            if (tokenHong.Length == 0) return;

            dot.CacToken.RemoveAll(token => tokenHong.Contains(token, StringComparer.Ordinal));
            giaiDoan = "don_token";
            try
            {
                await ChoCoHanAsync(phamViDb.ChayAsync(async (kho, huy) =>
                {
                    await kho.XoaCacTokenAsync(tokenHong, huy);
                    if (day is KenhDayTinFcm kenhFcm)
                        kenhFcm.QuenCanhBaoTokenDaXoa(tokenHong);
                    return true;
                }, huyKenh.Token), huyKenh.Token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception loi)
            {
                if (!huyNhip.IsCancellationRequested) CanhBaoDot(dot, giaiDoan, loi, batDau, soMay, tin.Symbol);
            }
        }
        catch (Exception loi)
        {
            if (huyNhip.IsCancellationRequested) return;
            dot.BoKenhDay = true;
            if (loi is LoiKenhDayTinFcm loiKhoiTao) giaiDoan = loiKhoiTao.GiaiDoan;
            else if (giaiDoan == "gui" && day is KenhDayTinFcm { DangKhoiTao: true }) giaiDoan = "khoi_tao";
            CanhBaoDot(dot, giaiDoan, loi, batDau, soMay, tin.Symbol);
        }
    }

    private void CanhBaoDot(TrangThaiDot dot, string giaiDoan, Exception loi, long batDau, int soMay, string? symbol)
    {
        var muc = dot.DaCanhBao ? LogLevel.Debug : LogLevel.Warning;
        dot.DaCanhBao = true;
        GhiLoi(muc, giaiDoan, loi, batDau, soMay, symbol);
    }

    private void GhiLoi(LogLevel muc, string giaiDoan, Exception loi, long batDau, int soMay, string? symbol)
    {
        var daLamSach = loi as LoiKenhDayTinFcm ?? LoiKenhDayTinFcm.Tao(loi);
        var loaiLoi = loi switch
        {
            LoiKenhDayTinFcm => daLamSach.LoaiLoi,
            OperationCanceledException or TimeoutException => "qua_han",
            FirebaseMessagingException { MessagingErrorCode: MessagingErrorCode.ThirdPartyAuthError or MessagingErrorCode.SenderIdMismatch } => "xac_thuc",
            ArgumentException or FormatException => "payload",
            _ => "mang"
        };
        ghiLog.Log(muc,
            "Notification failure: GiaiDoan={GiaiDoan}, LoaiLoi={LoaiLoi}, KieuLoi={KieuLoi}, MessagingErrorCode={MessagingErrorCode}, ThoiGianMs={ThoiGianMs}, SoMayNhan={SoMayNhan}, Symbol={Symbol}",
            giaiDoan, loaiLoi, daLamSach.KieuLoi, daLamSach.MessagingErrorCode,
            dongHo.GetElapsedTime(batDau).TotalMilliseconds, soMay, symbol);
    }

    /// <summary>Bounds waiting and observes late faults from cancellation-unaware transports.</summary>
    /// <param name="viec">An already-started asynchronous operation.</param>
    /// <param name="huy">The linked channel cancellation token.</param>
    /// <returns>Completion within the supplied cancellation budget.</returns>
    /// <exception cref="OperationCanceledException">The channel or tick was canceled.</exception>
    /// <exception cref="Exception">The operation failed before the waiting budget expired.</exception>
    private static async Task ChoCoHanAsync(Task viec, CancellationToken huy)
    {
        try
        {
            await viec.WaitAsync(huy);
        }
        catch (OperationCanceledException)
        {
            // DO-NOT-CHANGE: A timed-out send may already be accepted; retrying it can duplicate the notification.
            _ = viec.ContinueWith(daXong => { _ = daXong.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }

    /// <summary>Obtains an operation result after bounded waiting.</summary>
    /// <typeparam name="TKetQua">Operation result type.</typeparam>
    /// <param name="viec">An already-started asynchronous query or send.</param>
    /// <param name="huy">The linked channel cancellation token.</param>
    /// <returns>The completed operation's result.</returns>
    /// <exception cref="OperationCanceledException">The waiting budget was canceled.</exception>
    /// <exception cref="Exception">The underlying operation failed.</exception>
    private static async Task<TKetQua> ChoKetQuaAsync<TKetQua>(Task<TKetQua> viec, CancellationToken huy)
    {
        await ChoCoHanAsync(viec, huy);
        return await viec;
    }

    private sealed class TrangThaiDot
    {
        public List<string>? CacToken { get; set; }
        public bool BoKenhDay { get; set; }
        public bool DaCanhBao { get; set; }
    }
}
