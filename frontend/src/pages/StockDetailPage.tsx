import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { useLiveMarket, useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { LivePrice } from "@/components/ui/LivePrice";
import { LiveChangePill } from "@/components/ui/LiveChangePill";
import { ChartTimeframeBar } from "@/components/ui/ChartTimeframeBar";
import { buildDailyChartFromHistory, resolveAccumulationZones } from "@/lib/chartAccumulation";
import { formatPercent, formatPrice, getBaseSessionDaysStyle } from "@/lib/utils";
import type { BuyDecision, ChartBar, ChartInterval, CriterionScore, StockDetail } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ScorePill } from "@/components/ui/ScorePill";
import { PriceVolumeChart } from "@/components/ui/PriceVolumeChart";
import { AccumulationLegend } from "@/components/chart/AccumulationLegend";
import { useThemeTokens } from "@/context/ThemeContext";
import { BuyDecisionCard } from "@/components/entry/BuyDecisionCard";
import { SwingDecisionCard } from "@/components/entry/SwingDecisionCard";
import { ChevronLeft } from "lucide-react";

export function StockDetailPage() {
  const theme = useThemeTokens();
  const { symbol = "" } = useParams();
  const [detail, setDetail] = useState<StockDetail | null>(null);
  const [chartBars, setChartBars] = useState<ChartBar[]>([]);
  const [chartInterval, setChartInterval] = useState<ChartInterval>("1D");
  const [chartLoading, setChartLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [added, setAdded] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [highlightZone, setHighlightZone] = useState<number | null>(null);

    useEffect(() => {
    if (!symbol) return;
    setError(null);
    api
      .getStockDetail(symbol)
      .then(setDetail)
      .catch((e) => {
        const msg = e instanceof Error ? e.message : "";
        setError(
          msg.includes("404") || msg.toLowerCase().includes("not found")
            ? "Không tìm thấy mã cổ phiếu."
            : `Không tải được chi tiết mã: ${msg || "lỗi server"}`,
        );
      });
  }, [symbol]);

  useEffect(() => {
    if (!symbol) return;
    setChartLoading(true);

    const useDbDaily =
      chartInterval === "1D" &&
      detail?.history?.length &&
      detail.basePrice?.periods?.length;

    if (useDbDaily) {
      setChartBars(buildDailyChartFromHistory(detail.history, detail.basePrice!.periods));
      setChartLoading(false);
      return;
    }

    api
      .getStockChart(symbol, chartInterval)
      .then((chart) => setChartBars(chart.bars))
      .catch(() => setChartBars([]))
      .finally(() => setChartLoading(false));
  }, [symbol, chartInterval, detail?.history, detail?.basePrice?.periods]);

  const resolvedZones = useMemo(() => {
    if (chartInterval !== "1D" || !detail?.basePrice?.periods?.length) return [];
    return resolveAccumulationZones(chartBars, detail.basePrice.periods);
  }, [chartBars, chartInterval, detail?.basePrice?.periods]);

  const zoneVisibleFlags = useMemo(
    () => resolvedZones.map((z) => z.visible),
    [resolvedZones],
  );

  useSymbolSubscriptions(symbol ? [symbol] : []);
  const { quotes } = useLiveMarket();
  const live = symbol ? quotes[symbol] : undefined;

  const addWatchlist = async () => {
    if (!symbol) return;
    await api.addToWatchlist(symbol);
    setAdded(true);
  };

  if (error) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-negative">{error}</p>
        <Link to="/" className="text-sm font-medium text-primary">
          ← Quay lại trang chủ
        </Link>
      </div>
    );
  }

  if (!detail) {
    return <p className="text-center text-sm text-on-surface-variant">Đang tải {symbol}...</p>;
  }

  const baseSessionStyle = detail.basePrice
    ? getBaseSessionDaysStyle(detail.basePrice.totalSessionDays)
    : null;

  return (
    <div className="space-y-4 pb-20">
      <div className="flex items-center gap-3">
        <Link
          to="/"
          className="flex h-9 w-9 items-center justify-center rounded-full bg-surface-high text-on-surface"
          aria-label="Quay lại"
        >
          <ChevronLeft className="h-5 w-5" />
        </Link>
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-lg font-bold text-on-surface">{detail.symbol}</h2>
          <p className="truncate text-xs text-on-surface-variant">{detail.name}</p>
        </div>
        <ScorePill score={detail.score} className="!px-3 !py-1.5 !text-sm" />
      </div>

      <Card padding="lg">
        <p className="text-xs text-on-surface-variant">{detail.sector}</p>
        <div className="mt-3 flex items-end justify-between">
          <LivePrice
            symbol={detail.symbol}
            fallbackPrice={detail.price}
            className="font-data text-3xl font-bold text-on-surface"
          />
          <LiveChangePill symbol={detail.symbol} fallback={detail.changePercent} />
        </div>
        <div className="mt-4 grid grid-cols-2 gap-2 text-center">
          <div className="rounded-xl bg-surface-low py-2">
            <p className="label-caps text-on-surface-variant">Volume Ratio</p>
            <p className="font-data text-sm font-bold text-on-surface">{detail.volumeRatio}x</p>
          </div>
          <div className="rounded-xl bg-surface-low py-2">
            <p className="label-caps text-on-surface-variant">RS</p>
            <p
              className="font-data text-sm font-bold"
              style={{ color: detail.relativeStrength >= 0 ? theme.primary : theme.red }}
            >
              {formatPercent(detail.relativeStrength)}
            </p>
          </div>
        </div>
      </Card>

      {detail.buyDecision.swingDecision && (
        <SwingDecisionCard swing={detail.buyDecision.swingDecision} />
      )}

      <TradeJournalActions symbol={detail.symbol} buyDecision={detail.buyDecision} />

      <BuyDecisionCard decision={detail.buyDecision} />

      <Card padding="sm">
        <div className="mb-3 flex flex-col gap-2">
          <SectionTitle
            title="Biểu đồ giá & khối lượng"
            subtitle={
              detail.basePrice?.periods.length
                ? "Khung Ngày — vùng tím = giai đoạn tích lũy"
                : "KBS · TradingView style"
            }
          />
          <ChartTimeframeBar value={chartInterval} onChange={setChartInterval} />
        </div>
        <PriceVolumeChart
          symbol={detail.symbol}
          name={detail.name}
          interval={chartInterval}
          bars={chartBars}
          loading={chartLoading}
          livePrice={live?.price}
          liveChangePercent={live?.changePercent}
          accumulationPeriods={detail.basePrice?.periods}
          baseZone={
            detail.basePrice
              ? { low: detail.basePrice.baseLow, high: detail.basePrice.baseHigh }
              : undefined
          }
          highlightZoneIndex={highlightZone}
          resolvedZones={resolvedZones}
        />
        {detail.basePrice && detail.basePrice.periods.length > 0 && (
          <AccumulationLegend
            periods={detail.basePrice.periods}
            visibleFlags={zoneVisibleFlags}
            activeIndex={highlightZone}
            onSelect={(i) => {
              if (zoneVisibleFlags[i] === false) return;
              setHighlightZone((prev) => (prev === i ? null : i));
            }}
          />
        )}
        {detail.basePrice && chartInterval !== "1D" && detail.basePrice.periods.length > 0 && (
          <p className="mt-2 text-center text-[11px] text-on-surface-variant">
            Chuyển khung <span className="font-semibold text-primary">D</span> để xem vùng tích lũy trên biểu đồ
          </p>
        )}
      </Card>

      {detail.basePrice && baseSessionStyle && (
        <Card>
          <SectionTitle
            title="Nền giá"
            subtitle={
              detail.basePrice.totalBases > 1
                ? `Nền ${detail.basePrice.baseIndex} — vùng gần giá hiện tại (${detail.basePrice.totalBases} nền)`
                : "Pipeline: impulse → nén ATR → compression → volume khô"
            }
          />
          <div className="space-y-3">
            <div className="grid grid-cols-3 gap-2 text-center">
              <div className="rounded-xl bg-surface-low py-2.5 px-2">
                <p className="label-caps text-on-surface-variant">Vùng giá</p>
                <p className="font-data mt-0.5 text-sm font-bold text-on-surface">
                  {formatPrice(detail.basePrice.baseLow)} – {formatPrice(detail.basePrice.baseHigh)}
                </p>
              </div>
              <div
                className="rounded-xl border py-2.5 px-2"
                style={{
                  backgroundColor: baseSessionStyle.backgroundColor,
                  borderColor: baseSessionStyle.borderColor,
                }}
              >
                <p className="label-caps text-on-surface-variant">Số phiên trong nền</p>
                <p
                  className="font-data mt-0.5 text-lg font-bold tabular-nums"
                  style={{ color: baseSessionStyle.color }}
                >
                  {detail.basePrice.totalSessionDays}
                  <span className="ml-0.5 text-sm font-semibold">phiên</span>
                </p>
              </div>
              <div className="rounded-xl border border-outline-variant py-2.5 px-2">
                <p className="label-caps text-on-surface-variant">Chất lượng nền</p>
                <p
                  className="font-data mt-0.5 text-lg font-bold tabular-nums"
                  style={{
                    color:
                      detail.basePrice.qualityScore >= 80
                        ? theme.green
                        : detail.basePrice.qualityScore >= 60
                          ? theme.primary
                          : detail.basePrice.qualityScore >= 40
                            ? theme.text
                            : theme.red,
                  }}
                >
                  {detail.basePrice.qualityScore}
                  <span className="ml-0.5 text-xs font-semibold text-on-surface-variant">/100</span>
                </p>
              </div>
            </div>
            <div className="flex items-center justify-between rounded-xl border border-outline-variant px-3 py-2">
              <span className="text-xs text-on-surface-variant">
                Lọc FOMO: so với đỉnh nền {formatPrice(detail.basePrice.filterBaseHigh)}
              </span>
              <span
                className="font-data text-sm font-bold"
                style={{
                  color:
                    detail.basePrice.exceedsRunupFilter
                      ? theme.red
                      : detail.basePrice.filterGainFromBasePercent > 0
                        ? theme.primary
                        : theme.text,
                }}
              >
                {formatPercent(detail.basePrice.filterGainFromBasePercent)}
              </span>
            </div>
          </div>
        </Card>
      )}

      <Card>
        <button
          type="button"
          onClick={() => setShowAdvanced((v) => !v)}
          className="flex w-full items-center justify-between text-left"
        >
          <SectionTitle
            title="Chỉ báo nâng cao"
            subtitle={showAdvanced ? "Ẩn chi tiết" : "Mở rộng để xem điểm chỉ báo đơn / bộ"}
          />
          <span className="text-xs font-semibold text-primary">{showAdvanced ? "Thu gọn" : "Xem"}</span>
        </button>

        {showAdvanced && (
          <>
            <p className="label-caps mb-2 mt-3 text-on-surface-variant">Top 10 chỉ báo đơn</p>
            <ul className="space-y-2">
              {detail.patternScores
                .filter((p) => p.rank <= 10)
                .map((p) => (
                  <CriterionRow key={p.id} item={p} />
                ))}
            </ul>

            <p className="label-caps mb-2 mt-4 text-on-surface-variant">Bộ chỉ báo kết hợp</p>
            <ul className="space-y-2">
              {detail.patternScores
                .filter((p) => p.rank > 10 && p.rank <= 16)
                .map((p) => (
                  <CriterionRow key={p.id} item={p} levelBadge />
                ))}
            </ul>

            <p className="label-caps mb-2 mt-4 text-on-surface-variant">Top cơ hội — Buy Score</p>
            <ul className="space-y-2">
              {detail.patternScores
                .filter((p) => p.group === "Top cơ hội")
                .map((p) => (
                  <CriterionRow key={p.id} item={p} opportunityBadge />
                ))}
            </ul>
          </>
        )}
      </Card>

      <Card>
        <SectionTitle title="Tóm tắt phân tích" />
        <p className="text-sm leading-relaxed text-on-surface-variant">{detail.summary}</p>
      </Card>

      <Card>
        <SectionTitle title="Các tín hiệu" />
        <div className="flex flex-wrap gap-2">
          {detail.activeSignals.map((signal) => (
            <span
              key={signal}
              className="rounded-full px-3 py-1.5 text-xs font-semibold"
              style={{ backgroundColor: theme.greenBg, color: theme.primary }}
            >
              ✓ {signal}
            </span>
          ))}
        </div>
      </Card>

      <Card>
        <SectionTitle title="Các mức giá" subtitle="Tham chiếu nhanh (20 phiên) — ưu tiên mức trong Điểm vào" />
        <div className="grid grid-cols-2 gap-2">
          <PriceBox label="Điểm mua" value={detail.entryPoint.entryPrice || detail.buyZone} />
          <PriceBox label="Cắt lỗ" value={detail.entryPoint.stopLoss || detail.stopLoss} danger />
          <PriceBox label="Kích hoạt" value={detail.entryPoint.triggerPrice || detail.resistance} />
          <PriceBox label="Mục tiêu" value={detail.entryPoint.targetPrice || detail.target} accent />
        </div>
      </Card>

      <div className="ios-safe-bottom fixed bottom-16 left-0 right-0 z-20 px-4">
        <div className="mx-auto" style={{ maxWidth: theme.maxWidth }}>
          <button
            type="button"
            onClick={addWatchlist}
            disabled={added}
            className="w-full rounded-xl bg-primary py-3.5 text-sm font-bold text-on-primary shadow-lg disabled:opacity-60"
          >
            {added ? "Đã thêm Watchlist" : "+ Thêm vào Watchlist"}
          </button>
        </div>
      </div>
    </div>
  );
}

function CriterionRow({
  item,
  levelBadge,
  opportunityBadge,
}: {
  item: CriterionScore;
  levelBadge?: boolean;
  opportunityBadge?: boolean;
}) {
  const theme = useThemeTokens();
  const badgeStyle = opportunityBadge
    ? { backgroundColor: theme.amberBg, color: theme.amber }
    : levelBadge
      ? { backgroundColor: theme.greenBg, color: theme.primaryContainer }
      : { backgroundColor: theme.greenBg, color: theme.primary };

  const badgeLabel = opportunityBadge
    ? item.rank - 19
    : levelBadge
      ? item.rank - 10
      : item.rank;

  return (
    <li className="rounded-xl border border-outline-variant bg-surface-low px-3 py-2.5">
      <div className="flex items-center gap-2">
        <span
          className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg text-xs font-bold"
          style={badgeStyle}
        >
          {badgeLabel}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <div>
              <span className="text-sm font-semibold text-on-surface">{item.label}</span>
              <p className="text-[10px] text-on-surface-variant">{item.group}</p>
            </div>
            <div className="flex items-center gap-2">
              <BiasTag bias={item.bias} />
              <span
                className="font-data text-sm font-bold tabular-nums"
                style={{
                  color: item.score >= 70 ? theme.primary : item.score >= 50 ? theme.text : theme.textMuted,
                }}
              >
                {item.score}
              </span>
            </div>
          </div>
          <p className="mt-1 text-xs text-on-surface-variant">{item.summary}</p>
        </div>
      </div>
    </li>
  );
}

function BiasTag({ bias }: { bias: string }) {
  const theme = useThemeTokens();
  const map = {
    Bullish: { label: "Tăng", bg: theme.greenBg, color: theme.primary },
    Bearish: { label: "Giảm", bg: theme.redBg, color: theme.red },
    Neutral: { label: "Trung tính", bg: theme.neutralBg, color: theme.textMuted },
  } as const;
  const style = map[bias as keyof typeof map] ?? map.Neutral;
  return (
    <span
      className="rounded-full px-2 py-0.5 text-[10px] font-semibold"
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      {style.label}
    </span>
  );
}

function TradeJournalActions({
  symbol,
  buyDecision,
}: {
  symbol: string;
  buyDecision: BuyDecision;
}) {
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);
  const swing = buyDecision.swingDecision;

  const log = async (action: string) => {
    setBusy(true);
    setMsg(null);
    try {
      await api.addTradeJournalEntry({
        symbol,
        action,
        sizePercent: swing?.suggestedSizePercent,
        engineVerdict: swing?.verdict,
        buyScore: buyDecision.buyScore,
        predictedHit: swing?.adjustedHitPercent ?? buyDecision.predictedHitPercent,
        setupDna: buyDecision.setupDna ?? undefined,
      });
      setMsg(
        action === "Entered"
          ? "Đã ghi nhận vào lệnh — engine học thêm."
          : "Đã ghi nhận — engine điều chỉnh cal cá nhân.",
      );
    } catch {
      setMsg("Không lưu được journal.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <SectionTitle title="Trade journal" subtitle="Ghi nhận để engine học style swing của bạn" />
      <div className="mt-2 flex flex-wrap gap-2">
        <button
          type="button"
          disabled={busy}
          onClick={() => log("Entered")}
          className="rounded-xl bg-primary px-3 py-2 text-xs font-semibold text-on-primary disabled:opacity-60"
        >
          Đã vào lệnh
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => log("Skipped")}
          className="rounded-xl border border-outline-variant px-3 py-2 text-xs font-semibold text-on-surface disabled:opacity-60"
        >
          Bỏ qua setup
        </button>
        <button
          type="button"
          disabled={busy}
          onClick={() => log("Vetoed")}
          className="rounded-xl border border-outline-variant px-3 py-2 text-xs font-semibold text-on-surface-variant disabled:opacity-60"
        >
          Veto (không tin engine)
        </button>
      </div>
      {msg && <p className="mt-2 text-xs text-primary">{msg}</p>}
    </Card>
  );
}

function PriceBox({
  label,
  value,
  danger,
  accent,
}: {
  label: string;
  value: number;
  danger?: boolean;
  accent?: boolean;
}) {
  const theme = useThemeTokens();
  const color = danger ? theme.red : accent ? theme.primary : theme.text;
  return (
    <div className="rounded-2xl bg-surface-low p-3">
      <p className="label-caps text-on-surface-variant">{label}</p>
      <p className="font-data mt-1 text-lg font-bold" style={{ color }}>
        {formatPrice(value)}
      </p>
    </div>
  );
}
