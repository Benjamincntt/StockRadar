import { useMemo, useRef, useState } from "react";
import { cn, formatPrice, formatVolume } from "@/lib/utils";
import { chartIntervalLabel, useChartColors } from "@/components/ui/ChartTimeframeBar";
import { getAccumulationZoneColors } from "@/components/chart/AccumulationLegend";
import { resolveAccumulationZones, sliceChartToOneYear, type ResolvedAccumulationZone } from "@/lib/chartAccumulation";
import type { BasePricePeriod, ChartBar, ChartInterval } from "@/types";
import { useTheme } from "@/context/ThemeContext";

export interface PriceVolumeChartProps {
  symbol: string;
  name: string;
  interval: ChartInterval;
  bars: ChartBar[];
  livePrice?: number;
  liveChangePercent?: number;
  loading?: boolean;
  className?: string;
  accumulationPeriods?: BasePricePeriod[];
  baseZone?: { low: number; high: number };
  highlightZoneIndex?: number | null;
  resolvedZones?: ResolvedAccumulationZone[];
}

function mergeLiveBar(bars: ChartBar[], livePrice?: number): ChartBar[] {
  if (bars.length === 0 || livePrice == null) return bars;
  const copy = bars.map((b) => ({ ...b }));
  const last = copy[copy.length - 1];
  last.close = livePrice;
  last.high = Math.max(last.high, livePrice);
  last.low = Math.min(last.low, livePrice);
  return copy;
}

function parseChartDay(iso: string): Date {
  const raw = iso.includes("T") ? iso : `${iso}T12:00:00`;
  return new Date(raw);
}

function formatAxisTime(iso: string, interval: ChartInterval, prevIso?: string) {
  const d = parseChartDay(iso);
  if (Number.isNaN(d.getTime())) return iso;
  if (interval === "1D") {
    const dd = String(d.getDate()).padStart(2, "0");
    const mm = String(d.getMonth() + 1).padStart(2, "0");
    const yy = String(d.getFullYear()).slice(-2);
    if (!prevIso) return `${dd}/${mm}/${yy}`;
    const prev = parseChartDay(prevIso);
    if (
      Number.isNaN(prev.getTime())
      || prev.getFullYear() !== d.getFullYear()
      || prev.getMonth() !== d.getMonth()
    ) {
      return `${dd}/${mm}/${yy}`;
    }
    return `${dd}/${mm}`;
  }
  if (interval === "1H") {
    return d.toLocaleString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "2-digit",
      hour: "2-digit",
    });
  }
  return d.toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

function selectAxisTickIndices(barCount: number, maxTicks = 7): number[] {
  if (barCount <= 0) return [];
  if (barCount === 1) return [0];
  const indices = new Set<number>([0, barCount - 1]);
  const inner = maxTicks - 2;
  if (inner > 0) {
    const step = (barCount - 1) / (inner + 1);
    for (let k = 1; k <= inner; k++) {
      indices.add(Math.round(k * step));
    }
  }
  return [...indices].sort((a, b) => a - b);
}

function candleBodyWidth(slotW: number) {
  return Math.max(1.5, Math.min(7, slotW * 0.72));
}
function formatHeaderTime(iso: string, interval: ChartInterval) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  if (interval === "1D") {
    return d.toLocaleDateString("vi-VN", { weekday: "short", day: "2-digit", month: "short", year: "numeric" });
  }
  return d.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function renderOhlcBar(
  cx: number,
  bar: ChartBar,
  color: string,
  bodyW: number,
  priceY: (p: number) => number,
) {
  const openY = priceY(bar.open);
  const closeY = priceY(bar.close);
  const highY = priceY(bar.high);
  const lowY = priceY(bar.low);
  const half = bodyW / 2;
  const up = bar.close >= bar.open;
  const bodyTop = priceY(Math.max(bar.open, bar.close));
  const bodyBottom = priceY(Math.min(bar.open, bar.close));
  const bodyH = Math.max(1.5, bodyBottom - bodyTop);

  return (
    <g>
      <line x1={cx} y1={highY} x2={cx} y2={lowY} stroke={color} strokeWidth="1.25" />
      <line x1={cx - half} y1={openY} x2={cx} y2={openY} stroke={color} strokeWidth="1.25" />
      <line x1={cx} y1={closeY} x2={cx + half} y2={closeY} stroke={color} strokeWidth="1.5" />
      {bodyH > 2.5 ? (
        <rect
          x={cx - half * 0.55}
          y={bodyTop}
          width={half * 1.1}
          height={bodyH}
          fill={up ? color : "transparent"}
          stroke={color}
          strokeWidth="1"
          opacity={up ? 0.95 : 1}
        />
      ) : null}
    </g>
  );
}

export function PriceVolumeChart({
  symbol,
  name,
  interval,
  bars,
  livePrice,
  liveChangePercent,
  loading = false,
  className,
  accumulationPeriods,
  baseZone,
  highlightZoneIndex = null,
  resolvedZones,
}: PriceVolumeChartProps) {
  const chartColors = useChartColors();
  const { mode } = useTheme();
  const svgRef = useRef<SVGSVGElement>(null);
  const [hoverIndex, setHoverIndex] = useState<number | null>(null);

  const rawData = useMemo(() => mergeLiveBar(bars, livePrice), [bars, livePrice]);
  const data = useMemo(
    () => (interval === "1D" ? sliceChartToOneYear(rawData) : rawData),
    [rawData, interval],
  );

  const axisTickIndices = useMemo(
    () => (interval === "1D" ? selectAxisTickIndices(data.length) : null),
    [data.length, interval],
  );

  const accumulationZones = useMemo(() => {
    if (interval !== "1D") return [];
    if (resolvedZones) return resolvedZones.filter((z) => z.visible);
    if (!accumulationPeriods?.length) return [];
    return resolveAccumulationZones(data, accumulationPeriods).filter((z) => z.visible);
  }, [data, accumulationPeriods, interval, resolvedZones]);

  const showAccumulation = interval === "1D" && accumulationZones.length > 0;

  const layout = useMemo(() => {
    const width = 360;
    const padL = 8;
    const padR = 54;
    const height = 300;
    const padTop = 8;
    const padBottom = 26;
    const gap = 6;
    const plotW = width - padL - padR;
    const plotH = height - padTop - padBottom;
    const volumeH = plotH * 0.26;
    const priceH = plotH - volumeH - gap;
    const priceTop = padTop;
    const volumeTop = padTop + priceH + gap;

    if (data.length === 0) {
      return {
        width,
        height,
        padL,
        padR,
        padTop,
        padBottom,
        plotW,
        priceH,
        volumeH,
        priceTop,
        volumeTop,
        minP: 0,
        maxP: 1,
        maxV: 1,
        slotW: 0,
        bodyW: 4,
      };
    }

    const lows = data.map((b) => b.low);
    const highs = data.map((b) => b.high);
    const minP = Math.min(...lows);
    const maxP = Math.max(...highs);
    const pad = (maxP - minP) * 0.08 || maxP * 0.02 || 1;
    const maxV = Math.max(...data.map((b) => b.volume), 1);
    const slotW = plotW / data.length;
    const bodyW = candleBodyWidth(slotW);

    return {
      width,
      height,
      padL,
      padR,
      padTop,
      padBottom,
      plotW,
      priceH,
      volumeH,
      priceTop,
      volumeTop,
      minP: minP - pad,
      maxP: maxP + pad,
      maxV,
      slotW,
      bodyW,
    };
  }, [data]);

  const activeIndex = hoverIndex ?? (data.length > 0 ? data.length - 1 : null);
  const activeBar = activeIndex != null ? data[activeIndex] : null;

  const change =
    activeBar && activeIndex != null && activeIndex > 0
      ? activeBar.close - data[activeIndex - 1].close
      : activeBar
        ? activeBar.close - activeBar.open
        : 0;

  const changePct =
    liveChangePercent != null && activeIndex === data.length - 1
      ? liveChangePercent
      : activeBar && activeBar.open > 0
        ? ((activeBar.close - activeBar.open) / activeBar.open) * 100
        : 0;

  const bullish = change >= 0;
  const accent = bullish ? chartColors.green : chartColors.red;

  const priceY = (price: number) => {
    const range = layout.maxP - layout.minP || 1;
    return layout.priceTop + layout.priceH - ((price - layout.minP) / range) * layout.priceH;
  };

  const volH = (volume: number) => (volume / layout.maxV) * layout.volumeH;

  const handlePointer = (clientX: number) => {
    const svg = svgRef.current;
    if (!svg || data.length === 0) return;
    const rect = svg.getBoundingClientRect();
    const x = ((clientX - rect.left) / rect.width) * layout.width;
    const rel = x - layout.padL;
    const idx = Math.floor(rel / layout.slotW);
    setHoverIndex(Math.max(0, Math.min(data.length - 1, idx)));
  };

  const priceTicks = useMemo(() => {
    const steps = 4;
    const range = layout.maxP - layout.minP || 1;
    return Array.from({ length: steps + 1 }, (_, i) => layout.minP + (range * i) / steps);
  }, [layout.maxP, layout.minP]);

  if (!loading && data.length === 0) {
    return (
      <div
        className={cn("flex h-[300px] items-center justify-center rounded-xl text-sm", className)}
        style={{ backgroundColor: chartColors.bg, color: chartColors.muted }}
      >
        Không có dữ liệu khung {chartIntervalLabel(interval)} từ KBS.
      </div>
    );
  }

  return (
    <div
      className={cn("relative overflow-hidden rounded-xl", className)}
      style={{ backgroundColor: chartColors.bg }}
    >
      {loading && (
        <div
          className="absolute inset-0 z-10 flex items-center justify-center text-xs font-medium"
          style={{ backgroundColor: chartColors.overlay, color: chartColors.muted }}
        >
          Đang tải {chartIntervalLabel(interval)}...
        </div>
      )}

      <div className="px-3 pb-1 pt-3">
        <p className="text-[11px] font-medium" style={{ color: chartColors.muted }}>
          {name} · {chartIntervalLabel(interval)}
          {interval === "1D" ? " · 1 năm" : ""} · {symbol}
          {activeBar ? ` · ${formatHeaderTime(activeBar.time, interval)}` : ""}
        </p>
        {activeBar && (
          <>
            <p className="mt-1 text-[12px] leading-snug" style={{ color: chartColors.text }}>
              <span style={{ color: chartColors.muted }}>O</span>
              <span className="ml-0.5 font-semibold">{formatPrice(activeBar.open)}</span>
              <span className="ml-2" style={{ color: chartColors.muted }}>
                H
              </span>
              <span className="ml-0.5 font-semibold">{formatPrice(activeBar.high)}</span>
              <span className="ml-2" style={{ color: chartColors.muted }}>
                L
              </span>
              <span className="ml-0.5 font-semibold">{formatPrice(activeBar.low)}</span>
              <span className="ml-2" style={{ color: chartColors.muted }}>
                C
              </span>
              <span className="ml-0.5 font-semibold" style={{ color: accent }}>
                {formatPrice(activeBar.close)}
              </span>
              <span className="ml-2 font-semibold" style={{ color: accent }}>
                {change >= 0 ? "+" : ""}
                {formatPrice(change)} ({changePct >= 0 ? "+" : ""}
                {changePct.toFixed(2)}%)
              </span>
            </p>
            <p className="mt-0.5 text-[11px]" style={{ color: chartColors.muted }}>
              Khối lượng{" "}
              <span className="font-semibold" style={{ color: chartColors.text }}>
                {formatVolume(activeBar.volume)}
              </span>
            </p>
          </>
        )}
      </div>

      <svg
        ref={svgRef}
        viewBox={`0 0 ${layout.width} ${layout.height}`}
        className="w-full touch-none select-none"
        onMouseMove={(e) => handlePointer(e.clientX)}
        onMouseLeave={() => setHoverIndex(null)}
        onTouchMove={(e) => {
          if (e.touches[0]) handlePointer(e.touches[0].clientX);
        }}
        onTouchEnd={() => setHoverIndex(null)}
        aria-label={`Biểu đồ giá và khối lượng ${symbol}`}
      >
        {priceTicks.map((tick) => {
          const y = priceY(tick);
          return (
            <g key={tick}>
              <line
                x1={layout.padL}
                y1={y}
                x2={layout.width - layout.padR}
                y2={y}
                stroke={chartColors.grid}
                strokeWidth="1"
              />
              <text
                x={layout.width - 4}
                y={y + 3}
                textAnchor="end"
                fontSize="9"
                fill={chartColors.muted}
              >
                {formatPrice(tick)}
              </text>
            </g>
          );
        })}

        <line
          x1={layout.padL}
          y1={layout.volumeTop - 2}
          x2={layout.width - layout.padR}
          y2={layout.volumeTop - 2}
          stroke={chartColors.grid}
          strokeWidth="1"
        />

        {showAccumulation && baseZone && (
          <rect
            x={layout.padL}
            y={priceY(baseZone.high)}
            width={layout.plotW}
            height={Math.max(2, priceY(baseZone.low) - priceY(baseZone.high))}
            fill={mode === "light" ? "rgba(0, 109, 65, 0.06)" : "rgba(68, 224, 146, 0.08)"}
            stroke={mode === "light" ? "rgba(0, 109, 65, 0.25)" : "rgba(68, 224, 146, 0.35)"}
            strokeWidth="1"
            strokeDasharray="6 4"
          />
        )}

        {showAccumulation &&
          accumulationZones.map((zone) => {
            const zi = zone.periodIndex;
            const colors = getAccumulationZoneColors(zi, mode);
            const dimmed = highlightZoneIndex != null && highlightZoneIndex !== zi;
            const x = layout.padL + zone.startIndex * layout.slotW;
            const w = (zone.endIndex - zone.startIndex + 1) * layout.slotW;
            const y = priceY(zone.high);
            const h = Math.max(4, priceY(zone.low) - priceY(zone.high));
            return (
              <g key={zone.id} opacity={dimmed ? 0.35 : 1}>
                <rect
                  x={x}
                  y={y}
                  width={w}
                  height={h}
                  fill={colors.fill}
                  stroke={colors.stroke}
                  strokeWidth="1.5"
                  rx="2"
                />
                {w > 28 && (
                  <text
                    x={x + 4}
                    y={y + 11}
                    fontSize="8"
                    fontWeight="600"
                    fill={colors.label}
                  >
                    Nền
                  </text>
                )}
              </g>
            );
          })}

        {data.map((bar, i) => {
          const cx = layout.padL + (i + 0.5) * layout.slotW;
          const up = bar.close >= bar.open;
          const color = up ? chartColors.green : chartColors.red;
          const vh = volH(bar.volume);
          const volY = layout.volumeTop + layout.volumeH - vh;
          const barW = layout.bodyW;

          return (
            <g key={`${bar.time}-${i}`}>
              {renderOhlcBar(cx, bar, color, barW, priceY)}
              <rect
                x={cx - barW / 2}
                y={volY}
                width={barW}
                height={vh}
                fill={color}
                opacity={0.8}
              />
            </g>
          );
        })}

        {activeIndex != null && (
          <line
            x1={layout.padL + (activeIndex + 0.5) * layout.slotW}
            y1={layout.priceTop}
            x2={layout.padL + (activeIndex + 0.5) * layout.slotW}
            y2={layout.volumeTop + layout.volumeH}
            stroke={chartColors.crosshair}
            strokeWidth="1"
            strokeDasharray="4 3"
          />
        )}

        {data.map((bar, i) => {
          const show =
            axisTickIndices != null
              ? axisTickIndices.includes(i)
              : i === 0 ||
                i === data.length - 1 ||
                (data.length > 8 && i % Math.ceil(data.length / 4) === 0);
          if (!show) return null;
          const x = layout.padL + (i + 0.5) * layout.slotW;
          const prevBar = i > 0 ? data[i - 1] : undefined;
          return (
            <text
              key={`time-${bar.time}`}
              x={x}
              y={layout.height - 5}
              textAnchor="middle"
              fontSize="8"
              fill={chartColors.muted}
            >
              {formatAxisTime(bar.time, interval, prevBar?.time)}
            </text>
          );
        })}
      </svg>
    </div>
  );
}
