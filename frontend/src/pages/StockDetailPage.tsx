import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { useLiveMarket, useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { LivePrice } from "@/components/ui/LivePrice";
import { LiveChangePill } from "@/components/ui/LiveChangePill";
import { ChartTimeframeBar } from "@/components/ui/ChartTimeframeBar";
import { formatBasePricePeriods, formatPercent, formatPrice, getBaseSessionDaysStyle } from "@/lib/utils";
import type { ChartBar, ChartInterval, CriterionScore, StockDetail } from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { ScorePill } from "@/components/ui/ScorePill";
import { PriceVolumeChart } from "@/components/ui/PriceVolumeChart";
import { theme } from "@/theme/tokens";
import { ChevronLeft } from "lucide-react";

export function StockDetailPage() {
  const { symbol = "" } = useParams();
  const [detail, setDetail] = useState<StockDetail | null>(null);
  const [chartBars, setChartBars] = useState<ChartBar[]>([]);
  const [chartInterval, setChartInterval] = useState<ChartInterval>("1D");
  const [chartLoading, setChartLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [added, setAdded] = useState(false);

  useEffect(() => {
    if (!symbol) return;
    api
      .getStockDetail(symbol)
      .then(setDetail)
      .catch(() => setError("Không tìm thấy mã cổ phiếu."));
  }, [symbol]);

  useEffect(() => {
    if (!symbol) return;
    setChartLoading(true);
    api
      .getStockChart(symbol, chartInterval)
      .then((chart) => setChartBars(chart.bars))
      .catch(() => setChartBars([]))
      .finally(() => setChartLoading(false));
  }, [symbol, chartInterval]);

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
        <p className="text-sm text-red-600">{error}</p>
        <Link to="/" className="text-sm font-medium" style={{ color: theme.green }}>
          ← Quay lại trang chủ
        </Link>
      </div>
    );
  }

  if (!detail) {
    return <p className="text-center text-sm text-gray-500">Đang tải {symbol}...</p>;
  }

  const baseSessionStyle = detail.basePrice
    ? getBaseSessionDaysStyle(detail.basePrice.totalSessionDays)
    : null;

  return (
    <div className="space-y-4">
      <Link
        to="/"
        className="inline-flex items-center gap-1 text-sm font-medium text-gray-600"
      >
        <ChevronLeft className="h-4 w-4" />
        Trang chủ
      </Link>

      <Card padding="lg">
        <div className="flex items-start justify-between">
          <div>
            <h2 className="text-2xl font-bold text-gray-900">{detail.symbol}</h2>
            <p className="text-sm text-gray-500">{detail.name}</p>
            <p className="mt-1 text-xs text-gray-400">{detail.sector}</p>
          </div>
          <ScorePill score={detail.score} className="!px-3 !py-1.5 !text-sm" />
        </div>
        <div className="mt-4 flex items-end justify-between">
          <LivePrice symbol={detail.symbol} fallbackPrice={detail.price} className="text-3xl font-bold text-gray-900" />
          <LiveChangePill symbol={detail.symbol} fallback={detail.changePercent} />
        </div>
        <div className="mt-4 grid grid-cols-2 gap-2 text-center">
          <div className="rounded-xl bg-gray-50 py-2">
            <p className="text-[10px] text-gray-500">Volume Ratio</p>
            <p className="text-sm font-bold text-gray-900">{detail.volumeRatio}x</p>
          </div>
          <div className="rounded-xl bg-gray-50 py-2">
            <p className="text-[10px] text-gray-500">RS</p>
            <p
              className="text-sm font-bold"
              style={{ color: detail.relativeStrength >= 0 ? theme.green : theme.red }}
            >
              {formatPercent(detail.relativeStrength)}
            </p>
          </div>
        </div>
      </Card>

      {detail.basePrice && baseSessionStyle && (
        <Card>
          <SectionTitle
            title="Nền giá"
            subtitle={
              detail.basePrice.totalBases > 1
                ? `Nền ${detail.basePrice.baseIndex} — vùng giá gần giá hiện tại (có ${detail.basePrice.totalBases} nền)`
                : "Vùng tích lũy — biên độ dưới 15%"
            }
          />
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-2 text-center">
              <div className="rounded-xl bg-gray-50 py-2.5 px-2">
                <p className="text-[10px] text-gray-500">Vùng giá</p>
                <p className="mt-0.5 text-sm font-bold text-gray-900">
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
                <p className="text-[10px] text-gray-500">Số phiên trong nền</p>
                <p
                  className="mt-0.5 text-lg font-bold tabular-nums"
                  style={{ color: baseSessionStyle.color }}
                >
                  {detail.basePrice.totalSessionDays}
                  <span className="ml-0.5 text-sm font-semibold">phiên</span>
                </p>
              </div>
            </div>
            <div className="rounded-xl bg-gray-50 px-3 py-2">
              <p className="text-[10px] text-gray-500">Giai đoạn tích lũy</p>
              <p className="mt-1 text-sm font-medium text-gray-800">
                {formatBasePricePeriods(detail.basePrice.periods)}
              </p>
              <p className="mt-1 text-[11px] text-gray-500">
                Chỉ các đoạn cùng mức giá (nền {detail.basePrice.baseIndex}); bỏ qua giai đoạn biến động mạnh ở giữa.
              </p>
            </div>
            <div className="flex items-center justify-between rounded-xl border px-3 py-2" style={{ borderColor: theme.border }}>
              <span className="text-xs text-gray-500">Lọc FOMO: so với đỉnh nền {formatPrice(detail.basePrice.filterBaseHigh)}</span>
              <span
                className="text-sm font-bold"
                style={{
                  color:
                    detail.basePrice.exceedsRunupFilter
                      ? theme.red
                      : detail.basePrice.filterGainFromBasePercent > 0
                        ? theme.green
                        : theme.text,
                }}
              >
                {formatPercent(detail.basePrice.filterGainFromBasePercent)}
              </span>
            </div>
          </div>
        </Card>
      )}

      <Card padding="sm">
        <div className="mb-3 flex flex-col gap-2">
          <SectionTitle title="Biểu đồ giá & khối lượng" subtitle="KBS · TradingView style" />
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
        />
      </Card>

      <Card>
        <SectionTitle
          title="Chấm điểm phân tích"
          subtitle={`Đơn ${detail.patternCompositeScore} · Bộ ${detail.bundleCompositeScore} · Top cơ hội ${detail.opportunityCompositeScore}/100${detail.passesSmartMoneyFilter ? "" : " · chưa đạt bộ lọc"}`}
        />

        <p className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
          Top 10 chỉ báo đơn
        </p>
        <ul className="space-y-2">
          {detail.patternScores
            .filter((p) => p.rank <= 10)
            .map((p) => (
              <CriterionRow key={p.id} item={p} />
            ))}
        </ul>

        <p className="mb-2 mt-4 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
          Bộ chỉ báo kết hợp
        </p>
        <ul className="space-y-2">
          {detail.patternScores
            .filter((p) => p.rank > 10 && p.rank <= 16)
            .map((p) => (
              <CriterionRow key={p.id} item={p} levelBadge />
            ))}
        </ul>

        <p className="mb-2 mt-4 text-[11px] font-semibold uppercase tracking-wide text-gray-400">
          Top cơ hội — SmartMoney
        </p>
        <ul className="space-y-2">
          {detail.patternScores
            .filter((p) => p.group === "Top cơ hội")
            .map((p) => (
              <CriterionRow key={p.id} item={p} opportunityBadge />
            ))}
        </ul>
      </Card>

      <Card>
        <SectionTitle title="Tóm tắt phân tích" />
        <p className="text-sm leading-relaxed text-gray-600">{detail.summary}</p>
      </Card>

      <Card>
        <SectionTitle title="Các tín hiệu" />
        <div className="flex flex-wrap gap-2">
          {detail.activeSignals.map((signal) => (
            <span
              key={signal}
              className="rounded-full px-3 py-1.5 text-xs font-semibold"
              style={{ backgroundColor: theme.greenBg, color: theme.green }}
            >
              ✓ {signal}
            </span>
          ))}
        </div>
      </Card>

      <Card>
        <SectionTitle title="Các mức giá" />
        <div className="grid grid-cols-2 gap-2">
          <PriceBox label="Điểm mua" value={detail.buyZone} />
          <PriceBox label="Cắt lỗ" value={detail.stopLoss} danger />
          <PriceBox label="Kháng cự" value={detail.resistance} />
          <PriceBox label="Mục tiêu" value={detail.target} accent />
        </div>
      </Card>

      <button
        type="button"
        onClick={addWatchlist}
        disabled={added}
        className="w-full rounded-2xl py-3.5 text-sm font-semibold text-white disabled:opacity-60"
        style={{ backgroundColor: theme.green }}
      >
        {added ? "Đã thêm Watchlist" : "+ Thêm vào Watchlist"}
      </button>
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
  const badgeStyle = opportunityBadge
    ? { backgroundColor: "#fff7ed", color: "#ea580c" }
    : levelBadge
      ? { backgroundColor: "#eef2ff", color: "#4f46e5" }
      : { backgroundColor: theme.greenBg, color: theme.green };

  const badgeLabel = opportunityBadge
    ? item.rank - 19
    : levelBadge
      ? item.rank - 10
      : item.rank;

  return (
    <li
      className="rounded-xl border px-3 py-2.5"
      style={{ borderColor: theme.border, backgroundColor: theme.surfaceMuted }}
    >
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
              <span className="text-sm font-semibold text-gray-900">{item.label}</span>
              <p className="text-[10px] text-gray-400">{item.group}</p>
            </div>
            <div className="flex items-center gap-2">
              <BiasTag bias={item.bias} />
              <span
                className="text-sm font-bold tabular-nums"
                style={{
                  color: item.score >= 70 ? theme.green : item.score >= 50 ? theme.text : theme.textMuted,
                }}
              >
                {item.score}
              </span>
            </div>
          </div>
          <p className="mt-1 text-xs text-gray-500">{item.summary}</p>
        </div>
      </div>
    </li>
  );
}

function BiasTag({ bias }: { bias: string }) {
  const map = {
    Bullish: { label: "Tăng", bg: theme.greenBg, color: theme.green },
    Bearish: { label: "Giảm", bg: "#fef2f2", color: theme.red },
    Neutral: { label: "Trung tính", bg: "#f3f4f6", color: theme.textMuted },
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
  const color = danger ? theme.red : accent ? theme.green : theme.text;
  return (
    <div className="rounded-2xl bg-gray-50 p-3">
      <p className="text-[11px] text-gray-500">{label}</p>
      <p className="mt-1 text-lg font-bold" style={{ color }}>
        {formatPrice(value)}
      </p>
    </div>
  );
}
