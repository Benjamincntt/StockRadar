import type { ChartBar, OhlcvBar } from "@/types";

/** ~1 năm giao dịch HOSE (không hiển thị full lịch sử DB). */
export const CHART_ONE_YEAR_SESSIONS = 252;

export function sliceChartToOneYear<T>(bars: readonly T[]): T[] {
  if (bars.length <= CHART_ONE_YEAR_SESSIONS) return [...bars];
  return bars.slice(-CHART_ONE_YEAR_SESSIONS);
}

export function ohlcvToChartBars(history: OhlcvBar[]): ChartBar[] {
  return history.map((b) => ({
    time: b.date,
    open: b.open,
    high: b.high,
    low: b.low,
    close: b.close,
    volume: b.volume,
  }));
}

/** Biểu đồ ngày: tối đa 1 năm giao dịch gần nhất. */
export function buildDailyChartFromHistory(history: OhlcvBar[]): ChartBar[] {
  if (history.length === 0) return [];
  return ohlcvToChartBars(history.slice(-CHART_ONE_YEAR_SESSIONS));
}
