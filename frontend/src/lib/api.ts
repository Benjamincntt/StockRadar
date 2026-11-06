import type {
  HieuQuaTomTatV3,
  TinHieuV3,
  ViTheV3,
  ViTheV3Paged,
  MarketOverview,
  QuoteTick,
  SectorCatalogItem,
  SparklineSeries,
  StockChart,
  StockDetail,
  RightsEvent,
  WatchlistDto,
  WatchlistItem,
} from "@/types";

import { getToken, type AuthUser } from "@/lib/auth";



const API_BASE = import.meta.env.VITE_API_URL ?? "/api/v1";
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

  getStockDetail: (symbol: string) => request<StockDetail>(`/stocks/${symbol}`),

  getRightsEvents: (symbol: string) =>
    request<RightsEvent[]>(`/stocks/${encodeURIComponent(symbol)}/rights-events`),

  addRightsEvent: (
    symbol: string,
    body: {
      exDate: string;
      cash: number;
      dilution: number;
      oldShares?: number;
      newShares?: number;
      issuePrice?: number;
    },
  ) =>
    request<RightsEvent>(`/stocks/${encodeURIComponent(symbol)}/rights-events`, {
      method: "POST",
      body: JSON.stringify(body),
    }),

  getStockChart: (symbol: string, interval: string) =>
    request<StockChart>(`/stocks/${symbol}/chart?interval=${encodeURIComponent(interval)}`),

  /**
   * Các tín hiệu kịch bản V3 (P1++) của một mã — GET /stocks/{symbol}/v3, mới nhất trước.
   * Trả mảng rỗng khi mã chưa có tín hiệu V3.
   */
  getV3TinHieuTheoSymbol: (symbol: string) =>
    request<TinHieuV3[]>(`/stocks/${encodeURIComponent(symbol)}/v3`),

  getWatchlists: () => request<WatchlistDto[]>("/watchlists"),

  createWatchlist: (name: string) =>
    request<WatchlistDto>("/watchlists", {
      method: "POST",
      body: JSON.stringify({ name }),
    }),

  deleteWatchlist: (id: number) =>
    request<void>(`/watchlists/${id}`, { method: "DELETE" }),

  renameWatchlist: (id: number, name: string) =>
    request<WatchlistDto>(`/watchlists/${id}`, {
      method: "PATCH",
      body: JSON.stringify({ name }),
    }),

  getWatchlistById: (id: number) => request<WatchlistDto>(`/watchlists/${id}`),

  getWatchlistItems: (id: number) =>
    request<WatchlistItem[]>(`/watchlists/${id}/items`),

  addToWatchlistById: (watchlistId: number, symbol: string) =>
    request<void>(`/watchlists/${watchlistId}/items/${encodeURIComponent(symbol)}`, {
      method: "PUT",
    }),

  removeFromWatchlistById: (watchlistId: number, symbol: string) =>
    request<void>(`/watchlists/${watchlistId}/items/${encodeURIComponent(symbol)}`, {
      method: "DELETE",
    }),

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

  getV3HieuQuaTomTat: (tu?: string, den?: string) => {
    const params = new URLSearchParams();
    if (tu) params.set("tu", tu);
    if (den) params.set("den", den);
    const q = params.toString();
    return request<HieuQuaTomTatV3>(`/v3/hieu-qua/tom-tat${q ? `?${q}` : ""}`);
  },

  /** Vị thế V3 đang giữ cho Home (lọc DangGiu, bỏ chờ mua). */
  getViTheV3DangGiu: async () =>
    (await request<ViTheV3[]>("/v3/hieu-qua/vi-the-dang-giu")).filter(
      (v) => v.trangThai === "DangGiu",
    ),

  getV3HieuQuaLichSu: (page = 1, size = 20, status?: string) => {
    const params = new URLSearchParams({ page: String(page), size: String(size) });
    if (status) params.set("status", status);
    return request<ViTheV3Paged>(`/v3/hieu-qua/lich-su?${params}`);
  },

};


