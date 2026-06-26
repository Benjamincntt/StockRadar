using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class KbsPriceBoardClient(HttpClient http, ILogger<KbsPriceBoardClient> logger)
{
    private const string StockIssUrl =
        "https://kbbuddywts.kbsec.com.vn/iis-server/investment/stock/iss";

    public async Task<IReadOnlyList<KbsBoardRow>> FetchAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default)
    {
        if (symbols.Count == 0)
            return [];

        var payload = new { code = string.Join(",", symbols.Select(s => s.Trim().ToUpperInvariant())) };
        using var request = new HttpRequestMessage(HttpMethod.Post, StockIssUrl)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("x-lang", "vi");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("KBS price_board HTTP {Status}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(ParseRow)
                    .Where(r => r is not null)
                    .Cast<KbsBoardRow>()
                    .ToList();
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var single = ParseRow(doc.RootElement);
                return single is null ? [] : [single];
            }

            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Khong lay duoc bang gia KBS.");
            return [];
        }
    }

    private const decimal PriceScale = 1000m;

    private static KbsBoardRow? ParseRow(JsonElement row)
    {
        var symbol = GetString(row, "SB");
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        var close = ScalePrice(GetDecimal(row, "CP"));
        if (close <= 0)
            close = ScalePrice(GetDecimal(row, "RE"));
        if (close <= 0)
            return null;

        var open = ScalePrice(GetDecimal(row, "OP"));
        if (open <= 0) open = close;
        var high = ScalePrice(GetDecimal(row, "HI"));
        if (high <= 0) high = close;
        var low = ScalePrice(GetDecimal(row, "LO"));
        if (low <= 0) low = close;

        return new KbsBoardRow(
            symbol.Trim().ToUpperInvariant(),
            open,
            high,
            low,
            close,
            (long)GetDecimal(row, "TT"),
            GetDecimal(row, "CHP"),
            ScalePrice(GetDecimal(row, "B1")),
            ScalePrice(GetDecimal(row, "B2")),
            ScalePrice(GetDecimal(row, "B3")),
            ScalePrice(GetDecimal(row, "S1")),
            ScalePrice(GetDecimal(row, "S2")),
            ScalePrice(GetDecimal(row, "S3")),
            (long)GetDecimal(row, "V1"),
            (long)GetDecimal(row, "V2"),
            (long)GetDecimal(row, "V3"),
            (long)GetDecimal(row, "U1"),
            (long)GetDecimal(row, "U2"),
            (long)GetDecimal(row, "U3"),
            (long)GetDecimal(row, "FB"),
            (long)GetDecimal(row, "FS"),
            (long)GetDecimal(row, "CV"),
            (long)GetDecimal(row, "PTQ"),
            (long)GetDecimal(row, "PTV"));
    }

    private static decimal ScalePrice(decimal raw) =>
        raw >= 1000m ? raw / PriceScale : raw;

    private static string? GetString(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal GetDecimal(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
            return 0m;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0m
        };
    }

    internal sealed record KbsBoardRow(
        string Symbol,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long SessionVolume,
        decimal ChangePercent,
        decimal BidPrice1,
        decimal BidPrice2,
        decimal BidPrice3,
        decimal AskPrice1,
        decimal AskPrice2,
        decimal AskPrice3,
        long BidVolume1,
        long BidVolume2,
        long BidVolume3,
        long AskVolume1,
        long AskVolume2,
        long AskVolume3,
        long ForeignBuyVolume,
        long ForeignSellVolume,
        long ProprietaryVolume,
        long PutThroughVolume,
        long PutThroughValue);
}
