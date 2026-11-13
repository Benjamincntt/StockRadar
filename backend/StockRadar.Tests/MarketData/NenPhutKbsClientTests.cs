using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Tests.MarketData;

/// <summary>
/// Spec 017 T005 — hàm KBS cho nến phút không cắt nến, tách được "rỗng" khỏi "lỗi",
/// và <c>FetchAsync</c> cũ vẫn cắt 300 nến như trước (chốt chặn hồi quy cho biểu đồ app).
/// </summary>
public sealed class NenPhutKbsClientTests
{
    private static readonly DateOnly TuNgay = new(2026, 11, 5);
    private static readonly DateOnly DenNgay = new(2026, 11, 12);

    private sealed class KbsGia : HttpMessageHandler
    {
        public HttpStatusCode Ma { get; set; } = HttpStatusCode.OK;
        public string? NoiDung { get; set; }
        public List<string> CacUrl { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CacUrl.Add(request.RequestUri!.AbsoluteUri);
            var phanHoi = new HttpResponseMessage(Ma);
            if (NoiDung is not null)
                phanHoi.Content = new StringContent(NoiDung, Encoding.UTF8, "application/json");
            return Task.FromResult(phanHoi);
        }
    }

    private static (KbsHistoryClient Client, KbsGia Gia) TaoClient()
    {
        var gia = new KbsGia();
        var http = new HttpClient(gia) { BaseAddress = new Uri("https://kbbuddywts.kbsec.com.vn/") };
        var client = new KbsHistoryClient(
            http,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<KbsHistoryClient>.Instance);
        return (client, gia);
    }

    private static string JsonNhieuNen(int soNen, string key = "data_1P")
    {
        var dong = Enumerable.Range(0, soNen).Select(i =>
        {
            var phut = 9 * 60 + 15 + i;
            return $"{{\"t\":\"2026-11-12T{phut / 60:00}:{phut % 60:00}:00\","
                   + "\"o\":30100,\"h\":30150,\"l\":30050,\"c\":30120,\"v\":12400}";
        });
        return $"{{\"{key}\":[{string.Join(',', dong)}]}}";
    }

    [Fact]
    public async Task FetchMinuteRange_TraDu500Nen_KhongCatTheo300()
    {
        var (client, gia) = TaoClient();
        gia.NoiDung = JsonNhieuNen(500);

        var ketQua = await client.FetchMinuteRangeAsync("HPG", TuNgay, DenNgay);

        Assert.Equal(KbsTrangThaiTai.Ok, ketQua.TrangThai);
        Assert.Equal(500, ketQua.Bars.Count);
        Assert.Null(ketQua.Loi);
        Assert.Contains(gia.CacUrl, url => url.Contains("data_1P") && url.Contains("sdate=05-11-2026"));
    }

    [Fact]
    public async Task FetchMinuteRange_Http500_TraHttpError_KhongPhaiRong()
    {
        var (client, gia) = TaoClient();
        gia.Ma = HttpStatusCode.InternalServerError;

        var ketQua = await client.FetchMinuteRangeAsync("HPG", TuNgay, DenNgay);

        Assert.Equal(KbsTrangThaiTai.HttpError, ketQua.TrangThai);
        Assert.Empty(ketQua.Bars);
        Assert.Contains("500", ketQua.Loi);
    }

    [Fact]
    public async Task FetchMinuteRange_MangRong_TraRong_KhacHttpError()
    {
        var (client, gia) = TaoClient();
        gia.NoiDung = "{\"data_1P\":[]}";

        var ketQua = await client.FetchMinuteRangeAsync("HPG", TuNgay, DenNgay);

        Assert.Equal(KbsTrangThaiTai.Rong, ketQua.TrangThai);
        Assert.Empty(ketQua.Bars);
        Assert.Null(ketQua.Loi);
    }

    [Fact]
    public async Task FetchMinuteRange_ThieuKeyData1P_TraThieuKey()
    {
        var (client, gia) = TaoClient();
        gia.NoiDung = "{\"data_day\":[]}";

        var ketQua = await client.FetchMinuteRangeAsync("HPG", TuNgay, DenNgay);

        Assert.Equal(KbsTrangThaiTai.ThieuKey, ketQua.TrangThai);
        Assert.Empty(ketQua.Bars);
    }

    [Fact]
    public async Task FetchMinuteRange_KhongDungCache_GoiHaiLanRaHaiRequest()
    {
        var (client, gia) = TaoClient();
        gia.NoiDung = JsonNhieuNen(226);

        await client.FetchMinuteRangeAsync("HPG", TuNgay, DenNgay);
        await client.FetchMinuteRangeAsync("HPG", TuNgay, DenNgay);

        Assert.Equal(2, gia.CacUrl.Count);
    }

    [Fact]
    public async Task FetchAsync_Cu_VanCatCon300Nen_DeBieuDoAppKhongHoiQuy()
    {
        var (client, gia) = TaoClient();
        gia.NoiDung = JsonNhieuNen(500);

        var bars = await client.FetchAsync("HPG", "1m");

        Assert.Equal(300, bars.Count);
    }

    [Fact]
    public async Task FetchRangeAsync_Cu_KhiLoi_VanTraMangRong()
    {
        var (client, gia) = TaoClient();
        gia.Ma = HttpStatusCode.InternalServerError;

        var bars = await client.FetchDailyHistoryAsync("HPG", TuNgay, DenNgay);

        Assert.Empty(bars);
    }
}
