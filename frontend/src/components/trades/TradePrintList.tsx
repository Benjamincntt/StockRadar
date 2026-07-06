import { Link } from "react-router-dom";
import type { TradeEvent } from "@/types";
import { formatDateTime, formatPrice } from "@/lib/utils";
import { useLiveQuote, useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { useThemeTokens } from "@/context/ThemeContext";
import {
  labelAccentColor,
  tradeLabelVi,
  VN_CEILING_CHANGE_PCT,
} from "@/lib/tradeLabels";

function formatVolume(volume: number) {
  if (volume >= 1_000_000) return `${(volume / 1_000_000).toFixed(2)}M`;
  if (volume >= 1_000) return `${(volume / 1_000).toFixed(1)}K`;
  return volume.toLocaleString("vi-VN");
}

function formatTradeValue(valueVnd: number) {
  if (valueVnd >= 1_000_000_000) return `${(valueVnd / 1_000_000_000).toFixed(2)} tỷ`;
  if (valueVnd >= 1_000_000) return `${(valueVnd / 1_000_000).toFixed(0)} tr`;
  return `${valueVnd.toLocaleString("vi-VN")}đ`;
}

function formatNet(vol: number) {
  const sign = vol > 0 ? "+" : "";
  if (Math.abs(vol) >= 1_000_000) return `${sign}${(vol / 1_000_000).toFixed(1)}M`;
  if (Math.abs(vol) >= 1_000) return `${sign}${(vol / 1_000).toFixed(0)}K`;
  return `${sign}${vol}`;
}

function TradeEventRow({ trade }: { trade: TradeEvent }) {
  const theme = useThemeTokens();
  const live = useLiveQuote(trade.symbol);
  const accentKind = labelAccentColor(trade.label);
  const tint =
    accentKind === "green"
      ? theme.greenSoft
      : accentKind === "red"
        ? theme.redSoft
        : theme.neutralBg;
  const accent =
    accentKind === "green"
      ? theme.green
      : accentKind === "red"
        ? theme.red
        : theme.textMuted;
  const label = tradeLabelVi(trade.label);
  const changePct = live?.changePercent;
  const atCeiling = changePct != null && changePct >= VN_CEILING_CHANGE_PCT;
  const priceColor = atCeiling ? theme.secondary : theme.text;

  return (
    <Link
      to={`/stocks/${trade.symbol}`}
      className="flex items-center gap-3 rounded-2xl px-3 py-2.5 transition-colors hover:opacity-90"
      style={{ backgroundColor: tint }}
    >
      <span
        className="w-14 shrink-0 text-center text-[10px] font-bold leading-tight"
        style={{ color: accent }}
      >
        {label}
        {trade.isAggregated ? (
          <span className="mt-0.5 block text-[9px] font-normal opacity-80">Gom lô</span>
        ) : null}
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-bold text-on-surface">{trade.symbol}</p>
        <p className="text-xs text-on-surface-variant">
          {formatVolume(trade.volume)} CP · {formatTradeValue(trade.valueVnd)} ·{" "}
          {formatDateTime(trade.at)}
        </p>
        {trade.sessionForeignNet !== 0 ? (
          <p className="text-[10px] text-on-surface-variant">
            NN phiên {formatNet(trade.sessionForeignNet)} CP
            {trade.sessionPressure !== 0
              ? ` · Áp lực ${trade.sessionPressure > 0 ? "+" : ""}${trade.sessionPressure}`
              : ""}
          </p>
        ) : null}
      </div>
      <p
        className="shrink-0 text-right font-mono text-sm font-semibold"
        style={{ color: priceColor }}
      >
        {formatPrice(trade.price)}
      </p>
    </Link>
  );
}

interface TradePrintListProps {
  trades: TradeEvent[];
  loading: boolean;
}

export function TradePrintList({ trades, loading }: TradePrintListProps) {
  useSymbolSubscriptions(trades.map((t) => t.symbol));

  if (loading && trades.length === 0) {
    return <p className="py-8 text-center text-sm text-on-surface-variant">Đang tải...</p>;
  }

  if (!loading && trades.length === 0) {
    return (
      <p className="py-8 text-center text-sm text-on-surface-variant">
        Chưa có lô lớn trong phiên. Cần quét đang chạy và KL/GTGD đạt ngưỡng (≥25K CP, ≥500M).
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {trades.map((trade) => (
        <TradeEventRow
          key={`${trade.symbol}-${trade.at}-${trade.volume}-${trade.price}`}
          trade={trade}
        />
      ))}
    </div>
  );
}
