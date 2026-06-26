import { useState } from "react";
import type { AlertCategory } from "@/types";
import { useLiveAlerts } from "@/hooks/useLiveAlerts";
import { useViewedAlerts } from "@/hooks/useViewedAlerts";
import { RealtimeOrderList } from "@/components/alerts/RealtimeOrderList";
import { Card, SectionTitle } from "@/components/ui/Card";

export function AlertsPage() {
  const [category, setCategory] = useState<AlertCategory>("All");
  const { alerts, loading } = useLiveAlerts(category, "opportunity");
  const { markViewed, isViewed } = useViewedAlerts("opportunity");

  return (
    <div className="space-y-4">
      <Card>
        <SectionTitle
          title="Lệnh realtime"
          subtitle="Khối ngoại · tự doanh · thỏa thuận · lệnh treo — Top cơ hội + Watchlist"
        />
        <RealtimeOrderList
          alerts={alerts}
          loading={loading}
          category={category}
          onCategoryChange={setCategory}
          isViewed={isViewed}
          onMarkViewed={markViewed}
          emptyMessage="Chưa có lệnh realtime. Cần mã trong Top cơ hội hoặc Watchlist và Job 3 chạy trong phiên."
        />
      </Card>
    </div>
  );
}
