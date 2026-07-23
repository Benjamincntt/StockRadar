# Tối ưu tốc độ API chi tiết cổ phiếu (`GET /api/v1/stocks/{symbol}`)

> Đo thực tế trên server production (02/07/2026, `curl` từ chính server vào `127.0.0.1:5281`):
>
> | Request | Thời gian | Kích thước response |
> |---|---|---|
> | `GET /stocks/HPG` (cache nguội) | **7.9s** | 243 KB |
> | `GET /stocks/HPG` (cache ấm) | **1.8 – 3.7s** | 243 KB |
> | `GET /stocks/HPG/chart?interval=1D` | 0.26s | 236 KB |
>
> Chưa tính thời gian tải 243 KB (không nén) qua mạng di động — thực tế trên app còn chậm hơn.

## Tổng quan luồng hiện tại

`StocksController.GetDetail` → `StockService.GetDetailAsync` ([backend/StockRadar.Application/Services/StockService.cs](../backend/StockRadar.Application/Services/StockService.cs)):

1. `stocks.GetBySymbolAsync(symbol)` — query bảng `Stocks`, deserialize toàn bộ `HistoryJson` (~450 nến).
2. `jobStocks.GetBySymbolAsync(symbol)` — **query lại đúng dòng đó lần nữa** (cùng bảng, cùng repo).
3. `smartMoneyEval.BuildContextAsync()` — **thủ phạm chính**: khi cache hết hạn (60s), tải **toàn bộ ~300 mã × full HistoryJson** từ DB + deserialize (~vài chục MB), rồi tính sector snapshot cho cả universe. Mất nhiều giây.
4. Tính điểm 25 tiêu chí, base price, signals, swing decision (vài query nhỏ) — nhanh, chấp nhận được.
5. Trả về DTO chứa **toàn bộ history** → 243 KB JSON, không nén.

## Nguyên nhân gốc, theo mức độ ảnh hưởng

### 1. `BuildContextAsync` dùng repo KHÔNG cache (nặng nhất)

`SmartMoneyEvaluationService` inject `IJobStockRepository`:

```11:13:backend/StockRadar.Application/Services/SmartMoneyEvaluationService.cs
public sealed class SmartMoneyEvaluationService(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
```

Nhưng trong DI, `IJobStockRepository` trỏ thẳng vào `EfStockRepository` (không cache), chỉ `IStockRepository` mới được bọc `CachedStockRepository`:

```130:135:backend/StockRadar.Infrastructure/DependencyInjection.cs
        services.AddScoped<EfStockRepository>();
        services.AddScoped<IJobStockRepository>(sp => sp.GetRequiredService<EfStockRepository>());
        services.AddScoped<IStockRepository>(sp => new CachedStockRepository(
            sp.GetRequiredService<EfStockRepository>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CacheOptions>>()));
```

Hệ quả: cứ mỗi 60s (TTL `smartmoney:context`), request đầu tiên phải:
- `SELECT *` toàn bộ bảng `Stocks` (mỗi dòng chứa `HistoryJson` ~50–100 KB) → truyền vài chục MB từ SQL Server.
- Deserialize ~300 list nến.
- Chạy `selector.BuildContext` trên cả universe.

Đây chính là con số **7.9s** đo được.

### 2. Query trùng lặp trong `GetDetailAsync`

`IStockRepository.GetBySymbolAsync` và `IJobStockRepository.GetBySymbolAsync` là **cùng một implementation trên cùng bảng** — 2 query giống hệt nhau, mỗi query kéo về full `HistoryJson`.

### 3. Response 243 KB không nén

- Backend không bật `ResponseCompression`.
- nginx có `gzip on;` nhưng `gzip_proxied` và `gzip_types` đang comment → **JSON đi qua proxy không được nén** (mặc định nginx chỉ nén `text/html` và không nén response từ upstream).
- DTO trả về **toàn bộ history** (`match.History.Select(DtoMapper.ToDto)`) — app chỉ cần ~250 nến để vẽ chart 1D.

### 4. Cache context quá ngắn và "thundering herd"

TTL 60s nghĩa là gần như mọi lần user mở app đều dính cache miss. `IMemoryCache.GetOrCreateAsync` không khóa — nhiều request đồng thời cùng miss sẽ **cùng build context song song**, nhân đôi/ba tải DB + CPU.

---

## Các bước sửa (theo thứ tự làm)

### Bước 1 — Cho `SmartMoneyEvaluationService` dùng repo có cache

Cách rẻ nhất, không đổi schema: dùng chung cache `stocks:all` giữa job và API. Trong `backend/StockRadar.Infrastructure/DependencyInjection.cs`, bọc luôn `IJobStockRepository` bằng decorator cache:

```csharp
services.AddScoped<EfStockRepository>();
services.AddScoped<IJobStockRepository>(sp => new CachedStockRepository(
    sp.GetRequiredService<EfStockRepository>(),
    sp.GetRequiredService<IMemoryCache>(),
    sp.GetRequiredService<IOptions<CacheOptions>>()));
services.AddScoped<IStockRepository>(sp =>
    (IStockRepository)sp.GetRequiredService<IJobStockRepository>());
```

Lưu ý: `CachedStockRepository` hiện chỉ implement `IStockRepository`; thêm `IJobStockRepository` vào danh sách interface của nó (2 interface trùng chữ ký nên không phải viết thêm method):

```csharp
internal sealed class CachedStockRepository(...) : IStockRepository, IJobStockRepository
```

Job 2 sau khi ghi dữ liệu mới đã gọi `CacheInvalidation.InvalidateStocks` nên dữ liệu không bị cũ quá TTL.

### Bước 2 — Cache `GetBySymbolAsync` theo symbol

Trong `CachedStockRepository`, cache từng mã (TTL dùng chung `StockListSeconds`):

```csharp
public async Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken ct = default)
{
    if (!options.Value.Enabled)
        return await inner.GetBySymbolAsync(symbol, ct);

    var key = $"stocks:sym:{symbol.ToUpperInvariant()}";
    return await cache.GetOrCreateAsync(key, async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow =
            TimeSpan.FromSeconds(options.Value.StockListSeconds);
        return await inner.GetBySymbolAsync(symbol, ct);
    });
}
```

Và trong `CacheInvalidation.InvalidateStocks` không xoá được key theo mã bằng `Remove` đơn lẻ — dùng `CancellationTokenSource` làm "token invalidation":

```csharp
internal static class CacheInvalidation
{
    private static CancellationTokenSource _stocksCts = new();

    public static CancellationToken StocksToken => _stocksCts.Token;

    public static void InvalidateStocks(IMemoryCache cache)
    {
        cache.Remove("stocks:all");
        var old = Interlocked.Exchange(ref _stocksCts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }
}
```

Rồi khi tạo entry: `entry.AddExpirationToken(new CancellationChangeToken(CacheInvalidation.StocksToken));`

### Bước 3 — Bỏ query trùng trong `StockService.GetDetailAsync`

`stocks` và `jobStocks` đọc cùng bảng. Chỉ cần 1 lần:

```csharp
public async Task<StockDetailDto?> GetDetailAsync(string symbol, CancellationToken ct = default)
{
    var match = await jobStocks.GetBySymbolAsync(symbol, ct);
    if (match is null)
        return null;
    // xoá biến `stock`, các chỗ dùng `stock` đổi sang `match`
```

(Tương tự trong `GetChartAsync`: đang gọi `stocks.GetBySymbolAsync` rồi lại `jobStocks.GetBySymbolAsync` khi interval 1D.)

### Bước 4 — Bật nén response (giảm 243 KB → ~25 KB)

Cách chắc chắn nhất là nén ngay tại Kestrel, không phụ thuộc cấu hình nginx. Trong `backend/StockRadar.Api/Program.cs`:

```csharp
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o =>
    o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o =>
    o.Level = CompressionLevel.Fastest);

// Đặt TRƯỚC MapControllers, càng sớm càng tốt trong pipeline:
app.UseResponseCompression();
```

Client Flutter dùng `package:http` gửi `Accept-Encoding: gzip` mặc định và tự giải nén — không cần sửa app.

Tuỳ chọn thêm ở nginx (`/etc/nginx/nginx.conf`), bỏ comment và thêm types:

```nginx
gzip on;
gzip_vary on;
gzip_proxied any;
gzip_comp_level 5;
gzip_min_length 1024;
gzip_types application/json text/plain application/javascript text/css;
```

Rồi `nginx -t && systemctl reload nginx`. (Nếu đã nén ở Kestrel thì nginx tự bỏ qua.)

### Bước 5 — Cắt bớt history trong detail DTO

App chỉ dùng `detail.history` để vẽ chart 1D (`mobile/lib/screens/stock_detail_screen.dart`), ~250 nến (1 năm) là thừa đủ. Trong `StockService.GetDetailAsync`:

```csharp
private const int MaxHistoryBarsInDetail = 250;
// ...
var historyDto = match.History
    .Skip(Math.Max(0, match.History.Count - MaxHistoryBarsInDetail))
    .Select(DtoMapper.ToDto)
    .ToList();
```

Giảm thêm ~40–45% payload trước khi nén. **Lưu ý:** chỉ cắt DTO trả ra, không cắt `match.History` vì các bước tính điểm (`ScoreIndicators`, `AnalyzeBasePrice`, MA200…) cần đủ dữ liệu.

### Bước 6 — Tăng TTL cache context + chống thundering herd

Trong `backend/StockRadar.Api/appsettings.Production.json` thêm:

```json
"Cache": {
  "StockListSeconds": 300,
  "SmartMoneyContextSeconds": 300
}
```

Dữ liệu OHLCV chỉ đổi khi Job 2 sync (đã invalidate cache chủ động), nên TTL 5 phút không làm dữ liệu cũ.

Chống nhiều request cùng build context trong `SmartMoneyEvaluationService`:

```csharp
private static readonly SemaphoreSlim ContextLock = new(1, 1);

public async Task<SmartMoneyMarketContext> BuildContextAsync(CancellationToken ct = default)
{
    var cfg = cacheOptions.Value;
    if (!cfg.Enabled)
        return await BuildContextCoreAsync(ct);

    if (cache.TryGetValue<SmartMoneyMarketContext>(ContextCacheKey, out var cached) && cached is not null)
        return cached;

    await ContextLock.WaitAsync(ct);
    try
    {
        if (cache.TryGetValue<SmartMoneyMarketContext>(ContextCacheKey, out cached) && cached is not null)
            return cached;

        var ctx = await BuildContextCoreAsync(ct);
        cache.Set(ContextCacheKey, ctx, TimeSpan.FromSeconds(cfg.SmartMoneyContextSeconds));
        return ctx;
    }
    finally
    {
        ContextLock.Release();
    }
}
```

### Bước 7 (tuỳ chọn, ăn nốt cache miss) — Warm context nền

Để user **không bao giờ** phải trả giá cache miss, thêm một `BackgroundService` refresh context định kỳ:

```csharp
// backend/StockRadar.Api/Background/SmartMoneyContextWarmer.cs
public sealed class SmartMoneyContextWarmer(
    IServiceScopeFactory scopeFactory,
    ILogger<SmartMoneyContextWarmer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(4));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var eval = scope.ServiceProvider.GetRequiredService<SmartMoneyEvaluationService>();
                await eval.BuildContextAsync(stoppingToken); // nạp lại cache trước khi TTL 5' hết
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Warm SmartMoney context thất bại");
            }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
```

Đăng ký trong `Program.cs`: `builder.Services.AddHostedService<SmartMoneyContextWarmer>();`

Chu kỳ 4 phút < TTL 5 phút → cache luôn ấm, mọi request chỉ tốn phần tính điểm cho 1 mã (~100–200ms).

### Bước 8 (phía app) — Bỏ gọi chart 1D thừa

App đã tự dựng chart 1D từ `detail.history`, chỉ gọi `/chart` khi đổi interval — giữ nguyên. Nếu sau này muốn nhanh hơn nữa: cache `StockDetail` theo symbol trong app (TTL 1–2 phút) để back/forward không gọi lại API.

---

## Kỳ vọng sau khi làm xong

| Chỉ số | Trước | Sau |
|---|---|---|
| Detail, cache nguội | 7.9s | không còn xảy ra (warmer) hoặc ~2s (chỉ 1 lần/5 phút) |
| Detail, cache ấm | 1.8–3.7s | **~0.1–0.3s** |
| Payload qua mạng | 243 KB | **~15–25 KB** (cắt history + Brotli/gzip) |

## Cách kiểm chứng sau khi deploy

```bash
# Trên server:
for i in 1 2 3; do
  curl -so /dev/null -w 'detail: %{time_total}s %{size_download}B\n' \
    -H 'Accept-Encoding: gzip' http://127.0.0.1:5281/api/v1/stocks/HPG
done
```

Mục tiêu: lần nào cũng < 0.3s và size < 30 KB.

## Ghi chú vận hành

- Trong lúc job backfill tiêu chí (`criteria-backfill`) đang chạy, CPU server bị chiếm ~90% → mọi API đều chậm hơn bình thường. Số đo ở trên được lấy khi backfill đang chạy; sau khi backfill xong, kết quả sẽ tốt hơn nữa.
- Server chỉ có ít RAM: cache `stocks:all` (~300 mã full history) chiếm ước tính 50–150 MB bộ nhớ managed. TTL 5 phút + 1 bản duy nhất là chấp nhận được; tránh tạo thêm nhiều bản sao list này ở các cache key khác.
