import { useThemeTokens } from "@/context/ThemeContext";
import { formatPercent, formatPrice } from "@/lib/utils";
import type { EntryPoint } from "@/types";
import { SectionTitle } from "@/components/ui/Card";
import { Check, X } from "lucide-react";
import { showsPriceLevels } from "@/components/entry/BuyDecisionCard";

const STATUS_LABEL: Record<EntryPoint["status"], string> = {
  Ready: "Vào ngay",
  Watch: "Chờ kích hoạt",
  Late: "Trễ / FOMO",
  Invalid: "Không vào",
};

const TYPE_LABEL: Record<EntryPoint["type"], string> = {
  None: "",
  Breakout: "Breakout",
  Shakeout: "Shakeout",
};

export function EntryPointCard({
  entry,
  buyScore,
}: {
  entry: EntryPoint;
  buyScore?: number;
}) {
  const theme = useThemeTokens();
  const statusStyle = getStatusStyle(entry.status, theme);
  const typeLabel = TYPE_LABEL[entry.type];
  const showConfidence =
    buyScore == null || Math.abs(entry.confidence - buyScore) >= 1;
  const showPrices = showsPriceLevels(entry);

  return (
    <div
      className="overflow-hidden rounded-2xl border"
      style={{ borderColor: statusStyle.border, backgroundColor: statusStyle.bg }}
    >
      <div className="px-4 pt-4 pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="label-caps text-on-surface-variant">Giá vào</p>
            <h3 className="mt-1 text-base font-bold leading-snug text-on-surface">{entry.headline}</h3>
            {typeLabel && (
              <span
                className="mt-2 inline-block rounded-full px-2.5 py-0.5 text-[11px] font-bold uppercase tracking-wide"
                style={{ backgroundColor: theme.greenBg, color: theme.primary }}
              >
                {typeLabel}
              </span>
            )}
          </div>
          <div className="shrink-0 text-right">
            <span
              className="inline-block rounded-full px-2.5 py-1 text-[11px] font-bold"
              style={{ backgroundColor: statusStyle.pillBg, color: statusStyle.pillColor }}
            >
              {STATUS_LABEL[entry.status]}
            </span>
            {showConfidence && (
              <>
                <p
                  className="font-data mt-2 text-2xl font-bold tabular-nums"
                  style={{ color: statusStyle.accent }}
                >
                  {entry.confidence}%
                </p>
                <p className="text-[10px] text-on-surface-variant">checklist đạt</p>
              </>
            )}
          </div>
        </div>

        <p className="mt-3 text-sm leading-relaxed text-on-surface-variant">{entry.action}</p>
      </div>

      {showPrices && (
        <div className="grid grid-cols-2 gap-px border-t border-outline-variant bg-outline-variant">
          <PriceCell label="Vào" value={entry.entryPrice} accent />
          <PriceCell label="Cắt lỗ" value={entry.stopLoss} danger />
          <PriceCell label="Kích hoạt" value={entry.triggerPrice} />
          <PriceCell label="Mục tiêu" value={entry.targetPrice} accent />
        </div>
      )}

      {entry.riskRewardRatio > 0 && showPrices && (
        <div className="border-t border-outline-variant px-4 py-2 text-center">
          <span className="text-xs text-on-surface-variant">R:R </span>
          <span className="font-data text-sm font-bold text-on-surface">1 : {entry.riskRewardRatio}</span>
        </div>
      )}

      {entry.checklist.length > 0 && (
        <div className="border-t border-outline-variant bg-surface px-4 py-3">
          <SectionTitle title="Checklist điểm vào" />
          <ul className="mt-2 space-y-1.5">
            {entry.checklist.map((item) => (
              <li key={item.id} className="flex items-start gap-2 text-xs">
                {item.passed ? (
                  <Check className="mt-0.5 h-3.5 w-3.5 shrink-0" style={{ color: theme.primary }} />
                ) : (
                  <X className="mt-0.5 h-3.5 w-3.5 shrink-0" style={{ color: theme.red }} />
                )}
                <div className="min-w-0">
                  <span className="font-semibold text-on-surface">{item.label}</span>
                  <span className="text-on-surface-variant"> — {item.detail}</span>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

export function EntryPointBadge({ entry }: { entry: EntryPoint }) {
  const theme = useThemeTokens();
  const style = getStatusStyle(entry.status, theme);
  const type = TYPE_LABEL[entry.type];

  return (
    <span
      className="inline-flex max-w-full items-center gap-1 truncate rounded-full px-2 py-0.5 text-[10px] font-bold"
      style={{ backgroundColor: style.pillBg, color: style.pillColor }}
      title={entry.headline}
    >
      {type && <span>{type}</span>}
      {type && <span>·</span>}
      <span>{STATUS_LABEL[entry.status]}</span>
      <span className="font-data opacity-80">{entry.confidence}%</span>
    </span>
  );
}

function PriceCell({
  label,
  value,
  danger,
  accent,
}: {
  label: string;
  value: number;
  danger?: boolean;
  accent?: boolean;
}) {
  const theme = useThemeTokens();
  if (value <= 0) return null;

  const color = danger ? theme.red : accent ? theme.primary : theme.text;
  return (
    <div className="bg-surface px-3 py-2.5 text-center">
      <p className="label-caps text-on-surface-variant">{label}</p>
      <p className="font-data mt-0.5 text-sm font-bold tabular-nums" style={{ color }}>
        {formatPrice(value)}
      </p>
    </div>
  );
}

function getStatusStyle(status: EntryPoint["status"], theme: ReturnType<typeof useThemeTokens>) {
  switch (status) {
    case "Ready":
      return {
        bg: theme.greenBg,
        border: theme.primary,
        accent: theme.primary,
        pillBg: theme.primary,
        pillColor: theme.onPrimary,
      };
    case "Watch":
      return {
        bg: theme.amberBg,
        border: theme.amber,
        accent: theme.amber,
        pillBg: theme.amberBg,
        pillColor: theme.amber,
      };
    case "Late":
      return {
        bg: theme.redBg,
        border: theme.red,
        accent: theme.red,
        pillBg: theme.redBg,
        pillColor: theme.red,
      };
    default:
      return {
        bg: theme.neutralBg,
        border: theme.border,
        accent: theme.textMuted,
        pillBg: theme.neutralBg,
        pillColor: theme.textMuted,
      };
  }
}

export function entrySummaryLine(entry: EntryPoint): string {
  const gain = formatPercent(entry.gainFromBasePercent);
  if (entry.status === "Ready") {
    return `${TYPE_LABEL[entry.type] || "Setup"} · nền +${gain}`;
  }
  if (entry.status === "Watch") {
    return `Chờ trên ${formatPrice(entry.triggerPrice)} · nền +${gain}`;
  }
  return entry.headline;
}
