import type { BuyDecision, BuyRecommendation, EntryPoint, Opportunity } from "@/types";

export type StockTradeState = "Avoid" | "Watchlist" | "AwaitingTrigger" | "Actionable";

export const TRADE_STATE_LABEL_VI: Record<StockTradeState, string> = {
  Avoid: "Tránh",
  Watchlist: "Theo dõi",
  AwaitingTrigger: "Chờ kích hoạt",
  Actionable: "Vào ngay",
};

export interface ResolvedTradeState {
  state: StockTradeState;
  label: string;
  reason: string;
}

export function resolveBuyDecisionTradeState(decision: BuyDecision): ResolvedTradeState {
  if (decision.tradeState) {
    return {
      state: decision.tradeState,
      label: decision.tradeStateLabelVi ?? TRADE_STATE_LABEL_VI[decision.tradeState],
      reason: decision.tradeStateReason ?? "",
    };
  }
  return inferTradeStateFromLegacy(
    decision.recommendation,
    decision.entryPoint,
    decision.gateFailure,
    decision.buyScore,
    false,
  );
}

export function resolveOpportunityTradeState(item: Opportunity): ResolvedTradeState {
  if (item.tradeState) {
    return {
      state: item.tradeState,
      label: item.tradeStateLabelVi ?? TRADE_STATE_LABEL_VI[item.tradeState],
      reason: item.tradeStateReason ?? "",
    };
  }
  return inferTradeStateFromLegacy(
    item.recommendation ?? undefined,
    item.entryPoint ?? undefined,
    item.entryPoint?.headline,
    item.score,
    true,
  );
}

function inferTradeStateFromLegacy(
  recommendation: BuyRecommendation | string | undefined,
  entry: EntryPoint | undefined,
  gateOrHeadline: string | null | undefined,
  score: number,
  onList: boolean,
): ResolvedTradeState {
  const rec = recommendation ?? "Avoid";
  if (rec === "Avoid" && !onList) {
    return {
      state: "Avoid",
      label: TRADE_STATE_LABEL_VI.Avoid,
      reason: gateOrHeadline ?? entry?.headline ?? "Không đạt tiêu chí tối thiểu",
    };
  }

  if (entry?.status === "Ready" && rec !== "Avoid") {
    return {
      state: "Actionable",
      label: TRADE_STATE_LABEL_VI.Actionable,
      reason: score >= 80 ? "Mua mạnh — đạt chuẩn SmartMoney" : "Đạt chuẩn SmartMoney",
    };
  }

  const headline = gateOrHeadline ?? entry?.headline ?? "";
  if (
    headline.includes("MA stack") ||
    headline.includes("xu hướng dài hạn")
  ) {
    return {
      state: "AwaitingTrigger",
      label: TRADE_STATE_LABEL_VI.AwaitingTrigger,
      reason: headline,
    };
  }

  if (onList || rec !== "Avoid") {
    return {
      state: "Watchlist",
      label: TRADE_STATE_LABEL_VI.Watchlist,
      reason: headline || "Chưa phá vỡ nền / Chờ phiên kích hoạt",
    };
  }

  return {
    state: "Avoid",
    label: TRADE_STATE_LABEL_VI.Avoid,
    reason: headline || "Không đạt tiêu chí tối thiểu",
  };
}

export function getTradeStateStyle(
  state: StockTradeState,
  theme: {
    primary: string;
    onPrimary: string;
    greenBg: string;
    amber: string;
    amberBg: string;
    border: string;
    textMuted: string;
    neutralBg: string;
    secondary?: string;
  },
) {
  switch (state) {
    case "Actionable":
      return {
        bg: theme.greenBg,
        border: theme.primary,
        accent: theme.primary,
        pillBg: theme.primary,
        pillColor: theme.onPrimary,
      };
    case "AwaitingTrigger":
      return {
        bg: theme.amberBg,
        border: theme.amber,
        accent: theme.amber,
        pillBg: theme.amberBg,
        pillColor: theme.amber,
      };
    case "Watchlist":
      return {
        bg: theme.neutralBg,
        border: theme.border,
        accent: theme.secondary ?? theme.primary,
        pillBg: theme.neutralBg,
        pillColor: theme.secondary ?? theme.primary,
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
