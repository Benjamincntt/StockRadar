import { formatPeriodChip } from "@/lib/chartAccumulation";
import type { BasePricePeriod } from "@/types";
import { useTheme } from "@/context/ThemeContext";

export function AccumulationLegend({
  periods,
  visibleFlags,
  activeIndex,
  onSelect,
}: {
  periods: BasePricePeriod[];
  visibleFlags?: boolean[];
  activeIndex?: number | null;
  onSelect?: (index: number) => void;
}) {
  const { mode } = useTheme();
  const muted = mode === "light" ? "#6c7b6f" : "#bbcabd";
  const visibleCount = visibleFlags
    ? visibleFlags.filter(Boolean).length
    : periods.length;

  if (periods.length === 0) return null;

  return (
    <div className="mt-2 space-y-2">
      <div className="flex items-center justify-between gap-2">
        <p className="label-caps text-on-surface-variant">Vùng nền trên biểu đồ</p>
        <span className="text-[10px] text-on-surface-variant">
          {visibleCount}/{periods.length} vùng · tối thiểu 12 phiên
        </span>
      </div>
      <div className="flex gap-1.5 overflow-x-auto pb-1 [-ms-overflow-style:none] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {periods.map((p, i) => {
          const colors = getAccumulationZoneColors(i, mode);
          const onChart = visibleFlags?.[i] ?? true;
          const active = activeIndex === i;
          return (
            <button
              key={`${p.fromDate}-${p.toDate}`}
              type="button"
              onClick={() => onSelect?.(i)}
              disabled={!onChart}
              title={onChart ? undefined : "Vùng ngoài phạm vi biểu đồ hiện tại"}
              className="flex shrink-0 items-center gap-1.5 rounded-lg border px-2 py-1 text-left transition-opacity disabled:cursor-not-allowed"
              style={{
                borderColor: active ? colors.stroke : "var(--outline-variant, rgba(128,128,128,0.25))",
                backgroundColor: active ? colors.fill : "transparent",
                opacity: !onChart ? 0.4 : activeIndex == null || active ? 1 : 0.55,
              }}
            >
              <span
                className="h-3 w-3 shrink-0 rounded-sm border"
                style={{
                  backgroundColor: onChart ? colors.fill : "transparent",
                  borderColor: colors.stroke,
                  borderStyle: onChart ? "solid" : "dashed",
                }}
                aria-hidden
              />
              <span className="whitespace-nowrap text-[11px] font-semibold text-on-surface">
                Nền · {formatPeriodChip(p.fromDate, p.toDate)}
              </span>
              <span className="font-data text-[10px] tabular-nums" style={{ color: muted }}>
                {p.sessionDays}p
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

export function getAccumulationZoneColors(index: number, mode: "light" | "dark") {
  const palette =
    mode === "light"
      ? [
          { fill: "rgba(147, 51, 234, 0.14)", stroke: "rgba(126, 34, 206, 0.55)", label: "#7e22ce" },
          { fill: "rgba(124, 58, 237, 0.12)", stroke: "rgba(109, 40, 217, 0.5)", label: "#6d28d9" },
          { fill: "rgba(139, 92, 246, 0.1)", stroke: "rgba(124, 58, 237, 0.45)", label: "#5b21b6" },
          { fill: "rgba(168, 85, 247, 0.12)", stroke: "rgba(147, 51, 234, 0.5)", label: "#9333ea" },
          { fill: "rgba(192, 132, 252, 0.1)", stroke: "rgba(168, 85, 247, 0.45)", label: "#a855f7" },
          { fill: "rgba(216, 180, 254, 0.12)", stroke: "rgba(192, 132, 252, 0.5)", label: "#c084fc" },
        ]
      : [
          { fill: "rgba(168, 85, 247, 0.22)", stroke: "rgba(192, 132, 252, 0.85)", label: "#c4b5fd" },
          { fill: "rgba(139, 92, 246, 0.2)", stroke: "rgba(167, 139, 250, 0.8)", label: "#a78bfa" },
          { fill: "rgba(124, 58, 237, 0.18)", stroke: "rgba(167, 139, 250, 0.75)", label: "#a78bfa" },
          { fill: "rgba(147, 51, 234, 0.2)", stroke: "rgba(192, 132, 252, 0.8)", label: "#c4b5fd" },
          { fill: "rgba(126, 34, 206, 0.18)", stroke: "rgba(167, 139, 250, 0.75)", label: "#a78bfa" },
          { fill: "rgba(109, 40, 217, 0.16)", stroke: "rgba(147, 51, 234, 0.7)", label: "#ddd6fe" },
        ];
  return palette[index % palette.length];
}
