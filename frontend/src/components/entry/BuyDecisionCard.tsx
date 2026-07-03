import { useState } from "react";
import { useThemeTokens } from "@/context/ThemeContext";
import type { BuyDecision, BuyScoreComponent, EntryPoint } from "@/types";
import { EntryPointCard } from "@/components/entry/EntryPointCard";
import { PredictedHitPill } from "@/components/ui/ScorePill";
import { Check, ChevronDown, ChevronUp, X } from "lucide-react";

const RECOMMENDATION_LABEL: Record<BuyDecision["recommendation"], string> = {
  StrongBuy: "Mua mạnh",
  Watch: "Theo dõi",
  Avoid: "Tránh",
};

export function showsEntryPointCard(entry: EntryPoint) {
  return entry.status === "Ready" || entry.status === "Watch" || entry.status === "Late";
}

export function showsPriceLevels(entry: EntryPoint) {
  return entry.status === "Ready" || entry.status === "Watch";
}

function showsMergedInvalidCard(decision: BuyDecision) {
  return !showsEntryPointCard(decision.entryPoint) && decision.entryPoint.checklist.length > 0;
}

function visibleBreakdown(decision: BuyDecision): BuyScoreComponent[] {
  if (!decision.gateFailure) return decision.breakdown;
  const incomplete = decision.breakdown.filter((item) => item.points < item.maxPoints);
  if (incomplete.length > 0) return incomplete;
  return decision.breakdown.filter((item) => item.points <= 0);
}

export function showsEntryPointCardForDecision(decision: BuyDecision) {
  const entry = decision.entryPoint;
  if (decision.recommendation === "Avoid" && entry.status === "Ready") return false;
  return entry.status === "Ready" || entry.status === "Watch" || entry.status === "Late";
}

export function BuyDecisionCard({ decision }: { decision: BuyDecision }) {
  const theme = useThemeTokens();
  const [showBreakdown, setShowBreakdown] = useState(false);

  if (showsMergedInvalidCard(decision)) {
    return (
      <MergedInsufficientCard
        decision={decision}
        showBreakdown={showBreakdown}
        onToggleBreakdown={() => setShowBreakdown((v) => !v)}
      />
    );
  }

  const recStyle = getRecommendationStyle(decision.recommendation, theme);
  const breakdown = visibleBreakdown(decision);
  const hasHardGate = Boolean(decision.gateFailure);
  const displayScore = hasHardGate ? decision.actionScore : decision.buyScore;
  const scoreSuffix = hasHardGate ? "điểm hành động" : "/ 100";

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
              {!hasHardGate && decision.reasons.length > 0 && (
                <p className="mt-2 text-xs leading-relaxed text-on-surface-variant">
                  {decision.reasons.slice(0, 3).join(" · ")}
                </p>
              )}
              {hasHardGate && (
                <p className="mt-2 text-xs text-on-surface-variant">
                  Tiềm năng ranking {decision.buyScore}/100
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
                  {displayScore}
                </p>
                {!hasHardGate && (
                  <PredictedHitPill
                    percent={decision.predictedHitPercent}
                    sampleCount={decision.predictedSampleCount}
                  />
                )}
              </div>
              <p className="text-[10px] text-on-surface-variant">{scoreSuffix}</p>
              {decision.setupDna && (
                <p className="mt-1 max-w-[9rem] text-right text-[9px] leading-snug text-on-surface-variant">
                  {decision.setupDna}
                </p>
              )}
            </div>
          </div>
        </div>

        {breakdown.length > 0 && (
          <>
            <button
              type="button"
              onClick={() => setShowBreakdown((v) => !v)}
              className="flex w-full items-center justify-between border-t border-outline-variant bg-surface px-4 py-2.5 text-left"
            >
              <span className="text-xs font-semibold text-on-surface-variant">
                {hasHardGate ? "Điều kiện chưa đạt" : "Chi tiết điểm cộng"}
              </span>
              {showBreakdown ? (
                <ChevronUp className="h-4 w-4 text-on-surface-variant" />
              ) : (
                <ChevronDown className="h-4 w-4 text-on-surface-variant" />
              )}
            </button>

            {showBreakdown && (
              <ul className="border-t border-outline-variant bg-surface px-4 py-3">
                {!hasHardGate &&
                  decision.topExplainLines &&
                  decision.topExplainLines.length > 0 && (
                    <li className="mb-2 border-b border-outline-variant pb-2 text-[10px] text-on-surface-variant">
                      {decision.topExplainLines.map((line) => (
                        <p key={line} className="py-0.5">
                          ✓ {line}
                        </p>
                      ))}
                    </li>
                  )}
                {breakdown.map((item) => (
                  <li key={item.id} className="py-1.5 text-xs">
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-medium text-on-surface">{item.label}</span>
                      <span className="font-data shrink-0 font-bold tabular-nums text-on-surface">
                        +{item.points}
                        <span className="font-normal text-on-surface-variant">/{item.maxPoints}</span>
                      </span>
                    </div>
                    {item.detail && (
                      <p className="mt-0.5 text-[10px] leading-snug text-on-surface-variant">{item.detail}</p>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </div>

      {showsEntryPointCardForDecision(decision) && (
        <EntryPointCard entry={decision.entryPoint} buyScore={decision.buyScore} />
      )}
    </div>
  );
}

function MergedInsufficientCard({
  decision,
  showBreakdown,
  onToggleBreakdown,
}: {
  decision: BuyDecision;
  showBreakdown: boolean;
  onToggleBreakdown: () => void;
}) {
  const theme = useThemeTokens();
  const style = getRecommendationStyle(decision.recommendation, theme);
  const entry = decision.entryPoint;
  const breakdown = visibleBreakdown(decision);
  const headline = decision.gateFailure ?? entry.headline;

  return (
    <div
      className="overflow-hidden rounded-2xl border"
      style={{ borderColor: style.border, backgroundColor: style.bg }}
    >
      <div className="px-4 pt-4 pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="label-caps text-on-surface-variant">Chưa đủ điều kiện</p>
            <h3 className="mt-1 text-base font-bold leading-snug text-on-surface">{headline}</h3>
            {entry.action && (
              <p className="mt-2 text-sm leading-relaxed text-on-surface-variant">{entry.action}</p>
            )}
            <p className="mt-2 text-xs text-on-surface-variant">
              Tiềm năng ranking {decision.buyScore}/100
            </p>
          </div>
          <div className="shrink-0 text-right">
            <span
              className="inline-block rounded-full px-2.5 py-1 text-[11px] font-bold"
              style={{ backgroundColor: style.pillBg, color: style.pillColor }}
            >
              {RECOMMENDATION_LABEL[decision.recommendation]}
            </span>
            <p className="font-data mt-2 text-3xl font-bold tabular-nums" style={{ color: style.accent }}>
              {decision.actionScore}
            </p>
            <p className="text-[10px] text-on-surface-variant">điểm hành động</p>
          </div>
        </div>
      </div>

      {breakdown.length > 0 && (
        <>
          <button
            type="button"
            onClick={onToggleBreakdown}
            className="flex w-full items-center justify-between border-t border-outline-variant bg-surface px-4 py-2.5 text-left"
          >
            <span className="text-xs font-semibold text-on-surface-variant">Điều kiện chưa đạt</span>
            {showBreakdown ? (
              <ChevronUp className="h-4 w-4 text-on-surface-variant" />
            ) : (
              <ChevronDown className="h-4 w-4 text-on-surface-variant" />
            )}
          </button>
          {showBreakdown && (
            <ul className="border-t border-outline-variant bg-surface px-4 py-3">
              {breakdown.map((item) => (
                <li key={item.id} className="py-1.5 text-xs">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium text-on-surface">{item.label}</span>
                    <span className="font-data shrink-0 font-bold tabular-nums text-on-surface">
                      +{item.points}
                      <span className="font-normal text-on-surface-variant">/{item.maxPoints}</span>
                    </span>
                  </div>
                  {item.detail && (
                    <p className="mt-0.5 text-[10px] leading-snug text-on-surface-variant">{item.detail}</p>
                  )}
                </li>
              ))}
            </ul>
          )}
        </>
      )}

      <EntryChecklist checklist={entry.checklist} />
    </div>
  );
}

function EntryChecklist({ checklist }: { checklist: EntryPoint["checklist"] }) {
  const theme = useThemeTokens();
  if (checklist.length === 0) return null;

  return (
    <div className="border-t border-outline-variant bg-surface px-4 py-3">
      <p className="label-caps text-on-surface-variant">Checklist điểm vào</p>
      <ul className="mt-2 space-y-1.5">
        {checklist.map((item) => (
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
