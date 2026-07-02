import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { ensureMarketHubStarted, getMarketHubConnection } from "@/lib/marketHub";
import type { TradePrint } from "@/types";

const MAX_TRADES = 80;

function normalizeTrade(raw: Record<string, unknown>): TradePrint | null {
  const symbol = String(raw.symbol ?? raw.Symbol ?? "").toUpperCase();
  const side = String(raw.side ?? raw.Side ?? "");
  const price = Number(raw.price ?? raw.Price ?? 0);
  const volume = Number(raw.volume ?? raw.Volume ?? 0);
  const at = String(raw.at ?? raw.At ?? "");
  if (!symbol || price <= 0 || volume <= 0) return null;
  const normalizedSide = side === "Sell" ? "Sell" : "Buy";
  return { symbol, side: normalizedSide, price, volume, at };
}

export function useLiveTrades(sideFilter: "All" | "Buy" | "Sell" = "All") {
  const [trades, setTrades] = useState<TradePrint[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      const list = await api.getTradePrints(MAX_TRADES);
      setTrades(list);
    } catch {
      setTrades([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
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
      const print = normalizeTrade(raw as Record<string, unknown>);
      if (!print) return;
      setTrades((prev) => {
        const key = `${print.symbol}-${print.at}-${print.volume}`;
        if (prev.some((t) => `${t.symbol}-${t.at}-${t.volume}` === key)) return prev;
        return [print, ...prev].slice(0, MAX_TRADES);
      });
    };

    ensureMarketHubStarted()
      .then((conn) => {
        hub = conn;
        hub.on("TradePrintCreated", onTrade);
      })
      .catch(() => undefined);

    return () => {
      mounted = false;
      hub?.off("TradePrintCreated", onTrade);
    };
  }, []);

  const filtered =
    sideFilter === "All" ? trades : trades.filter((t) => t.side === sideFilter);

  return { trades: filtered, loading, refresh: load };
}
