import { cn } from "@/lib/utils";
import { CHART_INTERVALS, type ChartInterval } from "@/types";

interface ChartTimeframeBarProps {
  value: ChartInterval;
  onChange: (interval: ChartInterval) => void;
  className?: string;
}

export function ChartTimeframeBar({ value, onChange, className }: ChartTimeframeBarProps) {
  return (
    <div
      className={cn(
        "flex flex-wrap gap-1 rounded-lg p-1",
        className,
      )}
      style={{ backgroundColor: "#1e222d" }}
    >
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
              active ? "text-white" : "text-gray-400 hover:text-gray-200",
            )}
            style={active ? { backgroundColor: "#2962ff" } : undefined}
          >
            {item.short}
          </button>
        );
      })}
    </div>
  );
}

export function chartIntervalLabel(interval: ChartInterval): string {
  return CHART_INTERVALS.find((i) => i.value === interval)?.label ?? interval;
}
