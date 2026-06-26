import { useMemo } from "react";
import { cn, formatPercent, formatPrice } from "@/lib/utils";

const upColor = "#26a69a";
const downColor = "#ef5350";
const refColor = "#f59e0b";

export interface MiniSparklineProps {
  prices: number[];
  reference?: number;
  positive?: boolean;
  className?: string;
  height?: number;
}

export function MiniSparkline({
  prices,
  reference,
  positive = true,
  className,
  height = 40,
}: MiniSparklineProps) {
  const layout = useMemo(() => {
    if (prices.length < 2) return null;

    const width = 140;
    const padX = 2;
    const padY = 4;
    const plotW = width - padX * 2;
    const plotH = height - padY * 2;
    const values = reference != null ? [...prices, reference] : prices;
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;

    const toX = (i: number) => padX + (i / (prices.length - 1)) * plotW;
    const toY = (v: number) => padY + plotH - ((v - min) / range) * plotH;

    const line = prices.map((v, i) => `${toX(i)},${toY(v)}`).join(" ");
    const last = prices.length - 1;
    const lastX = toX(last);
    const lastY = toY(prices[last]);
    const refY = reference != null ? toY(reference) : null;

    return { width, height, line, lastX, lastY, refY, padX, plotW };
  }, [prices, reference, height]);

  if (!layout) {
    return <div className={cn("min-h-[40px] flex-1", className)} aria-hidden />;
  }

  const stroke = positive ? upColor : downColor;

  return (
    <svg
      viewBox={`0 0 ${layout.width} ${layout.height}`}
      className={cn("h-10 w-full min-w-[72px] flex-1", className)}
      preserveAspectRatio="none"
      aria-hidden
    >
      {layout.refY != null && (
        <line
          x1={layout.padX}
          y1={layout.refY}
          x2={layout.padX + layout.plotW}
          y2={layout.refY}
          stroke={refColor}
          strokeWidth="1"
          strokeDasharray="3 2"
          opacity={0.9}
        />
      )}
      <polyline
        fill="none"
        stroke={stroke}
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
        points={layout.line}
      />
      <circle cx={layout.lastX} cy={layout.lastY} r="2.5" fill={stroke} />
    </svg>
  );
}

export function formatQuoteChangeLine(price: number, changePercent: number) {
  const ref = changePercent !== 0 ? price / (1 + changePercent / 100) : price;
  const abs = price - ref;
  const sign = abs >= 0 ? "+" : "";
  return `${sign}${formatPrice(abs)} / ${formatPercent(changePercent)}`;
}

interface MiniQuoteStatsProps {
  price: number;
  changePercent: number;
  className?: string;
  dark?: boolean;
}

export function MiniQuoteStats({ price, changePercent, className, dark = false }: MiniQuoteStatsProps) {
  const positive = changePercent >= 0;
  const color = positive ? upColor : downColor;

  return (
    <div className={cn("shrink-0 text-right", className)}>
      <p
        className={cn("text-[15px] font-bold leading-tight tabular-nums", dark ? "text-white" : "text-gray-900")}
      >
        {formatPrice(price)}
      </p>
      <p className="mt-0.5 text-[11px] font-semibold tabular-nums" style={{ color }}>
        {formatQuoteChangeLine(price, changePercent)}
      </p>
    </div>
  );
}
