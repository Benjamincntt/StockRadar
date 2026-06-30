import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { LiveMiniQuote, LiveMiniSparkline } from "@/components/ui/LiveMiniQuote";
import { useSparklines } from "@/hooks/useSparklines";
import {
  formatDateTime,
  formatCooldownRemaining,
  formatShortDate,
  parseApiDate,
} from "@/lib/utils";
import type { EngineTrust, Opportunity } from "@/types";
import { EntryPointBadge } from "@/components/entry/EntryPointCard";
import { BuyRecommendationBadge } from "@/components/entry/BuyDecisionCard";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ScorePill, PredictedHitPill } from "@/components/ui/ScorePill";
import { RealtimeOrderList } from "@/components/alerts/RealtimeOrderList";
import { IntradayMonitorStatusLine } from "@/components/alerts/IntradayMonitorStatusLine";
import { useLiveAlerts } from "@/hooks/useLiveAlerts";
import { useThemeTokens } from "@/context/ThemeContext";

export function HomePage() {
  const theme = useThemeTokens();
  const [opportunities, setOpportunities] = useState<Opportunity[]>([]);
  const [oppMeta, setOppMeta] = useState({
    hasFreshData: false,
    statusMessage: null as string | null,
    generatedAt: null as string | null,
    needsAnalysis: false,
    canRunAnalysis: true,
    analysisAvailableAt: null as string | null,
    engineTrust: null as EngineTrust | null,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [analysisRunning, setAnalysisRunning] = useState(false);
  const [analysisError, setAnalysisError] = useState<string | null>(null);
  const [analysisSuccess, setAnalysisSuccess] = useState<string | null>(null);
  const [cooldownTick, setCooldownTick] = useState(() => Date.now());
  const opportunitySymbols = useMemo(
    () => opportunities.map((o) => o.symbol),
    [opportunities],
  );
  const { alerts: universeAlerts, loading: universeLoading } = useLiveAlerts(
    "All",
    "universe",
    { opportunitySymbols },
  );

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
      engineTrust: list.engineTrust ?? null,
    });
    return list;
  }, []);

  useEffect(() => {
    const id = setInterval(() => setCooldownTick(Date.now()), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    loadOpportunities()
      .catch(() => setError("Không thể tải dữ liệu. Hãy chạy backend trước."))
      .finally(() => setLoading(false));
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

  const generatedAtLabel = oppMeta.generatedAt
    ? formatDateTime(oppMeta.generatedAt)
    : null;

  const trust = oppMeta.engineTrust;
  const engineSubtitle = useMemo(() => {
    if (!trust) {
      return generatedAtLabel
        ? `Adaptive engine · cập nhật ${generatedAtLabel}`
        : "Xếp hạng theo P(thành công) + Buy Score";
    }
    const parts: string[] = [];
    if (trust.dataAsOfDate) {
      parts.push(`Dữ liệu T-1: ${formatShortDate(trust.dataAsOfDate)}`);
    }
    if (trust.measuredCount7d > 0 && trust.winRate7d != null) {
      parts.push(`Win 7d ${trust.winRate7d}% (${trust.goodCount7d}/${trust.measuredCount7d})`);
    }
    if (trust.calibrationSamples > 0) {
      parts.push(`Cal ×${trust.calibrationGlobalFactor.toFixed(2)}`);
    }
    if (generatedAtLabel) {
      parts.push(`cập nhật ${generatedAtLabel}`);
    }
    return parts.length > 0 ? parts.join(" · ") : "Adaptive engine";
  }, [trust, generatedAtLabel]);

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

  useSymbolSubscriptions(opportunitySymbols);
  const sparklines = useSparklines(opportunitySymbols, !loading && opportunities.length > 0);

  if (error) {
    return (
      <div
        className="rounded-2xl border border-outline-variant p-4 text-sm text-negative"
        style={{ backgroundColor: theme.redSoft }}
      >
        {error}
      </div>
    );
  }

  if (loading) {
    return (
      <div className="grid gap-4 lg:grid-cols-2 lg:items-start">
        <Card>
          <div className="mb-3 h-10 animate-pulse rounded-xl bg-surface-low" />
          <div className="space-y-2">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="h-16 animate-pulse rounded-2xl bg-surface-low" />
            ))}
          </div>
        </Card>
        <Card>
          <div className="mb-3 h-8 w-40 animate-pulse rounded-lg bg-surface-low" />
          <div className="space-y-2">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-12 animate-pulse rounded-xl bg-surface-low" />
            ))}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div className="grid gap-6 lg:grid-cols-2 lg:items-start">
      <Card wave>
        <div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <SectionTitle
            title="⭐ Top Opportunities"
            subtitle={engineSubtitle}
          />
          <button
            type="button"
            onClick={handleRunAnalysis}
            disabled={!canPressAnalysis}
            className="shrink-0 rounded-xl border border-primary/35 bg-primary/10 px-4 py-2 text-sm font-medium text-primary transition-colors hover:bg-primary/15 disabled:opacity-50"
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

        {trust?.shadowModeEnabled && trust.shadowStatusMessage && (
          <p className="mb-3 text-xs text-on-surface-variant">
            Shadow: {trust.shadowStatusMessage}
          </p>
        )}

        {inAnalysisCooldown && cooldownHint && !analysisRunning && (
          <p className="mb-3 text-xs text-on-surface-variant">
            Phân tích thành công gần đây — chờ thêm {cooldownHint} để chạy lại.
          </p>
        )}

        {analysisSuccess && (
          <p className="mb-3 text-sm text-primary">{analysisSuccess}</p>
        )}
        {analysisError && (
          <p className="mb-3 text-sm text-negative">{analysisError}</p>
        )}

        {!oppMeta.hasFreshData && (
          <div className="mb-3 rounded-2xl border border-outline-variant bg-surface-low px-3 py-3 text-sm">
            <p className="font-medium text-on-surface">Chưa có danh sách cơ hội mới</p>
            <p className="mt-1 text-on-surface-variant">
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
                className="flex items-center gap-2 rounded-2xl bg-surface-low px-2 py-2.5 transition-colors hover:bg-surface-high lg:gap-4 lg:px-3 lg:py-3"
              >
                <span className="w-4 shrink-0 text-xs font-bold text-on-surface-variant">{i + 1}</span>
                <div className="w-28 shrink-0">
                  <span className="font-bold text-on-surface">{item.symbol}</span>
                  <div className="mt-1 flex flex-wrap items-center gap-1">
                    <ScorePill score={item.score} />
                    <PredictedHitPill
                      percent={item.predictedHitPercent}
                      sampleCount={item.predictedSampleCount}
                    />
                  </div>
                  {item.setupDna && (
                    <p className="mt-1 line-clamp-2 text-[9px] leading-snug text-on-surface-variant">
                      {item.setupDna}
                    </p>
                  )}
                  <div className="mt-1 flex flex-wrap gap-1">
                    {item.recommendation && <BuyRecommendationBadge recommendation={item.recommendation} />}
                    {item.entryPoint && <EntryPointBadge entry={item.entryPoint} />}
                  </div>
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
            <p className="text-sm text-on-surface-variant">Không có mã nào đạt tiêu chí Smart Money hôm nay.</p>
          )
        )}
      </Card>

      <Card wave>
        <SectionTitle
          title="Tín hiệu mới nhất"
          subtitle="Lệnh đột biến trong phiên — toàn universe Job 1 · ⭐ = Top cơ hội + Watchlist"
        />
        <IntradayMonitorStatusLine className="mb-3" />
        <RealtimeOrderList
          alerts={universeAlerts}
          loading={universeLoading}
          category="All"
          emptyMessage="Chưa có lệnh đột biến. Cần universe (Job 1) và quét trong phiên đang chạy."
          showFilters={false}
          readOnly
        />
      </Card>
    </div>
  );
}
