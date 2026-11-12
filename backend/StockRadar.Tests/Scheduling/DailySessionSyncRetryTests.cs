using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Tests.Scheduling;

/// <summary>
/// Spec 018 Q4 — trạng thái thử lại của Job 2: lần chính ghi các mã KBS không trả nến;
/// lần thử lại 15:15/15:30 chỉ lấy đúng mã thiếu và không lọc lại danh sách mã;
/// đủ nến thì bỏ qua; chưa có trạng thái (khởi động lại) thì chạy đầy đủ, vẫn không lọc lại.
/// </summary>
public sealed class DailySessionSyncRetryTests
{
    private static readonly DateTimeOffset LucPhienGia = new(2026, 11, 12, 8, 20, 0, TimeSpan.Zero);

    private static readonly string[] ToanUniverse = ["HPG", "FPT", "TCB", "VCB", "DRI"];

    private sealed class DongHoPhienGia : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => LucPhienGia;
    }

    /// <summary>Bảng giá KBS giả: ghi nhận từng lô mã gửi lên, không trả dòng cho các mã trong MaKhongTra; chỉ số luôn 404.</summary>
    private sealed class BoXuLyBangGiaGia : HttpMessageHandler
    {
        public HashSet<string> MaKhongTra { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string[]> CacLoDaGui { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post || request.Content is null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            var codes = JsonSerializer.Deserialize<JsonElement>(body)
                .GetProperty("code").GetString()!
                .Split(',', StringSplitOptions.RemoveEmptyEntries);
            CacLoDaGui.Add(codes);

            var rows = codes
                .Where(c => !MaKhongTra.Contains(c))
                .Select(c => $"{{\"SB\":\"{c}\",\"CP\":15000,\"TT\":1000}}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[{string.Join(',', rows)}]", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class KhoMaJobGia(IReadOnlyList<string> active) : IJobStockRepository
    {
        public Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(active);

        public Task<IReadOnlyList<string>> GetInactiveSymbolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
            IReadOnlyList<string> symbols, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MarketBreadthStats> GetBreadthStatsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Stock>> GetAllForUniverseScreeningAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Stock>> GetBySymbolsAsync(
            IReadOnlyList<string> symbols, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class DichVuSyncGia : IMarketSyncService
    {
        public List<MarketSyncRequest> CacYeuCau { get; } = [];

        public Task<MarketSyncResultDto> ApplyAsync(MarketSyncRequest request, CancellationToken cancellationToken = default)
        {
            CacYeuCau.Add(request);
            return Task.FromResult(new MarketSyncResultDto(request.Quotes.Count, request.Index is not null, DateTime.UtcNow));
        }
    }

    private sealed class RescreenGia : IUniverseRescreenService
    {
        public int SoLanChay { get; private set; }

        public Task<UniverseRescreenResultDto> RunAsync(CancellationToken cancellationToken = default)
        {
            SoLanChay++;
            return Task.FromResult(new UniverseRescreenResultDto(0, 0, 0, DateTime.UtcNow));
        }
    }

    private sealed class NgoaiPhienHan(
        Job2TrangThaiNgay state,
        BoXuLyBangGiaGia handler,
        DichVuSyncGia sync,
        RescreenGia rescreen)
    {
        public DailySessionSyncRunner Tao(IReadOnlyList<string> active)
        {
            var http = new HttpClient(handler);
            var boardClient = new KbsPriceBoardClient(http, NullLogger<KbsPriceBoardClient>.Instance);
            var indexClient = new KbsIndexClient(
                new HttpClient(handler),
                new MemoryCache(Options.Create(new MemoryCacheOptions())),
                NullLogger<KbsIndexClient>.Instance);

            return new DailySessionSyncRunner(
                new KhoMaJobGia(active),
                boardClient,
                indexClient,
                sync,
                rescreen,
                Options.Create(new MarketJobsOptions()),
                state,
                new DongHoPhienGia(),
                NullLogger<DailySessionSyncRunner>.Instance);
        }
    }

    private readonly Job2TrangThaiNgay _state = new();
    private readonly BoXuLyBangGiaGia _handler = new();
    private readonly DichVuSyncGia _sync = new();
    private readonly RescreenGia _rescreen = new();

    [Fact]
    public async Task LanChinh_ThieuBaMa_GhiTrangThaiDungBaMaVaLocLaiMotLan()
    {
        _handler.MaKhongTra.UnionWith(["TCB", "VCB", "DRI"]);
        var runner = CreateRunner();

        await runner.RunAsync(CancellationToken.None, CheDoJob2.LanChinh);

        Assert.Equal(["DRI", "TCB", "VCB"], _state.MaConThieu.Order(StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.Equal(1, _state.LanThu);
        Assert.Equal(1, _rescreen.SoLanChay);
        Assert.All(_sync.CacYeuCau, yeuCau => Assert.True(yeuCau.SauDongCua));
    }

    [Fact]
    public async Task ThuLai_CoTrangThaiChiLayDungMaThieuKhongLocLai()
    {
        _handler.MaKhongTra.UnionWith(["TCB", "VCB", "DRI"]);
        var runner = CreateRunner();
        await runner.RunAsync(CancellationToken.None, CheDoJob2.LanChinh);

        _handler.CacLoDaGui.Clear();
        _handler.MaKhongTra.Clear();
        await runner.RunAsync(CancellationToken.None, CheDoJob2.ThuLai);

        Assert.Equal(["DRI", "TCB", "VCB"], _handler.CacLoDaGui[^1].Order(StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.Empty(_state.MaConThieu);
        Assert.Equal(2, _state.LanThu);
        Assert.Equal(1, _rescreen.SoLanChay);
    }

    [Fact]
    public async Task ThuLai_DuNen_BoQuaKhongGoiKBS()
    {
        var runner = CreateRunner();
        await runner.RunAsync(CancellationToken.None, CheDoJob2.LanChinh);

        _handler.CacLoDaGui.Clear();
        var result = await runner.RunAsync(CancellationToken.None, CheDoJob2.ThuLai);

        Assert.Empty(_handler.CacLoDaGui);
        Assert.Equal(0, result.SymbolsSynced);
        Assert.Equal(1, _rescreen.SoLanChay);
    }

    [Fact]
    public async Task ThuLai_ChuaCoTrangThai_ChanDayDuKhongLocLai()
    {
        var runner = CreateRunner();

        var result = await runner.RunAsync(CancellationToken.None, CheDoJob2.ThuLai);

        Assert.Equal(
            ToanUniverse.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            _handler.CacLoDaGui[^1].Order(StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.Equal(0, _rescreen.SoLanChay);
        Assert.Equal(1, _state.LanThu);
        Assert.Equal(ToanUniverse.Length - _handler.MaKhongTra.Count, result.SymbolsSynced);
    }

    private DailySessionSyncRunner CreateRunner() =>
        new NgoaiPhienHan(_state, _handler, _sync, _rescreen).Tao(ToanUniverse);
}
