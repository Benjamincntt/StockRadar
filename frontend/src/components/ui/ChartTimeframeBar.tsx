import { cn } from "@/lib/utils";
import { CHART_INTERVALS, type ChartInterval } from "@/types";
import { useTheme } from "@/context/ThemeContext";

interface ChartTimeframeBarProps {
  value: ChartInterval;
  onChange: (interval: ChartInterval) => void;
  className?: string;
}

export function ChartTimeframeBar({ value, onChange, className }: ChartTimeframeBarProps) {
  const { mode } = useTheme();
  const isLight = mode === "light";

  return (
    <div
      className={cn("flex flex-wrap gap-1 rounded-lg p-1", className)}
      style={{ backgroundColor: isLight ? undefined : undefined }}
    >
      <div className="flex flex-wrap gap-1 rounded-lg bg-surface-low p-1">
        {CHART_INTERVALS.map((item) => {
          const active = item.value === value;
          return (
            <button
              key={item.value}
              type="button"
              title={item.label}
              onClick={() => onChange(item.value)}
              className={cn(
                "min-w-[36px] rounded-md px-2 py-1 text-[11px] font-semibold transition-colors",
                active
                  ? isLight
                    ? "bg-surface-lowest text-primary shadow-sm"
                    : "bg-primary text-on-primary"
                  : "text-on-surface-variant hover:text-on-surface",
              )}
            >
              {item.short}
            </button>
          );
        })}
      </div>
    </div>
  );
}

export function chartIntervalLabel(interval: ChartInterval): string {
  return CHART_INTERVALS.find((i) => i.value === interval)?.label ?? interval;
}

function getChartColors(mode: "light" | "dark") {
  if (mode === "light") {
    return {
      bg: "#ffffff",
      grid: "#f1f5f9",
      text: "#191c1e",
      muted: "#6c7b6f",
      green: "#006d41",
      red: "#ba1a1a",
      crosshair: "rgba(25, 28, 30, 0.2)",
      overlay: "rgba(255, 255, 255, 0.92)",
    };
  }
  return {
    bg: "#111319",
    grid: "#3c4a40",
    text: "#e2e2ea",
    muted: "#bbcabd",
    green: "#44e092",
    red: "#c5020b",
    crosshair: "rgba(226, 226, 234, 0.25)",
    overlay: "rgba(19, 23, 34, 0.72)",
  };
}

export function useChartColors() {
  const { mode } = useTheme();
  return getChartColors(mode);
}
