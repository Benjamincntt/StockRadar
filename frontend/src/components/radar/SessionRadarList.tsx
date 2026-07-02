import { Link } from "react-router-dom";
import type { RadarLiveItem, RadarLiveSnapshot } from "@/types";
import { LiveMiniQuote } from "@/components/ui/LiveMiniQuote";
import { formatDateTime, signalLabelVi } from "@/lib/utils";
import { useThemeTokens } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

function formatVolumeRatio(value: number) {
  return `${value.toFixed(1)}×`;
}

function SessionRadarRow({ item }: { item: RadarLiveItem }) {
  return (
    <Link
      to={`/stocks/${item.symbol}`}
      className="flex items-start gap-3 rounded-2xl bg-surface-low px-3 py-2.5 transition-colors hover:bg-surface-high"
    >
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
          <span className="font-bold text-on-surface">{item.symbol}</span>
          {item.sector && (
            <span className="truncate text-xs text-on-surface-variant">{item.sector}</span>
          )}
        </div>
        {item.name && (
          <p className="mt-0.5 truncate text-xs text-on-surface-variant">{item.name}</p>
        )}
        <div className="mt-1.5 flex flex-wrap gap-1">
          {item.signals.map((signal) => (
            <span
              key={signal}
              className="rounded-md bg-surface-high px-1.5 py-0.5 text-[10px] font-medium text-on-surface-variant"
            >
              {signalLabelVi(signal)}
            </span>
          ))}
        </div>
        <p className="mt-1 text-[10px] text-on-surface-variant">
          KL {formatVolumeRatio(item.volumeRatio)}
          {item.relativeStrength !== 0 && (
            <>
              {" · "}
              RS {item.relativeStrength > 0 ? "+" : ""}
              {item.relativeStrength.toFixed(1)}%
            </>
          )}
        </p>
      </div>
      <div className="shrink-0 text-right">
        <LiveMiniQuote
          symbol={item.symbol}
          fallbackPrice={item.price}
          fallbackChangePercent={item.changePercent}
        />
      </div>
    </Link>
  );
}

export function SessionRadarStatusLine({
  snapshot,
  loading,
  className,
}: {
  snapshot: RadarLiveSnapshot | null;
  loading: boolean;
  className?: string;
}) {
  const theme = useThemeTokens();

  if (loading && !snapshot) {
    return (
      <p className={cn("text-xs text-on-surface-variant", className)}>
        Đang tải SessionRadar…
      </p>
    );
  }

  if (!snapshot) {
    return (
      <p className={cn("text-xs text-on-surface-variant", className)}>
        SessionRadar · chưa có dữ liệu quét
      </p>
    );
  }

  const scanTime = snapshot.scannedAt ? formatDateTime(snapshot.scannedAt) : "—";

  return (
    <p className={cn("flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs", className)}>
      <span className="inline-flex items-center gap-1.5 font-medium" style={{ color: theme.primary }}>
        <span
          className="inline-block h-1.5 w-1.5 animate-pulse rounded-full"
          style={{ backgroundColor: theme.primary }}
        />
        SessionRadar
      </span>
      <span className="text-on-surface-variant">
        Quét gần nhất: <span className="font-mono text-on-surface">{scanTime}</span>
        {" · "}
        {snapshot.matchCount > 0
          ? `${snapshot.matchCount} mã đột biến (|±3%|, KL≥1M)`
          : "0 mã đột biến"}
      </span>
    </p>
  );
}

interface SessionRadarListProps {
  snapshot: RadarLiveSnapshot | null;
  loading: boolean;
  limit?: number;
}

export function SessionRadarList({ snapshot, loading, limit = 8 }: SessionRadarListProps) {
  const items = snapshot?.items ?? [];

  if (loading && items.length === 0) {
    return <p className="py-6 text-center text-sm text-on-surface-variant">Đang tải...</p>;
  }

  if (!loading && items.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-on-surface-variant">
        Chưa có mã đột biến trong phiên (|±3%|, KL≥1M).
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {items.slice(0, limit).map((item) => (
        <SessionRadarRow key={item.symbol} item={item} />
      ))}
    </div>
  );
}
