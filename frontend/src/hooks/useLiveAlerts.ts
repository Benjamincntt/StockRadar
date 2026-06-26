import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "@/lib/api";
import { ensureMarketHubStarted, getMarketHubConnection } from "@/lib/marketHub";
import type { Alert, AlertCategory, AlertFeedScope } from "@/types";

const POLL_MS = 15_000;
const MAX_ALERTS = 20;
const TRACKED_REFRESH_MS = 60_000;

function enrichAlert(
  raw: Alert,
  opportunitySymbols: Set<string>,
  watchlistSymbols: Set<string>,
): Alert {
  const sym = raw.symbol.toUpperCase();
  return {
    ...raw,
    inOpportunity: raw.inOpportunity ?? opportunitySymbols.has(sym),
    inWatchlist: raw.inWatchlist ?? watchlistSymbols.has(sym),
  };
}

export function useLiveAlerts(category: AlertCategory, feed: AlertFeedScope = "opportunity") {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [loading, setLoading] = useState(true);
  const opportunitySymbolsRef = useRef<Set<string>>(new Set());
  const watchlistSymbolsRef = useRef<Set<string>>(new Set());
  const trackedSymbolsRef = useRef<Set<string>>(new Set());

  const refreshTrackedSymbols = useCallback(async () => {
    const [opportunities, watchlist] = await Promise.all([
      api.getOpportunities(30),
      api.getWatchlist(),
    ]);
    opportunitySymbolsRef.current = new Set(
      opportunities.items.map((o) => o.symbol.toUpperCase()),
    );
    watchlistSymbolsRef.current = new Set(
      watchlist.map((w) => w.symbol.toUpperCase()),
    );
    trackedSymbolsRef.current = new Set([
      ...opportunitySymbolsRef.current,
      ...watchlistSymbolsRef.current,
    ]);
  }, []);

  useEffect(() => {
    void refreshTrackedSymbols();
    const timer = setInterval(() => {
      void refreshTrackedSymbols().catch(() => undefined);
    }, TRACKED_REFRESH_MS);
    return () => clearInterval(timer);
  }, [refreshTrackedSymbols]);

  const load = useCallback(async () => {
    const data = await api.getAlerts(category, undefined, MAX_ALERTS, feed);
    setAlerts(
      data.map((a) =>
        enrichAlert(a, opportunitySymbolsRef.current, watchlistSymbolsRef.current),
      ),
    );
    setLoading(false);
  }, [category, feed]);

  useEffect(() => {
    setLoading(true);
    void refreshTrackedSymbols()
      .then(load)
      .catch(() => setLoading(false));
    const timer = setInterval(() => void load(), POLL_MS);
    return () => clearInterval(timer);
  }, [load, refreshTrackedSymbols]);

  useEffect(() => {
    let active = true;

    const shouldInclude = (symbol: string) => {
      if (feed === "universe") return true;
      return trackedSymbolsRef.current.has(symbol.toUpperCase());
    };

    const onAlert = (raw: Alert) => {
      if (!active) return;
      if (!shouldInclude(raw.symbol)) return;

      const alert = enrichAlert(
        raw,
        opportunitySymbolsRef.current,
        watchlistSymbolsRef.current,
      );

      if (category !== "All" && alert.category !== category) return;

      setAlerts((prev) => {
        if (prev.some((a) => a.id === alert.id)) return prev;
        const next = [alert, ...prev].slice(0, MAX_ALERTS);
        if (feed === "universe") {
          return [...next].sort((a, b) => {
            const aPriority = a.inOpportunity && a.inWatchlist ? 1 : 0;
            const bPriority = b.inOpportunity && b.inWatchlist ? 1 : 0;
            if (bPriority !== aPriority) return bPriority - aPriority;
            return b.createdAt.localeCompare(a.createdAt);
          });
        }
        return next;
      });
    };

    const connection = getMarketHubConnection();
    connection.on("AlertCreated", onAlert);

    void ensureMarketHubStarted().catch(() => {
      /* poll fallback */
    });

    return () => {
      active = false;
      connection.off("AlertCreated", onAlert);
    };
  }, [category, feed]);

  return { alerts, loading, refresh: load };
}
