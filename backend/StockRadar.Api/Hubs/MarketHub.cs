using Microsoft.AspNetCore.SignalR;

namespace StockRadar.Api.Hubs;

public sealed class MarketHub : Hub
{
    public const string QuotesUpdated = "QuotesUpdated";
    public const string IndexUpdated = "IndexUpdated";
    public const string RadarUpdated = "RadarUpdated";
    public const string AlertCreated = "AlertCreated";
    public const string TradeEventCreated = "TradeEventCreated";

    public async Task Subscribe(string[] symbols)
    {
        foreach (var symbol in symbols.Where(s => !string.IsNullOrWhiteSpace(s)))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(symbol));
    }

    public async Task Unsubscribe(string[] symbols)
    {
        foreach (var symbol in symbols.Where(s => !string.IsNullOrWhiteSpace(s)))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(symbol));
    }

    internal static string GroupName(string symbol) =>
        $"symbol:{symbol.Trim().ToUpperInvariant()}";
}
