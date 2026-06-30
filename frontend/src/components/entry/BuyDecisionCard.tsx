import { useState } from "react";
import { useThemeTokens } from "@/context/ThemeContext";
import type { BuyDecision } from "@/types";
import { EntryPointCard } from "@/components/entry/EntryPointCard";
import { PredictedHitPill } from "@/components/ui/ScorePill";
import { ChevronDown, ChevronUp } from "lucide-react";

const RECOMMENDATION_LABEL: Record<BuyDecision["recommendation"], string> = {
  StrongBuy: "Mua mạnh",
  Watch: "Theo dõi",
  Avoid: "Tránh",
};

export function BuyDecisionCard({ decision }: { decision: BuyDecision }) {
  const theme = useThemeTokens();
  const [showBreakdown, setShowBreakdown] = useState(false);
  const recStyle = getRecommendationStyle(decision.recommendation, theme);

  return (
    <div className="space-y-3">
      <div
        className="overflow-hidden rounded-2xl border"
        style={{ borderColor: recStyle.border, backgroundColor: recStyle.bg }}
      >
        <div className="px-4 pt-4 pb-3">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="label-caps text-on-surface-variant">Buy Score</p>
              <h3 className="mt-1 text-base font-bold text-on-surface">
                {decision.passesTopFilter
                  ? "Đạt bộ lọc Top cơ hội"
                  : decision.gateFailure ?? "Chưa đạt Top cơ hội"}
              </h3>
              {decision.reasons.length > 0 && (
                <p className="mt-2 text-xs leading-relaxed text-on-surface-variant">
                  {decision.reasons.slice(0, 3).join(" · ")}
                </p>
              )}
            </div>
            <div className="shrink-0 text-right">
              <span
                className="inline-block rounded-full px-2.5 py-1 text-[11px] font-bold"
                style={{ backgroundColor: recStyle.pillBg, color: recStyle.pillColor }}
              >
                {RECOMMENDATION_LABEL[decision.recommendation]}
              </span>
              <div className="mt-2 flex flex-col items-end gap-1">
                <p className="font-data text-3xl font-bold tabular-nums" style={{ color: recStyle.accent }}>
                  {decision.buyScore}
                </p>
                <PredictedHitPill
                  percent={decision.predictedHitPercent}
                  sampleCount={decision.predictedSampleCount}
                />
              </div>
              <p className="text-[10px] text-on-surface-variant">/ 100</p>
              {decision.setupDna && (
                <p className="mt-1 max-w-[9rem] text-right text-[9px] leading-snug text-on-surface-variant">
                  {decision.setupDna}
                </p>
              )}
            </div>
          </div>
        </div>

        <button
          type="button"
          onClick={() => setShowBreakdown((v) => !v)}
          className="flex w-full items-center justify-between border-t border-outline-variant bg-surface px-4 py-2.5 text-left"
        >
          <span className="text-xs font-semibold text-on-surface-variant">Chi tiết điểm cộng</span>
          {showBreakdown ? (
            <ChevronUp className="h-4 w-4 text-on-surface-variant" />
          ) : (
            <ChevronDown className="h-4 w-4 text-on-surface-variant" />
          )}
        </button>

        {showBreakdown && (
          <ul className="border-t border-outline-variant bg-surface px-4 py-3">
            {decision.topExplainLines && decision.topExplainLines.length > 0 && (
              <li className="mb-2 border-b border-outline-variant pb-2 text-[10px] text-on-surface-variant">
                {decision.topExplainLines.map((line) => (
                  <p key={line} className="py-0.5">
                    ✓ {line}
                  </p>
                ))}
              </li>
            )}
            {decision.breakdown.map((item) => (
              <li key={item.id} className="flex items-center justify-between gap-2 py-1.5 text-xs">
                <span className="font-medium text-on-surface">{item.label}</span>
                <span className="font-data shrink-0 font-bold tabular-nums text-on-surface">
                  +{item.points}
                  <span className="font-normal text-on-surface-variant">/{item.maxPoints}</span>
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>

      <EntryPointCard entry={decision.entryPoint} />
    </div>
  );
}

export function BuyRecommendationBadge({ recommendation }: { recommendation: BuyDecision["recommendation"] }) {
  const theme = useThemeTokens();
  const style = getRecommendationStyle(recommendation, theme);
  return (
    <span
      className="inline-flex rounded-full px-2 py-0.5 text-[10px] font-bold"
      style={{ backgroundColor: style.pillBg, color: style.pillColor }}
    >
      {RECOMMENDATION_LABEL[recommendation]}
    </span>
  );
}

function getRecommendationStyle(
  recommendation: BuyDecision["recommendation"],
  theme: ReturnType<typeof useThemeTokens>,
) {
  switch (recommendation) {
    case "StrongBuy":
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
