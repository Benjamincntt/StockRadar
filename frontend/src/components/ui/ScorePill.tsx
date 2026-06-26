import { cn } from "@/lib/utils";
import { theme } from "@/theme/tokens";

interface ScorePillProps {
  score: number;
  className?: string;
}

export function ScorePill({ score, className }: ScorePillProps) {
  const style =
    score >= 85
      ? { bg: theme.greenBg, color: theme.green }
      : score >= 70
        ? { bg: theme.blueBg, color: theme.blue }
        : { bg: "#fef3c7", color: theme.amber };

  return (
    <span
      className={cn("inline-flex rounded-full px-2.5 py-1 text-xs font-semibold", className)}
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
  const positive = value > 0;
  const neutral = value === 0;
  const bg = neutral ? theme.surfaceMuted : positive ? theme.greenBg : theme.redBg;
  const color = neutral ? theme.textMuted : positive ? theme.green : theme.red;

  return (
    <span
      className={cn("inline-flex rounded-full px-2 py-0.5 text-xs font-semibold", className)}
      style={{ backgroundColor: bg, color }}
    >
      {positive ? "+" : ""}
      {value.toFixed(2)}%
    </span>
  );
}
