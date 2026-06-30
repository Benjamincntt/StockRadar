import type { BasePricePeriod, ChartBar, OhlcvBar } from "@/types";

export interface ResolvedAccumulationZone {
  id: string;
  periodIndex: number;
  fromDate: string;
  toDate: string;
  sessionDays: number;
  low: number;
  high: number;
  startIndex: number;
  endIndex: number;
  visible: boolean;
}

function dayKey(iso: string): number {
  const d = new Date(iso.includes("T") ? iso : `${iso}T12:00:00`);
  if (Number.isNaN(d.getTime())) return 0;
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

function dayKeyFromYmd(ymd: string): number {
  return dayKey(ymd);
}

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
export function buildDailyChartFromHistory(
  history: OhlcvBar[],
  _periods: BasePricePeriod[],
  _paddingSessions = 8,
): ChartBar[] {
  if (history.length === 0) return [];
  return ohlcvToChartBars(history.slice(-CHART_ONE_YEAR_SESSIONS));
}

export function resolveAccumulationZones(
  bars: ChartBar[],
  periods: BasePricePeriod[],
): ResolvedAccumulationZone[] {
  if (bars.length === 0 || periods.length === 0) return [];

  const barDays = bars.map((b) => dayKey(b.time));
  const firstBar = barDays[0];
  const lastBar = barDays[barDays.length - 1];

  return periods.map((p, i) => {
    const from = dayKeyFromYmd(p.fromDate);
    const to = dayKeyFromYmd(p.toDate);
    const visible = to >= firstBar && from <= lastBar;

    if (!visible) {
      return {
        id: `acc-${i}`,
        periodIndex: i,
        fromDate: p.fromDate,
        toDate: p.toDate,
        sessionDays: p.sessionDays,
        low: p.low,
        high: p.high,
        startIndex: -1,
        endIndex: -1,
        visible: false,
      };
    }

    let startIndex = barDays.findIndex((d) => d >= from);
    if (startIndex < 0) startIndex = 0;

    let endIndex = -1;
    for (let j = barDays.length - 1; j >= 0; j--) {
      if (barDays[j] <= to) {
        endIndex = j;
        break;
      }
    }
    if (endIndex < 0) endIndex = barDays.length - 1;
    if (endIndex < startIndex) endIndex = startIndex;

    return {
      id: `acc-${i}`,
      periodIndex: i,
      fromDate: p.fromDate,
      toDate: p.toDate,
      sessionDays: p.sessionDays,
      low: p.low,
      high: p.high,
      startIndex,
      endIndex,
      visible: true,
    };
  });
}

export function formatPeriodChip(fromDate: string, toDate: string): string {
  const fmt = (ymd: string) => {
    const d = new Date(`${ymd}T12:00:00`);
    if (Number.isNaN(d.getTime())) return ymd;
    return d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
  };
  return `${fmt(fromDate)}–${fmt(toDate)}`;
}
