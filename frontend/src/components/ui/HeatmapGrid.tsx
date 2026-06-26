import { cn } from "@/lib/utils";
import { theme } from "@/theme/tokens";

export interface HeatmapCell {
  id: string;
  label: string;
  value: number;
  changePercent: number;
  size?: number;
}

interface HeatmapGridProps {
  cells: HeatmapCell[];
  className?: string;
  /** score = hien diem; change = hien % thay doi */
  variant?: "score" | "change";
}

export function HeatmapGrid({ cells, className, variant = "score" }: HeatmapGridProps) {
  const sorted = [...cells].sort((a, b) => (b.size ?? b.value) - (a.size ?? a.value));

  return (
    <div className={cn("grid grid-cols-4 auto-rows-[72px] gap-1.5", className)}>
      {sorted.map((cell, index) => {
        const span =
          index === 0 ? "col-span-2 row-span-2" : index < 4 ? "col-span-2" : "col-span-1";
        const positive = cell.changePercent >= 0;
        const intensity = Math.min(Math.abs(cell.changePercent) / 8, 1);
        const bg = positive
          ? `rgba(22, 163, 74, ${0.18 + intensity * 0.55})`
          : `rgba(220, 38, 38, ${0.18 + intensity * 0.55})`;

        return (
          <div
            key={cell.id}
            className={cn("flex flex-col justify-between rounded-xl p-2.5", span)}
            style={{ backgroundColor: bg, border: `1px solid ${theme.border}` }}
          >
            <span className="text-[11px] font-semibold text-gray-800">{cell.label}</span>
            <div>
              {variant === "change" ? (
                <p
                  className="text-sm font-bold"
                  style={{ color: positive ? theme.green : theme.red }}
                >
                  {positive ? "+" : ""}
                  {cell.changePercent.toFixed(2)}%
                </p>
              ) : (
                <>
                  <p className="text-sm font-bold text-gray-900">{cell.value}</p>
                  <p
                    className="text-[10px] font-semibold"
                    style={{ color: positive ? theme.green : theme.red }}
                  >
                    {positive ? "+" : ""}
                    {cell.changePercent.toFixed(2)}%
                  </p>
                </>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
