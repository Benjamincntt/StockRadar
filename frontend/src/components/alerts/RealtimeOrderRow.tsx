import { Link } from "react-router-dom";
import { Star } from "lucide-react";
import { formatTime, cn } from "@/lib/utils";
import type { Alert } from "@/types";
import { theme } from "@/theme/tokens";

export function alertTitleWithoutSymbol(alert: Alert): string {
  const sym = alert.symbol.toUpperCase();
  let rest = alert.title.trim();
  if (rest.toUpperCase().startsWith(sym)) {
    rest = rest.slice(sym.length).replace(/^[\s—–\-]+/, "").trim();
  }
  return rest || alert.title;
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
  const isBuy = alert.category === "Buy";
  const showUnread = !readOnly && !viewed;
  const isPriority = Boolean(alert.inOpportunity && alert.inWatchlist);

  const style = {
    borderColor: readOnly
      ? isPriority
        ? theme.amber
        : theme.border
      : viewed
        ? theme.border
        : isBuy
          ? theme.green
          : "#fca5a5",
    borderLeftWidth: readOnly ? (isPriority ? 2 : 1) : viewed ? 1 : 4,
    backgroundColor: readOnly
      ? isPriority
        ? "#fffbeb"
        : theme.surfaceMuted
      : viewed
        ? theme.surfaceMuted
        : isBuy
          ? "#f0fdf4"
          : "#fef2f2",
    opacity: readOnly ? 1 : viewed ? 0.85 : 1,
  };

  const wrapperClass = cn(
    "w-full rounded-2xl border px-3 py-3 text-left",
    isPriority && readOnly && "priority-signal-row",
  );
  const content = (
    <>
      <div className="flex items-center justify-between gap-2">
        <p className="flex min-w-0 flex-1 items-center gap-1.5 text-sm font-semibold text-gray-900">
          {isPriority && (
            <Star
              className="h-3.5 w-3.5 shrink-0 fill-amber-400 text-amber-500"
              aria-label="Top cơ hội + Watchlist"
            />
          )}
          {showUnread && (            <span
              className="shrink-0 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase"
              style={{ backgroundColor: theme.blueBg, color: theme.blue }}
            >
              Mới
            </span>
          )}
          <span
            className="shrink-0 rounded px-1.5 py-0.5 text-[10px] font-bold uppercase"
            style={{
              backgroundColor: isBuy ? theme.greenSoft : "#fee2e2",
              color: isBuy ? theme.green : "#b91c1c",
            }}
          >
            {isBuy ? "Tăng" : "Giảm"}
          </span>
          <Link
            to={`/stocks/${alert.symbol}`}
            onClick={(e) => e.stopPropagation()}
            className="shrink-0 font-bold hover:underline"
            style={{ color: theme.green }}
          >
            {alert.symbol}
          </Link>
          <span className="truncate">{alertTitleWithoutSymbol(alert)}</span>
        </p>
        <span
          className="shrink-0 text-[10px]"
          style={{ color: readOnly || !viewed ? theme.text : theme.textMuted }}
        >
          {formatTime(alert.createdAt)}
        </span>
      </div>
      <p
        className="mt-1 whitespace-pre-line text-xs"
        style={{ color: readOnly || !viewed ? "#4b5563" : theme.textMuted }}
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
    >      {content}
    </button>
  );
}
