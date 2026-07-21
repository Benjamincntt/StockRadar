using System.Security.Cryptography;
using System.Text;

namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>
/// SetupId deterministic (spec §4.2): cùng <c>(Symbol, CapitulationDate, StrategyVersion)</c> → cùng Guid.
/// Đáy mới (CapitulationDate đổi) → setup mới.
/// </summary>
public static class ReversalBounceSetupId
{
    public static Guid Compute(string symbol, DateOnly? capitulationDate, string strategyVersion)
    {
        var key = $"{symbol}|{capitulationDate?.ToString("yyyy-MM-dd") ?? "0001-01-01"}|{strategyVersion}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}
