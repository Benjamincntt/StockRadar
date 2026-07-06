import { useThemeTokens } from "@/context/ThemeContext";
import { getTradeStateStyle, type ResolvedTradeState } from "@/lib/tradeState";

export function TradeStateBadge({
  trade,
  showReason = false,
}: {
  trade: ResolvedTradeState;
  showReason?: boolean;
}) {
  const theme = useThemeTokens();
  const style = getTradeStateStyle(trade.state, theme);

  return (
    <div className="min-w-0">
      <span
        className="inline-flex rounded-full px-2 py-0.5 text-[10px] font-bold"
        style={{ backgroundColor: style.pillBg, color: style.pillColor }}
      >
        {trade.label}
      </span>
      {showReason && trade.reason && (
        <p className="mt-0.5 line-clamp-2 text-[9px] leading-snug text-on-surface-variant">
          {trade.reason}
        </p>
      )}
    </div>
  );
}
