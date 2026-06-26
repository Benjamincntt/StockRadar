import type { Alert, AlertCategory } from "@/types";
import { RealtimeOrderRow } from "@/components/alerts/RealtimeOrderRow";
import { theme } from "@/theme/tokens";

export type AlertFeed = "opportunity" | "universe";

interface RealtimeOrderListProps {
  alerts: Alert[];
  loading: boolean;
  category: AlertCategory;
  onCategoryChange?: (category: AlertCategory) => void;
  isViewed?: (id: string) => boolean;
  onMarkViewed?: (id: string) => void;
  emptyMessage: string;
  showFilters?: boolean;
  readOnly?: boolean;
}

const categories: { key: AlertCategory; label: string }[] = [
  { key: "All", label: "Tất cả" },
  { key: "Buy", label: "Tăng" },
  { key: "Sell", label: "Giảm" },
];

export function RealtimeOrderList({
  alerts,
  loading,
  category,
  onCategoryChange,
  isViewed,
  onMarkViewed,
  emptyMessage,
  showFilters = true,
  readOnly = false,
}: RealtimeOrderListProps) {
  return (
    <>
      {showFilters && onCategoryChange && (
        <div className="mb-3 flex gap-2">
          {categories.map(({ key, label }) => (
            <button
              key={key}
              type="button"
              onClick={() => onCategoryChange(key)}
              className="rounded-full px-4 py-2 text-xs font-semibold"
              style={{
                backgroundColor: category === key ? theme.blueBg : theme.surfaceMuted,
                color: category === key ? theme.blue : theme.textMuted,
              }}
            >
              {label}
            </button>
          ))}
        </div>
      )}

      {loading && alerts.length === 0 && (
        <p className="py-6 text-center text-sm text-gray-500">Đang tải...</p>
      )}

      {!loading && alerts.length === 0 && (
        <p className="py-6 text-center text-sm text-gray-500">{emptyMessage}</p>
      )}

      <div className="space-y-2">
        {alerts.map((alert) => (
          <RealtimeOrderRow
            key={alert.id}
            alert={alert}
            readOnly={readOnly}
            viewed={readOnly ? false : isViewed?.(alert.id)}
            onMarkViewed={onMarkViewed}
          />
        ))}
      </div>
    </>
  );
}
