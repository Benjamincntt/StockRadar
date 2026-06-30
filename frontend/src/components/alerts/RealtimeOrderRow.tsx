import { Link } from "react-router-dom";
import { Star } from "lucide-react";
import { formatAlertTime, cn } from "@/lib/utils";
import type { Alert } from "@/types";
import { DirectionTag, NewBadge } from "@/components/ui/ScorePill";
import { useThemeTokens } from "@/context/ThemeContext";

export function alertTitleWithoutSymbol(alert: Alert): string {
  const sym = alert.symbol.toUpperCase();
  let rest = alert.title.trim();
  if (rest.toUpperCase().startsWith(sym)) {
    rest = rest.slice(sym.length).replace(/^[\s—–\-]+/, "").trim();
  }
  return rest || alert.title;
}

export function isMasterAlert(alert: Alert): boolean {
  return alert.sectorRank === "Master";
}

interface RealtimeOrderRowProps {
  alert: Alert;
  viewed?: boolean;
  onMarkViewed?: (id: string) => void;
  readOnly?: boolean;
}

export function RealtimeOrderRow({
  alert,
  viewed = false,
  onMarkViewed,
  readOnly = false,
}: RealtimeOrderRowProps) {
  const theme = useThemeTokens();
  const isBuy = alert.category === "Buy";
  const showUnread = !readOnly && !viewed;
  const isMaster = isMasterAlert(alert);
  const isPriority = isMaster || Boolean(alert.inOpportunity && alert.inWatchlist);

  const style = {
    borderColor: readOnly
      ? isMaster
        ? theme.primary
        : isPriority
          ? theme.amber
          : theme.border
      : viewed
        ? theme.border
        : isMaster
          ? theme.primary
          : isBuy
            ? theme.primary
            : theme.red,
    borderLeftWidth: readOnly ? (isMaster || isPriority ? 3 : 1) : viewed ? 1 : isMaster ? 5 : 4,
    backgroundColor: readOnly
      ? isBuy
        ? theme.alertBuyBg
        : theme.alertSellBg
      : viewed
        ? theme.surfaceMuted
        : isMaster
          ? theme.greenBg
          : isBuy
            ? theme.alertBuyBg
            : theme.alertSellBg,
    opacity: readOnly ? 1 : viewed ? 0.85 : 1,
  };

  const wrapperClass = cn(
    "w-full rounded-xl border px-3 py-3 text-left",
    isPriority && readOnly && "priority-signal-row",
  );

  const content = (
    <>
      <div className="flex items-center justify-between gap-2">
        <p className="flex min-w-0 flex-1 items-center gap-1.5 text-sm font-semibold text-on-surface">
          {isMaster && (
            <span
              className="shrink-0 rounded px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide"
              style={{ backgroundColor: theme.primary, color: theme.onPrimary }}
            >
              Master
            </span>
          )}
          {isPriority && !isMaster && (
            <Star
              className="h-3.5 w-3.5 shrink-0 fill-warning text-warning"
              aria-label="Top cơ hội + Watchlist"
            />
          )}
          {showUnread && <NewBadge />}
          <DirectionTag direction={isBuy ? "up" : "down"} />
          <Link
            to={`/stocks/${alert.symbol}`}
            onClick={(e) => e.stopPropagation()}
            className="shrink-0 font-bold text-primary hover:underline"
          >
            {alert.symbol}
          </Link>
          <span className="truncate text-on-surface-variant">{alertTitleWithoutSymbol(alert)}</span>
        </p>
        <span
          className="shrink-0 font-data text-[10px]"
          style={{ color: readOnly || !viewed ? theme.textMuted : theme.textSubtle }}
        >
          {formatAlertTime(alert.createdAt)}
        </span>
      </div>
      <p
        className="mt-1 whitespace-pre-line text-xs text-on-surface-variant"
        style={{ opacity: readOnly || !viewed ? 1 : 0.75 }}
      >
        {alert.message}
      </p>
    </>
  );

  if (readOnly) {
    return (
      <div className={wrapperClass} style={style}>
        {content}
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => onMarkViewed?.(alert.id)}
      className={cn(wrapperClass, "transition-colors")}
      style={style}
    >
      {content}
    </button>
  );
}
