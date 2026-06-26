import { useCallback, useState } from "react";
import { getViewedAlertIds, markAlertViewed } from "@/lib/alertViewState";
import type { AlertFeedScope } from "@/types";

export function useViewedAlerts(feed: AlertFeedScope = "opportunity") {
  const [viewedIds, setViewedIds] = useState<Set<string>>(() => getViewedAlertIds(feed));

  const markViewed = useCallback(
    (id: string) => {
      setViewedIds(markAlertViewed(id, feed));
    },
    [feed],
  );

  const isViewed = useCallback((id: string) => viewedIds.has(id), [viewedIds]);

  return { markViewed, isViewed };
}
