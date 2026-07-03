import { useCallback, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { formatPercent, formatPrice, formatShortDate } from "@/lib/utils";
import type { SmartMoneyBacktestMode, SmartMoneyBacktestResult } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { useThemeTokens } from "@/context/ThemeContext";

const MODE_OPTIONS: { value: SmartMoneyBacktestMode; label: string }[] = [
  { value: "relaxed", label: "Nới (top Buy Score)" },
  { value: "strict-then-relaxed", label: "Strict → fallback" },
  { value: "strict", label: "Strict SmartMoney" },
];

export function SmartMoneyBacktestPanel() {
  const theme = useThemeTokens();
  const [days, setDays] = useState(90);
  const [maxPicks, setMaxPicks] = useState(10);
  const [holdSessions, setHoldSessions] = useState(5);
  const [mode, setMode] = useState<SmartMoneyBacktestMode>("relaxed");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SmartMoneyBacktestResult | null>(null);

  const run = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.runSmartMoneyBacktest({
        days,
        maxPicksPerDay: maxPicks,
        holdSessions,
        mode,
      });
      setResult(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Backtest thất bại — thử lại.");
    } finally {
      setLoading(false);
    }
  }, [days, maxPicks, holdSessions, mode]);

  const summary = result?.summary;
  const trades = result?.trades ?? [];
  const recentTrades = [...trades]
    .sort((a, b) => b.entryDate.localeCompare(a.entryDate) || a.symbol.localeCompare(b.symbol))
    .slice(0, 40);

  return (
    <Card>
      <SectionTitle
        title="Backtest SmartMoney"
        subtitle="Replay chiến lược trên lịch sử OHLCV — đa mã, đo win rate & drawdown"
      />

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <label className="block text-xs text-on-surface-variant">
          Số phiên
          <select
            value={days}
            onChange={(e) => setDays(Number(e.target.value))}
            className="mt-1 w-full rounded-xl border border-outline-variant bg-surface-low px-3 py-2 text-sm text-on-surface"
          >
            {[30, 60, 90, 120, 180].map((d) => (
              <option key={d} value={d}>{d} ngày</option>
            ))}
          </select>
        </label>

        <label className="block text-xs text-on-surface-variant">
          Top mã / ngày
          <select
            value={maxPicks}
            onChange={(e) => setMaxPicks(Number(e.target.value))}
            className="mt-1 w-full rounded-xl border border-outline-variant bg-surface-low px-3 py-2 text-sm text-on-surface"
          >
            {[5, 10, 15, 20, 30].map((n) => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>
        </label>

        <label className="block text-xs text-on-surface-variant">
          Giữ (T+N)
          <select
            value={holdSessions}
            onChange={(e) => setHoldSessions(Number(e.target.value))}
            className="mt-1 w-full rounded-xl border border-outline-variant bg-surface-low px-3 py-2 text-sm text-on-surface"
          >
            {[3, 5, 10, 20].map((n) => (
              <option key={n} value={n}>T+{n}</option>
            ))}
          </select>
        </label>

        <label className="block text-xs text-on-surface-variant">
          Chế độ
          <select
            value={mode}
            onChange={(e) => setMode(e.target.value as SmartMoneyBacktestMode)}
            className="mt-1 w-full rounded-xl border border-outline-variant bg-surface-low px-3 py-2 text-sm text-on-surface"
          >
            {MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </label>
      </div>

      <button
        type="button"
        onClick={run}
        disabled={loading}
        className="mt-4 w-full rounded-xl bg-primary px-4 py-2.5 text-sm font-semibold text-on-primary transition-opacity hover:opacity-90 disabled:opacity-50 sm:w-auto"
      >
        {loading ? "Đang chạy backtest..." : "Chạy backtest"}
      </button>

      {error && (
        <p className="mt-3 text-sm text-negative">{error}</p>
      )}

      {summary && (
        <>
          <p className="mt-4 text-xs text-on-surface-variant">
            {formatShortDate(summary.fromDate)} → {formatShortDate(summary.toDate)}
            {" · "}
            {summary.universeSize} mã universe
            {" · "}
            {summary.daysWithPicks}/{summary.tradingDaysScanned} ngày có tín hiệu
            {" · "}
            Ngưỡng thắng ≥{summary.successThresholdPercent}%
          </p>

          <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Metric label="Win rate" value={`${summary.winRatePercent}%`} accent />
            <Metric
              label="Lợi nhuận TB"
              value={formatPercent(summary.avgReturnPercent)}
              danger={summary.avgReturnPercent < 0}
              accent={summary.avgReturnPercent > 0}
            />
            <Metric label="Median" value={formatPercent(summary.medianReturnPercent)} />
            <Metric label="Max DD" value={`${summary.maxDrawdownPercent}%`} danger />
          </div>

          <div className="mt-3 grid grid-cols-3 gap-2 text-center text-xs">
            <div className="rounded-xl bg-surface-low py-2">
              <p className="text-on-surface-variant">Tốt</p>
              <p className="font-data text-lg font-bold" style={{ color: theme.primary }}>{summary.winCount}</p>
            </div>
            <div className="rounded-xl bg-surface-low py-2">
              <p className="text-on-surface-variant">Ngang</p>
              <p className="font-data text-lg font-bold text-on-surface">{summary.flatCount}</p>
            </div>
            <div className="rounded-xl bg-surface-low py-2">
              <p className="text-on-surface-variant">Xịt</p>
              <p className="font-data text-lg font-bold" style={{ color: theme.red }}>{summary.lossCount}</p>
            </div>
          </div>

          <p className="mt-3 text-xs text-on-surface-variant">
            Tổng {summary.totalTrades} lệnh mô phỏng (vào giá đóng cửa, thoát T+{holdSessions}).
          </p>
        </>
      )}

      {recentTrades.length > 0 && (
        <div className="mt-4">
          <p className="mb-2 text-xs font-semibold text-on-surface-variant">
            Lệnh gần đây (tối đa 40)
          </p>
          <ul className="max-h-80 space-y-1.5 overflow-y-auto">
            {recentTrades.map((t) => (
              <li
                key={`${t.symbol}-${t.entryDate}`}
                className="flex items-center justify-between gap-2 rounded-xl border border-outline-variant bg-surface-low px-3 py-2 text-xs"
              >
                <div className="min-w-0">
                  <Link to={`/stocks/${t.symbol}`} className="font-bold text-primary">
                    {t.symbol}
                  </Link>
                  <p className="text-on-surface-variant">
                    {formatShortDate(t.entryDate)}
                    {" · "}
                    điểm {t.buyScore}
                    {t.usedRelaxedFallback && " · fallback"}
                  </p>
                </div>
                <div className="shrink-0 text-right">
                  <OutcomeBadge outcome={t.outcome} returnPercent={t.returnPercent} />
                  <p className="mt-0.5 font-mono text-[10px] text-on-surface-variant">
                    {formatPrice(t.entryPrice)} → {formatPrice(t.exitPrice)}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  );
}

function Metric({
  label,
  value,
  accent,
  danger,
}: {
  label: string;
  value: string;
  accent?: boolean;
  danger?: boolean;
}) {
  const theme = useThemeTokens();
  const color = danger ? theme.red : accent ? theme.primary : theme.text;
  return (
    <div className="rounded-xl border border-outline-variant px-3 py-2 text-center">
      <p className="text-[10px] text-on-surface-variant">{label}</p>
      <p className="font-data text-lg font-bold tabular-nums" style={{ color }}>{value}</p>
    </div>
  );
}

function OutcomeBadge({ outcome, returnPercent }: { outcome: string; returnPercent: number }) {
  const theme = useThemeTokens();
  const map = {
    Good: { label: "Tốt", bg: theme.greenBg, color: theme.primary },
    Flat: { label: "Ngang", bg: theme.neutralBg, color: theme.textMuted },
    Bad: { label: "Xịt", bg: theme.redBg, color: theme.red },
  } as const;
  const style = map[outcome as keyof typeof map] ?? map.Flat;
  return (
    <span
      className="inline-block rounded-full px-2 py-0.5 text-[10px] font-bold"
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      {style.label} {formatPercent(returnPercent)}
    </span>
  );
}
