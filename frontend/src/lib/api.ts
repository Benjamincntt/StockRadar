import type {

  Alert,

  AlertCategory,

  AlertFeedScope,

  CriteriaSummary,

  OpportunityPerformanceSummary,

  DailyAnalysisResult,

  Job1Result,

  Job1Status,

  MarketOverview,

  OpportunitiesList,

  PagedResult,

  QuoteTick,

  RadarFilters,

  RadarLiveQuery,

  RadarLiveSnapshot,

  RadarItem,

  Sector,

  SectorCatalogItem,

  Signal,

  SignalType,

  SparklineSeries,

  StockChart,

  StockDetail,

  WatchlistItem,

} from "@/types";

import { getToken, type AuthUser } from "@/lib/auth";



const API_BASE = import.meta.env.VITE_API_URL ?? "/api/v1";
const SYNC_KEY = import.meta.env.VITE_SYNC_API_KEY ?? "dev-sync-key-change-me";

function syncHeaders(): HeadersInit {
  return { "X-Sync-Key": SYNC_KEY, "Content-Type": "application/json" };
}



async function request<T>(path: string, init?: RequestInit): Promise<T> {

  const token = getToken();

  const headers = new Headers(init?.headers);

  if (token) headers.set("Authorization", `Bearer ${token}`);

  if (init?.body && !headers.has("Content-Type")) {

    headers.set("Content-Type", "application/json");

  }



  const response = await fetch(`${API_BASE}${path}`, { ...init, headers });

  if (!response.ok) {

    const body = await response.json().catch(() => ({}));

    const problem = body as { detail?: string; message?: string };

    const message = problem.detail ?? problem.message ?? `API error ${response.status}`;

    throw new Error(message);

  }

  if (response.status === 204) return undefined as T;

  return response.json() as Promise<T>;

}



function unwrap<T>(result: PagedResult<T>): T[] {

  return result.items;

}



export const api = {

  login: (email: string, password: string) =>

    request<AuthUser>("/auth/tokens", {

      method: "POST",

      body: JSON.stringify({ email, password }),

    }),

  register: (email: string, password: string, displayName: string) =>

    request<AuthUser>("/users", {

      method: "POST",

      body: JSON.stringify({ email, password, displayName }),

    }),

  getMarketOverview: () => request<MarketOverview>("/market"),
  getQuoteSnapshot: () => request<QuoteTick[]>("/market/quotes"),

  getSparklines: (symbols: string[]) => {
    if (symbols.length === 0) return Promise.resolve([] as SparklineSeries[]);
    const q = encodeURIComponent(symbols.join(","));
    return request<SparklineSeries[]>(`/market/sparklines?symbols=${q}`);
  },

  getSectors: async (pageSize = 5) =>

    unwrap(await request<PagedResult<Sector>>(`/sectors?page=1&pageSize=${pageSize}`)),

  getOpportunities: async (pageSize = 15) =>
    request<OpportunitiesList>(`/opportunities?page=1&pageSize=${pageSize}`),

  getOpportunitySymbols: () => request<string[]>("/opportunities/symbols"),

  runOpportunityAnalysis: () =>
    request<DailyAnalysisResult>("/opportunities/run-analysis", { method: "POST" }),

  getJob1Status: () => request<Job1Status>("/market/jobs/history/status"),

  runJob1Fast: () =>
    request<Job1Result>("/market/jobs/history", {
      method: "POST",
      headers: syncHeaders(),
      body: JSON.stringify({ mode: "fast" }),
    }),

  runJob1Night: () =>
    request<Job1Result>("/market/jobs/history/night", {
      method: "POST",
      headers: syncHeaders(),
      body: JSON.stringify({ mode: "night" }),
    }),

  getSignals: async (pageSize = 10) =>

    unwrap(await request<PagedResult<Signal>>(`/signals?page=1&pageSize=${pageSize}`)),

  getRadar: async (filters: RadarFilters, page = 1, pageSize = 50) => {

    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });

    if (filters.breakout) params.set("breakout", "true");

    if (filters.accumulation) params.set("accumulation", "true");

    if (filters.relativeStrength) params.set("relativeStrength", "true");

    if (filters.volumeSpike) params.set("volumeSpike", "true");

    if (filters.shakeout) params.set("shakeout", "true");

    if (filters.distribution) params.set("distribution", "true");

    if (filters.sector) params.set("sector", filters.sector);

    return unwrap(await request<PagedResult<RadarItem>>(`/radar-items?${params}`));
  },

  getRadarLive: async (query: RadarLiveQuery = {}) => {
    const params = new URLSearchParams();
    if (query.minSessionVolume != null)
      params.set("minSessionVolume", String(query.minSessionVolume));
    if (query.minAbsChangePercent != null)
      params.set("minAbsChangePercent", String(query.minAbsChangePercent));
    if (query.direction) params.set("direction", query.direction);
    const q = params.toString();
    return request<RadarLiveSnapshot>(`/radar-items/live${q ? `?${q}` : ""}`);
  },

  getStockDetail: (symbol: string) => request<StockDetail>(`/stocks/${symbol}`),

  getStockChart: (symbol: string, interval: string) =>
    request<StockChart>(`/stocks/${symbol}/chart?interval=${encodeURIComponent(interval)}`),

  getCriteriaSummary: () => request<CriteriaSummary>("/criteria/summary"),

  getPerformanceSummary: () => request<OpportunityPerformanceSummary>("/performance/summary"),

  addTradeJournalEntry: (body: {
    symbol: string;
    action: string;
    sizePercent?: number;
    engineVerdict?: string;
    note?: string;
    buyScore?: number;
    predictedHit?: number;
    setupDna?: string;
  }) =>
    request<import("@/types").TradeJournalEntry>("/trade-journal", {
      method: "POST",
      body: JSON.stringify(body),
    }),

  getTradeJournal: (limit = 30) =>
    request<import("@/types").TradeJournalEntry[]>(`/trade-journal?limit=${limit}`),

  getAlerts: async (
    category: AlertCategory = "All",
    type?: SignalType,
    pageSize = 20,
    feed: AlertFeedScope = "opportunity",
  ) => {
    const params = new URLSearchParams({
      category,
      page: "1",
      pageSize: String(pageSize),
      feed,
    });

    if (type) params.set("type", type);

    return unwrap(await request<PagedResult<Alert>>(`/alerts?${params}`));
  },

  getWatchlist: () => request<WatchlistItem[]>("/watchlist-items"),

  addToWatchlist: (symbol: string) =>

    request<void>(`/watchlist-items/${symbol}`, { method: "PUT" }),

  removeFromWatchlist: (symbol: string) =>

    request<void>(`/watchlist-items/${symbol}`, { method: "DELETE" }),

  getSectorCatalog: () => request<SectorCatalogItem[]>("/sectors/catalog"),

  updateStockSector: (symbol: string, sector: string) =>
    request<{ symbol: string; sector: string; sectorLocked: boolean }>(
      `/stocks/${encodeURIComponent(symbol)}/sector`,
      { method: "PATCH", body: JSON.stringify({ sector }) },
    ),

};


