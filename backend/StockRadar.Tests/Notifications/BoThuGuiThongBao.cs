using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockRadar.Infrastructure;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.Notifications;

internal sealed class BoThuGuiThongBao
{
    internal KenhTelegramGia Telegram { get; } = new();
    internal KenhDayGia Day { get; } = new();
    internal KhoThietBiGia Kho { get; } = new();
    internal LogGuiThongBao Log { get; } = new();
    internal PushNotifyOptions CauHinhDay { get; } = new();
    internal TelegramNotifyOptions CauHinhTelegram { get; } = new();
    internal TimeProvider DongHo { get; set; } = TimeProvider.System;

    /// <summary>Creates the real dispatcher with offline channels and an optional real InMemory repository.</summary>
    /// <param name="kho">Repository override for integration assertions.</param>
    /// <param name="day">Push override for missing-configuration assertions.</param>
    /// <returns>A fresh scoped dispatcher.</returns>
    internal GuiThongBao Tao(IThietBiNhanThongBaoRepository? kho = null, INguiTinDay? day = null)
    {
        var dichVu = TaoDichVu();
        dichVu.AddSingleton(kho ?? Kho);
        dichVu.AddSingleton(day ?? Day);
        return (GuiThongBao)dichVu.BuildServiceProvider().GetRequiredService<IGuiThongBao>();
    }

    internal ServiceCollection TaoDichVu()
    {
        var dichVu = new ServiceCollection();
        dichVu.AddLogging();
        dichVu.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "unused-offline-test"
        }).Build());
        dichVu.AddSingleton<ITelegramNotifier>(Telegram);
        dichVu.AddSingleton<INguiTinDay>(Day);
        dichVu.AddSingleton<IThietBiNhanThongBaoRepository>(Kho);
        dichVu.AddSingleton(Options.Create(CauHinhDay));
        dichVu.AddSingleton(Options.Create(CauHinhTelegram));
        dichVu.AddSingleton(DongHo);
        dichVu.AddSingleton<ILogger<GuiThongBao>>(Log);
        return dichVu;
    }

    /// <summary>Buffers ordered synthetic V3 messages.</summary>
    /// <param name="gui">Dispatcher under test.</param>
    /// <param name="soTin">Number of messages to buffer.</param>
    internal static void Gom(IGuiThongBao gui, int soTin = 1)
    {
        for (var viTri = 0; viTri < soTin; viTri++)
            gui.DatTinV3(LoaiTinV3.Mua, "HPG", LoaiKichBanV3.N1, $"<b>tin-{viTri}</b>");
    }

    internal sealed class KenhTelegramGia : ITelegramNotifier
    {
        internal List<(string NoiDung, string? DinhDang)> Tin { get; } = [];
        internal Func<CancellationToken, Task> KhiGui { get; set; } = _ => Task.CompletedTask;

        /// <inheritdoc />
        public Task SendAsync(string message, CancellationToken cancellationToken = default, string? parseMode = null)
        {
            Tin.Add((message, parseMode));
            return KhiGui(cancellationToken);
        }
    }

    internal sealed class KenhDayGia : INguiTinDay
    {
        public bool DangHoatDong { get; set; } = true;
        internal List<(string[] CacToken, DayTinNhan NoiDung)> Tin { get; } = [];
        internal Func<CancellationToken, Task<DayTinKetQua>> KhiGui { get; set; } =
            _ => Task.FromResult(new DayTinKetQua(1, []));

        /// <inheritdoc />
        public Task<DayTinKetQua> GuiAsync(IReadOnlyList<string> tokens, DayTinNhan tin,
            CancellationToken cancellationToken = default)
        {
            Tin.Add((tokens.ToArray(), tin));
            return KhiGui(cancellationToken);
        }
    }

    internal sealed class KhoThietBiGia : IThietBiNhanThongBaoRepository
    {
        internal int SoLanDoc { get; private set; }
        internal List<string> CacToken { get; } = ["token-gia-thiet-bi-00001"];
        internal Func<CancellationToken, Task>? KhiDoc { get; set; }
        internal bool LoiXoa { get; set; }
        internal Func<CancellationToken, Task>? KhiXoa { get; set; }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> LayTokenCacMayAsync(CancellationToken cancellationToken = default)
        {
            SoLanDoc++;
            if (KhiDoc is not null) await KhiDoc(cancellationToken);
            return CacToken.ToArray();
        }

        /// <inheritdoc />
        public async Task XoaCacTokenAsync(IReadOnlyList<string> tokens, CancellationToken cancellationToken = default)
        {
            if (KhiXoa is not null) await KhiXoa(cancellationToken);
            if (LoiXoa) throw new InvalidOperationException("synthetic-sensitive-cleanup-error");
            CacToken.RemoveAll(tokens.Contains);
        }

        /// <inheritdoc />
        public Task DangKyAsync(Guid userId, string token, string nenTang, string? tokenCu = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task XoaAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> LayTokenTheoUserAsync(Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    internal sealed class LogGuiThongBao : ILogger<GuiThongBao>
    {
        internal ConcurrentQueue<(LogLevel Muc, string NoiDung, Exception? Loi, IReadOnlyDictionary<string, object?> Truong)> CacDong { get; } = new();

        /// <summary>Counts events of a specific severity.</summary>
        /// <param name="muc">Severity to inspect.</param>
        /// <returns>Matching event count.</returns>
        internal int Dem(LogLevel muc) => CacDong.Count(dong => dong.Muc == muc);

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            CacDong.Enqueue((logLevel, formatter(state, exception), exception,
                state is IEnumerable<KeyValuePair<string, object?>> cacTruong
                    ? cacTruong.ToDictionary(cap => cap.Key, cap => cap.Value) : new Dictionary<string, object?>()));
    }
}
