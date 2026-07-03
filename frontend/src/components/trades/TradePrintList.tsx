import { Link } from "react-router-dom";
import type { TradePrint } from "@/types";
import { formatDateTime, formatPrice } from "@/lib/utils";
import { useThemeTokens } from "@/context/ThemeContext";

function formatVolume(volume: number) {
  if (volume >= 1_000_000) return `${(volume / 1_000_000).toFixed(2)}M`;
  if (volume >= 1_000) return `${(volume / 1_000).toFixed(1)}K`;
  return volume.toLocaleString("vi-VN");
}

function formatTradeValue(price: number, volume: number) {
  const vnd = price * 1000 * volume;
  if (vnd >= 1_000_000_000) return `${(vnd / 1_000_000_000).toFixed(2)} tỷ`;
  if (vnd >= 1_000_000) return `${(vnd / 1_000_000).toFixed(0)} tr`;
  return `${vnd.toLocaleString("vi-VN")}đ`;
}

function TradePrintRow({ trade }: { trade: TradePrint }) {
  const theme = useThemeTokens();
  const isBuy = trade.side === "Buy";
  const tint = isBuy ? theme.greenSoft : theme.redSoft;
  const accent = isBuy ? theme.green : theme.red;
  const label = isBuy ? "MUA" : "BÁN";

  return (
    <Link
      to={`/stocks/${trade.symbol}`}
      className="flex items-center gap-3 rounded-2xl px-3 py-2.5 transition-colors hover:opacity-90"
      style={{ backgroundColor: tint }}
    >
      <span
        className="w-11 shrink-0 text-center text-xs font-bold"
        style={{ color: accent }}
      >
        {label}
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-bold text-on-surface">{trade.symbol}</p>
        <p className="text-xs text-on-surface-variant">
          {formatVolume(trade.volume)} CP · {formatTradeValue(trade.price, trade.volume)} · {formatDateTime(trade.at)}
        </p>
      </div>
      <p className="shrink-0 text-right font-mono text-sm font-semibold text-on-surface">
        {formatPrice(trade.price)}
      </p>
    </Link>
  );
}

interface TradePrintListProps {
  trades: TradePrint[];
  loading: boolean;
}

export function TradePrintList({ trades, loading }: TradePrintListProps) {
  if (loading && trades.length === 0) {
    return <p className="py-8 text-center text-sm text-on-surface-variant">Đang tải...</p>;
  }

  if (!loading && trades.length === 0) {
    return (
      <p className="py-8 text-center text-sm text-on-surface-variant">
        Chưa có lệnh block lớn trong phiên. Cần quét đang chạy và KL/GTGD đạt ngưỡng (≥25K CP, ≥500M).
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {trades.map((trade) => (
        <TradePrintRow
          key={`${trade.symbol}-${trade.at}-${trade.volume}-${trade.price}`}
          trade={trade}
        />
      ))}
    </div>
  );
}
