import { useThemeTokens } from "@/context/ThemeContext";
import type { SwingDecision } from "@/types";

const VERDICT_LABEL: Record<string, string> = {
  Go: "Vào lệnh",
  Wait: "Chờ / theo dõi",
  NoGo: "Không vào",
};

export function SwingDecisionCard({ swing }: { swing: SwingDecision }) {
  const theme = useThemeTokens();
  const style = getVerdictStyle(swing.verdict, theme);

  return (
    <div
      className="overflow-hidden rounded-2xl border"
      style={{ borderColor: style.border, backgroundColor: style.bg }}
    >
      <div className="px-4 py-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="label-caps text-on-surface-variant">Swing decision</p>
            <h3 className="mt-1 text-base font-bold text-on-surface">{swing.headline}</h3>
            <p className="mt-1 text-xs leading-relaxed text-on-surface-variant">{swing.detail}</p>
          </div>
          <span
            className="shrink-0 rounded-full px-2.5 py-1 text-[11px] font-bold"
            style={{ backgroundColor: style.pillBg, color: style.pillColor }}
          >
            {VERDICT_LABEL[swing.verdict] ?? swing.verdict}
          </span>
        </div>

        <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
          <Metric label="P điều chỉnh" value={`${swing.adjustedHitPercent.toFixed(0)}%`} />
          <Metric label="Size gợi ý" value={`${swing.suggestedSizePercent.toFixed(1)}% NAV`} />
          <Metric label="R:R" value={swing.riskRewardRatio.toFixed(1)} />
          <Metric
            label="Cal cá nhân"
            value={`×${swing.personalCalibrationFactor.toFixed(2)}`}
          />
        </div>

        {swing.requiresMasterConfirm && (
          <p className="mt-2 text-[11px] font-medium text-amber-600 dark:text-amber-400">
            Ưu tiên chờ Mua điểm 1 xác nhận trong phiên
          </p>
        )}

        {swing.reasons.length > 0 && (
          <ul className="mt-2 space-y-0.5 border-t border-outline-variant pt-2">
            {swing.reasons.slice(0, 4).map((r) => (
              <li key={r} className="text-[10px] text-on-surface-variant">
                · {r}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-surface-low px-2 py-1.5">
      <p className="text-[10px] text-on-surface-variant">{label}</p>
      <p className="font-data font-bold tabular-nums text-on-surface">{value}</p>
    </div>
  );
}

function getVerdictStyle(verdict: string, theme: ReturnType<typeof useThemeTokens>) {
  switch (verdict) {
    case "Go":
      return {
        bg: theme.greenBg,
        border: theme.primary,
        pillBg: theme.primary,
        pillColor: theme.onPrimary,
      };
    case "Wait":
      return {
        bg: theme.amberBg,
        border: theme.amber,
        pillBg: theme.amberBg,
        pillColor: theme.amber,
      };
    default:
      return {
        bg: theme.neutralBg,
        border: theme.border,
        pillBg: theme.neutralBg,
        pillColor: theme.textMuted,
      };
  }
}
