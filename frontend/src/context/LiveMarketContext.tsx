import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import * as signalR from "@microsoft/signalr";
import { api } from "@/lib/api";
import { ensureMarketHubStarted, getMarketHubConnection } from "@/lib/marketHub";
import type { IndexTick, MarketOverview, MarketTrend, QuoteTick } from "@/types";

export type LiveConnectionState = "connecting" | "connected" | "reconnecting" | "disconnected";

interface LiveMarketContextValue {
  connectionState: LiveConnectionState;
  quotes: Record<string, QuoteTick>;
  index: IndexTick | null;
  lastUpdated: string | null;
  subscribeSymbols: (symbols: string[]) => void;
  mergeOverview: (overview: MarketOverview) => MarketOverview;
  getQuote: (symbol: string) => QuoteTick | undefined;
}

const LiveMarketContext = createContext<LiveMarketContextValue | null>(null);

const POLL_MS = 25_000;

type RawQuote = QuoteTick & {
  Symbol?: string;
  Price?: number;
  ChangePercent?: number;
  Volume?: number;
  UpdatedAt?: string;
};

type RawIndex = IndexTick & {
  Symbol?: string;
  Price?: number;
  ChangePercent?: number;
  MarketScore?: number;
  Trend?: MarketTrend | number;
  UpdatedAt?: string;
};

function parseTrend(value: unknown): MarketTrend {
  if (value === "Uptrend" || value === 1) return "Uptrend";
  if (value === "Downtrend" || value === 2) return "Downtrend";
  return "Sideway";
}

function normalizeQuote(raw: RawQuote): QuoteTick {
  const symbol = String(raw.symbol ?? raw.Symbol ?? "").toUpperCase();
  return {
    symbol,
    price: Number(raw.price ?? raw.Price ?? 0),
    changePercent: Number(raw.changePercent ?? raw.ChangePercent ?? 0),
    volume: Number(raw.volume ?? raw.Volume ?? 0),
    updatedAt: String(raw.updatedAt ?? raw.UpdatedAt ?? new Date().toISOString()),
  };
}

function normalizeIndex(raw: RawIndex): IndexTick {
  return {
    symbol: String(raw.symbol ?? raw.Symbol ?? "VNINDEX"),
    price: Number(raw.price ?? raw.Price ?? 0),
    changePercent: Number(raw.changePercent ?? raw.ChangePercent ?? 0),
    marketScore: Number(raw.marketScore ?? raw.MarketScore ?? 0),
    trend: parseTrend(raw.trend ?? raw.Trend),
    updatedAt: String(raw.updatedAt ?? raw.UpdatedAt ?? new Date().toISOString()),
  };
}

function mergeQuotes(
  prev: Record<string, QuoteTick>,
  batch: RawQuote[],
): Record<string, QuoteTick> {
  const next = { ...prev };
  for (const item of batch) {
    const quote = normalizeQuote(item);
    if (!quote.symbol || quote.price <= 0) continue;
    next[quote.symbol] = quote;
  }
  return next;
}

export function LiveMarketProvider({ children }: { children: ReactNode }) {
  const [connectionState, setConnectionState] = useState<LiveConnectionState>("connecting");
  const [quotes, setQuotes] = useState<Record<string, QuoteTick>>({});
  const [index, setIndex] = useState<IndexTick | null>(null);
  const [lastUpdated, setLastUpdated] = useState<string | null>(null);
  const subscribedRef = useRef<Set<string>>(new Set());
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const refreshSnapshot = useCallback(async () => {
    try {
      const [snapshot, overview] = await Promise.all([
        api.getQuoteSnapshot(),
        api.getMarketOverview(),
      ]);
      setQuotes((prev) => mergeQuotes(prev, snapshot));
      setIndex({
        symbol: overview.indexSymbol,
        price: overview.indexPrice,
        changePercent: overview.indexChangePercent,
        marketScore: overview.marketScore,
        trend: parseTrend(overview.trend),
        updatedAt: new Date().toISOString(),
      });
      setLastUpdated(new Date().toISOString());
    } catch {
      // API chưa sẵn sàng
    }
  }, []);

  useEffect(() => {
    let active = true;
    const connection = getMarketHubConnection();

    const onQuotes = (batch: RawQuote[]) => {
      if (!active || !Array.isArray(batch) || batch.length === 0) return;
      setQuotes((prev) => mergeQuotes(prev, batch));
      setLastUpdated(new Date().toISOString());
    };

    const onIndex = (tick: RawIndex) => {
      if (!active) return;
      setIndex(normalizeIndex(tick));
      setLastUpdated(new Date().toISOString());
    };

    connection.on("QuotesUpdated", onQuotes);
    connection.on("IndexUpdated", onIndex);

    connection.onreconnecting(() => {
      if (active) setConnectionState("reconnecting");
    });
    connection.onreconnected(async () => {
      if (!active) return;
      setConnectionState("connected");
      await refreshSnapshot();
      const symbols = [...subscribedRef.current];
      if (symbols.length > 0) await connection.invoke("Subscribe", symbols);
    });
    connection.onclose(() => {
      if (active) setConnectionState("disconnected");
    });

    (async () => {
      try {
        setConnectionState("connecting");
        await ensureMarketHubStarted();
        if (!active) return;
        setConnectionState("connected");
        await refreshSnapshot();
      } catch {
        if (!active) return;
        setConnectionState("disconnected");
        await refreshSnapshot();
      }
    })();

    return () => {
      active = false;
      connection.off("QuotesUpdated", onQuotes);
      connection.off("IndexUpdated", onIndex);
    };
  }, [refreshSnapshot]);

  // Luôn poll — backup khi worker chậm hoặc SignalR rớt
  useEffect(() => {
    const tick = () => {
      if (document.visibilityState === "hidden") return;
      void refreshSnapshot();
    };
    const timer = window.setInterval(tick, POLL_MS);
    return () => window.clearInterval(timer);
  }, [refreshSnapshot]);

  const subscribeSymbols = useCallback((symbols: string[]) => {
    const normalized = symbols.map((s) => s.toUpperCase()).filter(Boolean);
    subscribedRef.current = new Set(normalized);
    const connection = getMarketHubConnection();
    connectionRef.current = connection;
    if (connection?.state === signalR.HubConnectionState.Connected && normalized.length > 0) {
      void connection.invoke("Subscribe", normalized);
    }
  }, []);

  const getQuote = useCallback(
    (symbol: string) => quotes[symbol.toUpperCase()],
    [quotes],
  );

  const mergeOverview = useCallback(
    (overview: MarketOverview): MarketOverview => {
      if (!index) return overview;
      return {
        ...overview,
        indexPrice: index.price,
        indexChangePercent: index.changePercent,
        marketScore: index.marketScore,
        trend: index.trend,
      };
    },
    [index],
  );

  const value = useMemo(
    () => ({
      connectionState,
      quotes,
      index,
      lastUpdated,
      subscribeSymbols,
      mergeOverview,
      getQuote,
    }),
    [connectionState, quotes, index, lastUpdated, subscribeSymbols, mergeOverview, getQuote],
  );

  return <LiveMarketContext.Provider value={value}>{children}</LiveMarketContext.Provider>;
}

export function useLiveMarket() {
  const ctx = useContext(LiveMarketContext);
  if (!ctx) throw new Error("useLiveMarket must be used within LiveMarketProvider");
  return ctx;
}

export function useLiveQuote(symbol: string) {
  const { quotes } = useLiveMarket();
  return quotes[symbol.toUpperCase()];
}

export function useSymbolSubscriptions(symbols: string[]) {
  const { subscribeSymbols } = useLiveMarket();
  const key = symbols.map((s) => s.toUpperCase()).sort().join(",");

  useEffect(() => {
    if (!key) return;
    subscribeSymbols(symbols);
  }, [key, subscribeSymbols, symbols]);
}

export function usePriceFlash(value: number) {
  const prev = useRef(value);
  const [flash, setFlash] = useState<"up" | "down" | null>(null);

  useEffect(() => {
    if (prev.current === value) return;
    setFlash(value > prev.current ? "up" : "down");
    prev.current = value;
    const timer = window.setTimeout(() => setFlash(null), 700);
    return () => window.clearTimeout(timer);
  }, [value]);

  return flash;
}
