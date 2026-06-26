import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { useLiveMarket, usePriceFlash, useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { LiveMiniQuote, LiveMiniSparkline } from "@/components/ui/LiveMiniQuote";
import { useSparklines } from "@/hooks/useSparklines";
import {
  formatDateTime,
  formatPrice,
  formatCooldownRemaining,
  parseApiDate,
  trendLabel,
  cn,
} from "@/lib/utils";
import type { MarketOverview, Opportunity, Sector } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ChangePill, ScorePill } from "@/components/ui/ScorePill";
import { RealtimeOrderList } from "@/components/alerts/RealtimeOrderList";
import { useLiveAlerts } from "@/hooks/useLiveAlerts";
import { theme } from "@/theme/tokens";

export function HomePage() {
  const { mergeOverview } = useLiveMarket();
  const [overview, setOverview] = useState<MarketOverview | null>(null);
  const [sectors, setSectors] = useState<Sector[]>([]);
  const [opportunities, setOpportunities] = useState<Opportunity[]>([]);
  const [oppMeta, setOppMeta] = useState({
    hasFreshData: false,
    statusMessage: null as string | null,
    generatedAt: null as string | null,
    needsAnalysis: false,
    canRunAnalysis: true,
    analysisAvailableAt: null as string | null,
  });
  const [error, setError] = useState<string | null>(null);
  const [analysisRunning, setAnalysisRunning] = useState(false);
  const [analysisError, setAnalysisError] = useState<string | null>(null);
  const [analysisSuccess, setAnalysisSuccess] = useState<string | null>(null);
  const [cooldownTick, setCooldownTick] = useState(() => Date.now());
  const { alerts: universeAlerts, loading: universeLoading } = useLiveAlerts("All", "universe");

  const loadOpportunities = useCallback(async () => {
    const list = await api.getOpportunities();
    setOpportunities(list.items);
    setOppMeta({
      hasFreshData: list.hasFreshData,
      statusMessage: list.statusMessage ?? null,
      generatedAt: list.generatedAt ?? null,
      needsAnalysis: list.needsAnalysis,
      canRunAnalysis: list.canRunAnalysis,
      analysisAvailableAt: list.analysisAvailableAt ?? null,
    });
    return list;
  }, []);

  useEffect(() => {
    const id = setInterval(() => setCooldownTick(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    Promise.all([
      api.getMarketOverview(),
      api.getSectors(),
      loadOpportunities(),
    ])
      .then(([o, s]) => {
        setOverview(o);
        setSectors(s);
      })
      .catch(() => setError("Không thể tải dữ liệu. Hãy chạy backend trước."));
  }, [loadOpportunities]);

  const handleRunAnalysis = async () => {
    if (!canPressAnalysis) return;
    setAnalysisRunning(true);
    setAnalysisError(null);
    setAnalysisSuccess(null);
    try {
      const result = await api.runOpportunityAnalysis();
      await loadOpportunities();
      setAnalysisSuccess(
        result.opportunitiesSaved > 0
          ? `Phân tích xong: ${result.opportunitiesSaved} mã trong top (quét ${result.stocksScored} mã).`
          : `Phân tích xong: không có mã đạt SmartMoney (quét ${result.stocksScored} mã).`,
      );
    } catch (e) {
      const message =
        e instanceof Error ? e.message : "Không chạy được phân tích. Kiểm tra backend và thử lại.";
      setAnalysisError(message);
      await loadOpportunities().catch(() => undefined);
    } finally {
      setAnalysisRunning(false);
    }
  };

  const liveOverview = useMemo(
    () => (overview ? mergeOverview(overview) : null),
    [overview, mergeOverview],
  );
  const indexFlash = usePriceFlash(liveOverview?.indexPrice ?? 0);
  const generatedAtLabel = oppMeta.generatedAt
    ? formatDateTime(oppMeta.generatedAt)
    : null;

  const inAnalysisCooldown = useMemo(() => {
    if (!oppMeta.analysisAvailableAt) return false;
    const until = parseApiDate(oppMeta.analysisAvailableAt).getTime();
    return !Number.isNaN(until) && cooldownTick < until;
  }, [oppMeta.analysisAvailableAt, cooldownTick]);

  const canPressAnalysis = !analysisRunning && !inAnalysisCooldown;

  const cooldownHint =
    inAnalysisCooldown && oppMeta.analysisAvailableAt
      ? formatCooldownRemaining(oppMeta.analysisAvailableAt, cooldownTick)
      : null;

  useSymbolSubscriptions(opportunities.map((o) => o.symbol));
  const sparklines = useSparklines(opportunities.map((o) => o.symbol));

  if (error) {
    return (
      <div
        className="rounded-[20px] border p-4 text-sm text-red-600"
        style={{ backgroundColor: theme.redSoft, borderColor: theme.border }}
      >
        {error}
      </div>
    );
  }

  if (!liveOverview) {
    return <p className="text-center text-sm text-gray-500">Đang tải...</p>;
  }

  const uptrend = liveOverview.trend === "Uptrend";

  return (
    <div className="space-y-4">
      <Card padding="lg">
        <div className="flex items-start justify-between">
          <div>
            <p className="text-xs font-medium text-gray-500">{liveOverview.indexSymbol}</p>
            <p
              className={cn(
                "mt-1 text-3xl font-bold text-gray-900 inline-block rounded-md px-0.5",
                indexFlash === "up" && "live-flash-up",
                indexFlash === "down" && "live-flash-down",
              )}
            >
              {formatPrice(liveOverview.indexPrice)}
            </p>
            <ChangePill value={liveOverview.indexChangePercent} className="mt-2" />
          </div>
          <div className="text-right">
            <span
              className="inline-flex rounded-full px-3 py-1 text-[11px] font-bold"
              style={{
                backgroundColor: uptrend ? theme.greenBg : theme.redBg,
                color: uptrend ? theme.green : theme.red,
              }}
            >
              {uptrend ? "🟢" : "🔴"} {trendLabel(liveOverview.trend)}
            </span>
            <p className="mt-3 text-xs text-gray-500">Market Score</p>
            <p className="text-2xl font-bold text-gray-900">{liveOverview.marketScore}</p>
          </div>
        </div>
      </Card>

      <Card>
        <SectionTitle title="🔥 Dòng tiền ngành" subtitle="Sector Rotation" />
        <div className="space-y-2.5">
          {sectors.map((sector, i) => (
            <div key={sector.name} className="flex items-center gap-3">
              <span className="w-4 text-xs font-semibold text-gray-400">{i + 1}</span>
              <div className="flex-1">
                <div className="mb-1 flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-900">{sector.name}</span>
                  <ScorePill score={sector.score} />
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-gray-100">
                  <div
                    className="h-full rounded-full"
                    style={{
                      width: `${sector.score}%`,
                      backgroundColor: theme.green,
                      opacity: 0.35 + (sector.score / 100) * 0.65,
                    }}
                  />
                </div>
              </div>
              <ChangePill value={sector.changePercent} />
            </div>
          ))}
        </div>
      </Card>

      <Card>
        <div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <SectionTitle
            title="⭐ Top Opportunities"
            subtitle={
              generatedAtLabel
                ? `Cập nhật: ${generatedAtLabel}`
                : "Cơ hội giao dịch hôm nay"
            }
          />
          <button
            type="button"
            onClick={handleRunAnalysis}
            disabled={!canPressAnalysis}
            className="shrink-0 rounded-xl px-4 py-2 text-sm font-semibold text-white disabled:opacity-60"
            style={{ backgroundColor: theme.green }}
          >
            {analysisRunning
              ? "Đang phân tích..."
              : inAnalysisCooldown && cooldownHint
                ? `Chạy lại sau ${cooldownHint}`
                : oppMeta.hasFreshData
                  ? "Chạy lại phân tích"
                  : "Chạy phân tích"}
          </button>
        </div>

        {inAnalysisCooldown && cooldownHint && !analysisRunning && (
          <p className="mb-3 text-xs text-gray-500">
            Phân tích thành công gần đây — chờ thêm {cooldownHint} để chạy lại.
          </p>
        )}

        {analysisSuccess && (
          <p className="mb-3 text-sm" style={{ color: theme.green }}>
            {analysisSuccess}
          </p>
        )}
        {analysisError && (
          <p className="mb-3 text-sm text-red-600">{analysisError}</p>
        )}

        {!oppMeta.hasFreshData && (
          <div
            className="mb-3 rounded-2xl border px-3 py-3 text-sm"
            style={{ borderColor: theme.border, backgroundColor: theme.surfaceMuted }}
          >
            <p className="font-medium text-gray-900">Chưa có danh sách cơ hội mới</p>
            <p className="mt-1 text-gray-600">
              {oppMeta.statusMessage ??
                "Job phân tích hàng ngày chưa chạy hoặc không có dữ liệu cho phiên giao dịch hiện tại. Hệ thống không hiển thị dữ liệu cũ."}
            </p>
          </div>
        )}

        {opportunities.length > 0 ? (
          <div className="space-y-2">
            {opportunities.map((item, i) => (
              <Link
                key={item.symbol}
                to={`/stocks/${item.symbol}`}
                className="flex items-center gap-2 rounded-2xl bg-gray-50 px-2 py-2.5"
              >
                <span className="w-4 shrink-0 text-xs font-bold text-gray-400">{i + 1}</span>
                <div className="w-14 shrink-0">
                  <span className="font-bold text-gray-900">{item.symbol}</span>
                  <ScorePill score={item.score} className="mt-1" />
                </div>
                <LiveMiniSparkline
                  symbol={item.symbol}
                  series={sparklines[item.symbol]}
                  fallbackChangePercent={item.changePercent}
                />
                <LiveMiniQuote
                  symbol={item.symbol}
                  fallbackPrice={item.price}
                  fallbackChangePercent={item.changePercent}
                />
              </Link>
            ))}
          </div>
        ) : (
          oppMeta.hasFreshData && (
            <p className="text-sm text-gray-500">Không có mã nào đạt tiêu chí Smart Money hôm nay.</p>
          )
        )}
      </Card>

      <Card>
        <SectionTitle
          title="Tín hiệu mới nhất"
          subtitle="Lệnh đột biến trong phiên — toàn universe Job 1 · ⭐ = Top cơ hội + Watchlist"
        />
        <RealtimeOrderList
          alerts={universeAlerts}
          loading={universeLoading}
          category="All"
          emptyMessage="Chưa có lệnh đột biến. Cần Job 1 (universe) và Job 3 chạy trong phiên."
          showFilters={false}
          readOnly
        />
      </Card>
    </div>
  );
}
