import { cn } from "@/lib/utils";
import { useTheme, useThemeTokens } from "@/context/ThemeContext";

interface ScorePillProps {
  score: number;
  className?: string;
}

export function ScorePill({ score, className }: ScorePillProps) {
  const theme = useThemeTokens();
  const style =
    score >= 85
      ? { bg: theme.greenBg, color: theme.primary }
      : score >= 70
        ? { bg: theme.greenBg, color: theme.primaryContainer }
        : { bg: theme.amberBg, color: theme.amber };

  return (
    <span
      className={cn(
        "font-data inline-flex rounded px-2 py-0.5 text-xs font-semibold",
        className,
      )}
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      {score}
    </span>
  );
}

interface ChangePillProps {
  value: number;
  className?: string;
}

export function ChangePill({ value, className }: ChangePillProps) {
  const theme = useThemeTokens();
  const positive = value > 0;
  const neutral = value === 0;
  const bg = neutral ? theme.neutralBg : positive ? theme.greenBg : theme.redBg;
  const color = neutral ? theme.textMuted : positive ? theme.primary : theme.red;

  return (
    <span
      className={cn(
        "font-data inline-flex rounded px-1.5 py-0.5 text-xs font-semibold",
        className,
      )}
      style={{ backgroundColor: bg, color }}
    >
      {positive ? "+" : ""}
      {value.toFixed(2)}%
    </span>
  );
}

interface DirectionTagProps {
  direction: "up" | "down";
  className?: string;
}

export function DirectionTag({ direction, className }: DirectionTagProps) {
  const theme = useThemeTokens();
  const up = direction === "up";
  return (
    <span
      className={cn("rounded px-1.5 py-0.5 text-[10px] font-bold", className)}
      style={{
        backgroundColor: up ? theme.greenBg : theme.redBg,
        color: up ? theme.primary : theme.red,
        border: up ? `1px solid color-mix(in srgb, ${theme.primary} 20%, transparent)` : `1px solid color-mix(in srgb, ${theme.red} 20%, transparent)`,
      }}
    >
      {up ? "TĂNG" : "GIẢM"}
    </span>
  );
}

export function NewBadge({ className }: { className?: string }) {
  const theme = useThemeTokens();
  return (
    <span
      className={cn("rounded px-1.5 py-0.5 text-[10px] font-bold", className)}
      style={{ backgroundColor: theme.amberBg, color: theme.amber }}
    >
      MỚI
    </span>
  );
}

export function PredictedHitPill({
  percent,
  sampleCount,
  className,
}: {
  percent?: number;
  sampleCount?: number;
  className?: string;
}) {
  const theme = useThemeTokens();
  if (percent == null || percent <= 0) return null;

  const p = percent;
  const color = p >= 65 ? theme.primary : p >= 50 ? theme.amber : theme.textMuted;
  const sample =
    sampleCount != null && sampleCount > 0
      ? ` · n=${sampleCount}`
      : "";

  return (
    <span
      className={cn(
        "inline-block rounded-full px-2 py-0.5 font-data text-[10px] font-bold tabular-nums",
        className,
      )}
      style={{ backgroundColor: `${color}22`, color }}
      title="Dự báo xác suất setup đạt mục tiêu swing (từ reliability tiêu chí)"
    >
      P {p.toFixed(0)}%{sample}
    </span>
  );
}

interface FilterChipsProps<T extends string> {
  value: T;
  options: { key: T; label: string }[];
  onChange: (key: T) => void;
  className?: string;
}

export function FilterChips<T extends string>({
  value,
  options,
  onChange,
  className,
}: FilterChipsProps<T>) {
  const { mode } = useTheme();
  const isLight = mode === "light";

  return (
    <div className={cn("flex gap-2 overflow-x-auto py-1", className)}>
      {options.map(({ key, label }) => {
        const active = value === key;
        return (
          <button
            key={key}
            type="button"
            onClick={() => onChange(key)}
            className={cn(
              "shrink-0 rounded-full px-5 py-1.5 text-sm font-medium transition-colors",
              active
                ? "bg-primary text-on-primary shadow-sm"
                : isLight
                  ? "border border-outline-variant/30 bg-surface-lowest text-on-surface-variant hover:border-primary/50"
                  : "bg-surface-high text-on-surface-variant hover:text-on-surface",
            )}
          >
            {label}
          </button>
        );
      })}
    </div>
  );
}
