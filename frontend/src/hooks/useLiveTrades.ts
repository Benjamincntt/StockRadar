import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { ensureMarketHubStarted, getMarketHubConnection } from "@/lib/marketHub";
import type { TradeEvent } from "@/types";
import type { TradeLabelFilter } from "@/lib/tradeLabels";

const MAX_TRADES = 40;

function normalizeTrade(raw: Record<string, unknown>): TradeEvent | null {
  const symbol = String(raw.symbol ?? raw.Symbol ?? "").toUpperCase();
  const label = String(raw.label ?? raw.Label ?? "TrungTinh");
  const price = Number(raw.price ?? raw.Price ?? 0);
  const volume = Number(raw.volume ?? raw.Volume ?? 0);
  const valueVnd = Number(raw.valueVnd ?? raw.ValueVnd ?? price * 1000 * volume);
  const spreadPct = Number(raw.spreadPct ?? raw.SpreadPct ?? 0);
  const bookImbalance = Number(raw.bookImbalance ?? raw.BookImbalance ?? 0);
  const foreignNetDelta = Number(raw.foreignNetDelta ?? raw.ForeignNetDelta ?? 0);
  const sessionForeignNet = Number(raw.sessionForeignNet ?? raw.SessionForeignNet ?? 0);
  const sessionPropNet = Number(raw.sessionPropNet ?? raw.SessionPropNet ?? 0);
  const sessionPressure = Number(raw.sessionPressure ?? raw.SessionPressure ?? 0);
  const at = String(raw.at ?? raw.At ?? "");
  const isAggregated = Boolean(raw.isAggregated ?? raw.IsAggregated ?? false);
  if (!symbol || price <= 0 || volume <= 0) return null;
  return {
    symbol,
    label,
    price,
    volume,
    valueVnd,
    spreadPct,
    bookImbalance,
    foreignNetDelta,
    sessionForeignNet,
    sessionPropNet,
    sessionPressure,
    at,
    isAggregated,
  };
}

export function useLiveTrades(labelFilter: TradeLabelFilter = "All") {
  const [trades, setTrades] = useState<TradeEvent[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      const list = await api.getTradeEvents(
        MAX_TRADES,
        labelFilter === "All" ? undefined : labelFilter,
      );
      setTrades(list);
    } catch {
      setTrades([]);
    } finally {
      setLoading(false);
    }
  }, [labelFilter]);

  useEffect(() => {
    setLoading(true);
    load();
    const interval = setInterval(load, 30_000);
    return () => clearInterval(interval);
  }, [load]);

  useEffect(() => {
    let mounted = true;
    let hub: ReturnType<typeof getMarketHubConnection> | null = null;

    const onTrade = (...args: unknown[]) => {
      if (!mounted || args.length === 0) return;
      const raw = args[0];
      if (!raw || typeof raw !== "object") return;
      const evt = normalizeTrade(raw as Record<string, unknown>);
      if (!evt) return;

      if (labelFilter === "ForeignStrong" && evt.sessionForeignNet < 500_000) return;
      if (
        labelFilter !== "All" &&
        labelFilter !== "ForeignStrong" &&
        evt.label !== labelFilter
      )
        return;

      setTrades((prev) => {
        const key = `${evt.symbol}-${evt.at}-${evt.volume}`;
        if (prev.some((t) => `${t.symbol}-${t.at}-${t.volume}` === key)) return prev;
        return [evt, ...prev].slice(0, MAX_TRADES);
      });
    };

    ensureMarketHubStarted()
      .then((conn) => {
        hub = conn;
        hub.on("TradeEventCreated", onTrade);
      })
      .catch(() => undefined);

    return () => {
      mounted = false;
      hub?.off("TradeEventCreated", onTrade);
    };
  }, [labelFilter]);

  return { trades, loading, refresh: load };
}
