import type { Alert, AlertCategory } from "@/types";
import { RealtimeOrderRow } from "@/components/alerts/RealtimeOrderRow";
import { FilterChips } from "@/components/ui/ScorePill";

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
        <FilterChips value={category} options={categories} onChange={onCategoryChange} className="mb-3" />
      )}

      {loading && alerts.length === 0 && (
        <p className="py-6 text-center text-sm text-on-surface-variant">Đang tải...</p>
      )}

      {!loading && alerts.length === 0 && (
        <p className="py-6 text-center text-sm text-on-surface-variant">{emptyMessage}</p>
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
