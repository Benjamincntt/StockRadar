import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { useLiveMarket, useSymbolSubscriptions } from "@/context/LiveMarketContext";
import { LivePrice } from "@/components/ui/LivePrice";
import { LiveChangePill } from "@/components/ui/LiveChangePill";
import { ChartTimeframeBar } from "@/components/ui/ChartTimeframeBar";
import { buildDailyChartFromHistory, resolveAccumulationZones } from "@/lib/chartAccumulation";
import {
  cn,
  formatPercent,
  formatPrice,
  formatShortDate,
  getBaseSessionDaysStyle,
} from "@/lib/utils";
import { BASE_PRICE_LABELS, flatBoxCardSubtitle } from "@/lib/basePriceLabels";
import type {
  ChartBar,
  ChartInterval,
  StockDetail,
  TinHieuV3,
} from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { PriceVolumeChart } from "@/components/ui/PriceVolumeChart";
import { AccumulationLegend } from "@/components/chart/AccumulationLegend";
import { useThemeTokens } from "@/context/ThemeContext";
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
  const [highlightZone, setHighlightZone] = useState<number | null>(null);
  /** undefined = đang tải · mảng = danh sách tín hiệu V3 (rỗng khi mã chưa có tín hiệu). */
  const [tinHieuV3, setTinHieuV3] = useState<TinHieuV3[] | undefined>(undefined);

    useEffect(() => {
    if (!symbol) return;
    setError(null);
    api
      .getStockDetail(symbol)
      .then(setDetail)
      .catch((e) => {
        const msg = e instanceof Error ? e.message : "";
        setError(
          msg.includes("404") || msg.toLowerCase().includes("not found") || msg.includes("không tìm thấy")
            ? "Không tìm thấy mã cổ phiếu."
            : `Không tải được chi tiết mã: ${msg || "lỗi server"}`,
        );
      });
  }, [symbol]);

  useEffect(() => {
    if (!symbol) return;
    setTinHieuV3(undefined);
    api
      .getV3TinHieuTheoSymbol(symbol)
      .then(setTinHieuV3)
      .catch(() => setTinHieuV3([])); // Lỗi tải → coi như chưa có tín hiệu V3
  }, [symbol]);

  useEffect(() => {
    if (!symbol) return;
    setChartLoading(true);

    const useDbDaily =
      chartInterval === "1D" &&
      detail?.history?.length &&
      detail?.flatBox?.periods?.length;

    if (useDbDaily) {
      setChartBars(buildDailyChartFromHistory(detail.history, detail.flatBox!.periods));
      setChartLoading(false);
      return;
    }

    api
      .getStockChart(symbol, chartInterval)
      .then((chart) => setChartBars(chart.bars))
      .catch(() => setChartBars([]))
      .finally(() => setChartLoading(false));
  }, [symbol, chartInterval, detail?.history, detail?.flatBox?.periods]);

  const resolvedZones = useMemo(() => {
    if (chartInterval !== "1D" || !detail?.flatBox?.periods?.length) return [];
    return resolveAccumulationZones(chartBars, detail.flatBox.periods);
  }, [chartBars, chartInterval, detail?.flatBox?.periods]);

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

  const boxSessionStyle = detail.flatBox
    ? getBaseSessionDaysStyle(detail.flatBox.sessionDays)
    : null;

  const dsTinHieu = tinHieuV3 ?? [];

  return (
    <div className="space-y-4 pb-24 lg:pb-4">
      <div className="flex items-center gap-3">
        <Link
          to="/"
          className="flex h-9 w-9 items-center justify-center rounded-full bg-surface-high text-on-surface"
          aria-label="Quay lại"
        >
          <ChevronLeft className="h-5 w-5" />
        </Link>
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-lg font-bold text-on-surface lg:text-2xl">{detail.symbol}</h2>
          <p className="truncate text-xs text-on-surface-variant lg:text-sm">{detail.name}</p>
        </div>
        <Link
          to={`/stocks/${detail.symbol}/su-kien-quyen`}
          className="rounded-full bg-surface-high px-3 py-2 text-xs font-semibold text-primary"
        >
          Sự kiện quyền
        </Link>
      </div>

      {dsTinHieu.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {dsTinHieu.map((th) => (
            <TinHieuV3Badge key={th.id} th={th} />
          ))}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.6fr)_minmax(300px,1fr)] xl:grid-cols-[minmax(0,1.75fr)_420px] lg:items-start">
        <div className="space-y-4">
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
            <p className="label-caps text-on-surface-variant">Tỷ lệ khối lượng</p>
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

      <Card padding="sm">
        <div className="mb-3 flex flex-col gap-2">
          <SectionTitle
            title="Biểu đồ giá & khối lượng"
            subtitle={
              detail.flatBox?.periods.length
                ? "Khung Ngày — vùng tím = nền giá"
                : "KBS · Phong cách TradingView"
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
          accumulationPeriods={detail.flatBox?.periods}
          baseZone={
            detail.flatBox
              ? { low: detail.flatBox.boxLow, high: detail.flatBox.boxHigh }
              : undefined
          }
          highlightZoneIndex={highlightZone}
          resolvedZones={resolvedZones}
        />
        {detail.flatBox && detail.flatBox.periods.length > 0 && (
          <AccumulationLegend
            periods={detail.flatBox.periods}
            visibleFlags={zoneVisibleFlags}
            activeIndex={highlightZone}
            onSelect={(i) => {
              if (zoneVisibleFlags[i] === false) return;
              setHighlightZone((prev) => (prev === i ? null : i));
            }}
          />
        )}
        {detail.flatBox && chartInterval !== "1D" && detail.flatBox.periods.length > 0 && (
          <p className="mt-2 text-center text-[11px] text-on-surface-variant">
            Chuyển khung <span className="font-semibold text-primary">D</span> để xem vùng tích lũy trên biểu đồ
          </p>
        )}
      </Card>

      {detail.flatBox && boxSessionStyle && (
        <Card>
          <SectionTitle
            title={BASE_PRICE_LABELS.base}
            subtitle={flatBoxCardSubtitle(detail.flatBox, live?.price ?? detail.price)}
          />
          <div className="space-y-3">
            <div className="grid grid-cols-3 gap-2 text-center">
              <div className="rounded-xl bg-surface-low py-2.5 px-2">
                <p className="label-caps text-on-surface-variant">Vùng nền</p>
                <p className="font-data mt-0.5 text-sm font-bold text-on-surface">
                  {formatPrice(detail.flatBox.boxLow)} – {formatPrice(detail.flatBox.boxHigh)}
                </p>
              </div>
              <div
                className="rounded-xl border py-2.5 px-2"
                style={{
                  backgroundColor: boxSessionStyle.backgroundColor,
                  borderColor: boxSessionStyle.borderColor,
                }}
              >
                <p className="label-caps text-on-surface-variant">Số phiên</p>
                <p
                  className="font-data mt-0.5 text-lg font-bold tabular-nums"
                  style={{ color: boxSessionStyle.color }}
                >
                  {detail.flatBox.sessionDays}
                  <span className="ml-0.5 text-sm font-semibold">phiên</span>
                </p>
              </div>
              <div className="rounded-xl border border-outline-variant py-2.5 px-2">
                <p className="label-caps text-on-surface-variant">
                  {detail.flatBox.isBreakoutConfirmed ? "KL / nền" : "Cắt lỗ"}
                </p>
                <p className="font-data mt-0.5 text-lg font-bold tabular-nums text-on-surface">
                  {detail.flatBox.isBreakoutConfirmed && detail.flatBox.volumeMultiplier != null
                    ? `×${detail.flatBox.volumeMultiplier.toFixed(1)}`
                    : formatPrice(detail.flatBox.suggestedStopLoss)}
                </p>
              </div>
            </div>
            {detail.flatBox.isBreakoutConfirmed && detail.flatBox.priceGainPercent != null && (
              <div className="rounded-xl border border-primary/30 bg-primary/5 px-3 py-2 text-center text-xs text-on-surface">
                Phiên kích hoạt{" "}
                <span className="font-data font-bold text-primary">
                  +{detail.flatBox.priceGainPercent.toFixed(1)}%
                </span>
              </div>
            )}
            <div className="flex items-center justify-between rounded-xl border border-outline-variant px-3 py-2">
              <span className="text-xs text-on-surface-variant">
                Lọc FOMO: so với đỉnh nền {formatPrice(detail.flatBox.filterBoxTop)}
              </span>
              <span
                className="font-data text-sm font-bold"
                style={{
                  color: detail.flatBox.exceedsRunupFilter
                    ? theme.red
                    : detail.flatBox.filterGainFromBoxTopPercent > 0
                      ? theme.primary
                      : theme.text,
                }}
              >
                {formatPercent(detail.flatBox.filterGainFromBoxTopPercent)}
              </span>
            </div>
          </div>
        </Card>
      )}

        </div>

        <div className="space-y-4 lg:sticky lg:top-20 lg:self-start">
      <button
        type="button"
        onClick={addWatchlist}
        disabled={added}
        className="hidden w-full rounded-xl bg-primary py-3.5 text-sm font-bold text-on-primary shadow-lg disabled:opacity-60 lg:block"
      >
        {added ? "Đã thêm Watchlist" : "+ Thêm vào Watchlist"}
      </button>
        </div>

        <div className="space-y-4 lg:col-span-2">

      <Card>
        <SectionTitle
          title="Tín hiệu V3 (P1++)"
          subtitle="Kịch bản đã kích hoạt · bán realtime trên giá khớp · snapshot chỉ báo quyết định"
        />
        {tinHieuV3 === undefined ? (
          <div className="space-y-2">
            {Array.from({ length: 2 }).map((_, i) => (
              <div key={i} className="h-16 animate-pulse rounded-xl bg-surface-low" />
            ))}
          </div>
        ) : dsTinHieu.length === 0 ? (
          <p className="py-4 text-center text-sm text-on-surface-variant">
            Chưa có tín hiệu V3 cho mã này. Dữ liệu cập nhật khi bộ kịch bản P1++ kích hoạt.
          </p>
        ) : (
          <div className="space-y-2">
            {dsTinHieu.map((th) => (
              <TinHieuV3Card key={th.id} th={th} />
            ))}
          </div>
        )}
      </Card>

        </div>
      </div>

      <div className="ios-safe-bottom fixed bottom-16 left-0 right-0 z-20 px-4 lg:hidden">
        <div className="page-container">
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

type ThemeTokens = ReturnType<typeof useThemeTokens>;

/** Nhãn tiếng Việt cho từng kịch bản V3 (Phụ lục A). */
const NHAN_KICH_BAN_V3: Record<string, string> = {
  KB1: "KB1 · Hoảng loạn",
  KB2: "KB2 · Chỉ số thủng MA50",
  KB4: "KB4 · Ngành bán quá mức",
  N1: "N1 · Giải chấp cạn kiệt",
  N3: "N3 · Đón dòng tiền đầu tháng",
};

/** Nhãn trạng thái tín hiệu V3 (enum TrangThaiTinHieuV3). */
const TRANG_THAI_TIN_HIEU_LABEL: Record<string, string> = {
  DaBao: "Đã báo",
  BiCatDanhMuc: "Bị cắt danh mục",
  DaVaoLenh: "Đã vào lệnh",
  BiHuy: "Bị hủy",
};

/** Nhãn + định nghĩa bán realtime theo từng kịch bản (Phụ lục A). */
const LUAT_BAN_V3: Record<string, { MA: string; giuToiDa: number }> = {
  KB1: { MA: "MA10", giuToiDa: 15 },
  KB2: { MA: "MA10", giuToiDa: 15 },
  KB4: { MA: "MA10", giuToiDa: 10 },
  N1: { MA: "MA20", giuToiDa: 15 },
  N3: { MA: "ATO TDM=2 tháng sau", giuToiDa: 12 },
};

function nhanKichBanV3(loai: string) {
  return NHAN_KICH_BAN_V3[loai] ?? loai;
}

/** Màu theo trạng thái tín hiệu: xanh = đã vào lệnh, xám = mới báo/đã cắt, đỏ = hủy. */
function trangThaiTinHieuStyle(trangThai: string, theme: ThemeTokens) {
  switch (trangThai) {
    case "DaVaoLenh":
      return { bg: theme.greenBg, color: theme.primary };
    case "DaBao":
      return { bg: theme.amberBg, color: theme.amber };
    case "BiHuy":
      return { bg: theme.redBg, color: theme.red };
    default:
      return { bg: theme.neutralBg, color: theme.textMuted };
  }
}

/** Badge tín hiệu V3 — "KB1 · Hoảng loạn — Đã vào lệnh". */
function TinHieuV3Badge({ th }: { th: TinHieuV3 }) {
  const theme = useThemeTokens();
  const style = trangThaiTinHieuStyle(th.trangThai, theme);
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[11px] font-semibold"
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      <span>{nhanKichBanV3(th.loai)}</span>
      <span className="opacity-60">—</span>
      <span>{TRANG_THAI_TIN_HIEU_LABEL[th.trangThai] ?? th.trangThai}</span>
    </span>
  );
}

/** Nhãn đọc được cho các khóa trong snapshotJson (FR-024). */
const SNAPSHOT_LABELS: Record<string, string> = {
  tv20: "TV20 (tỷ)",
  close: "Giá đóng cửa",
  rsi14: "RSI14",
  ret20: "RET20",
  secr20: "SECR20",
  rs63: "RS63",
  rs126: "RS126",
  floor20: "FLOOR20",
  laSan: "Phiên sàn",
  breadth50: "BREADTH50",
  chiSoMa50Lech: "Chỉ số lệch MA50",
  left: "LEFT",
  tdm: "TDM",
  soMaUni: "UNI",
  sector: "Ngành",
};

const SNAPSHOT_THU_TU = [
  "breadth50",
  "chiSoMa50Lech",
  "tv20",
  "rsi14",
  "ret20",
  "secr20",
  "rs63",
  "rs126",
  "floor20",
  "left",
  "tdm",
  "soMaUni",
  "laSan",
  "close",
];

function formatSnapshotValue(value: unknown): string {
  if (typeof value === "boolean") return value ? "Có" : "Không";
  if (typeof value === "number")
    return value.toLocaleString("vi-VN", { maximumFractionDigits: 2 });
  if (value == null) return "—";
  return String(value);
}

/** Thẻ một tín hiệu V3: kịch bản, kế hoạch mua/bán, snapshot chỉ báo, kết quả. */
function TinHieuV3Card({ th }: { th: TinHieuV3 }) {
  const theme = useThemeTokens();
  const style = trangThaiTinHieuStyle(th.trangThai, theme);
  const luat = LUAT_BAN_V3[th.loai];

  let snapshot: Record<string, unknown> = {};
  if (th.snapshotJson) {
    try {
      snapshot = JSON.parse(th.snapshotJson) as Record<string, unknown>;
    } catch {
      snapshot = {};
    }
  }
  const keys = Object.keys(snapshot).sort(
    (a, b) => {
      const ia = SNAPSHOT_THU_TU.indexOf(a);
      const ib = SNAPSHOT_THU_TU.indexOf(b);
      return (ia === -1 ? 99 : ia) - (ib === -1 ? 99 : ib);
    },
  );

  const mua = th.muaMoCuaPhienSau ? "ATO mở cửa phiên sau" : "ATC cuối phiên tín hiệu";
  const ban = luat
    ? th.loai === "N3"
      ? `Bán lệnh ATO phiên TDM=2 tháng sau · Giữ tối đa ${luat.giuToiDa} phiên`
      : `Bán realtime: giá khớp ≥ max(chạm ${luat.MA}, giá vào +0,4%) · Giữ tối đa ${luat.giuToiDa} phiên`
    : null;

  return (
    <div className="rounded-xl border border-outline-variant bg-surface-low px-3 py-3">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span className="text-sm font-semibold text-on-surface">{nhanKichBanV3(th.loai)}</span>
        <span
          className="rounded-full px-2 py-0.5 text-[10px] font-bold"
          style={{ backgroundColor: style.bg, color: style.color }}
        >
          {TRANG_THAI_TIN_HIEU_LABEL[th.trangThai] ?? th.trangThai}
        </span>
        <span className="ml-auto text-[11px] text-on-surface-variant">
          Ngày tín hiệu{" "}
          <span className="font-semibold text-on-surface">{formatShortDate(th.ngayTinHieu)}</span>
        </span>
      </div>

      <div className="mt-2 grid grid-cols-3 gap-2 text-center">
        <MetricCell label="Giá tín hiệu" value={formatPrice(th.giaTinHieu)} />
        <MetricCell label="Hạng" value={`#${th.hang}`} />
        <MetricCell
          label="Thước xếp hạng"
          value={formatSnapshotValue(th.giaTriXepHang)}
        />
      </div>

      {mua && (
        <p className="mt-2 text-xs text-on-surface-variant">
          <span className="font-semibold text-on-surface">Mua:</span> {mua}
          {ban ? ` · ${ban}` : ""}
        </p>
      )}

      {th.giaVao != null && (
        <p className="mt-1 text-xs text-on-surface-variant">
          Giá vào: <span className="font-data font-semibold text-on-surface">{formatPrice(th.giaVao)}</span>
          {th.giaThoat != null && (
            <>
              {" "}· Giá thoát:{" "}
              <span className="font-data font-semibold text-on-surface">{formatPrice(th.giaThoat)}</span>
            </>
          )}
        </p>
      )}

      {keys.length > 0 && (
        <div className="mt-2.5 flex flex-wrap gap-1.5">
          {keys
            .filter((k) => k !== "symbol")
            .map((k) => (
              <span
                key={k}
                className={cn(
                  "inline-flex items-center gap-1 rounded-lg border border-outline-variant/60 bg-surface px-2 py-1 text-[10px]",
                )}
              >
                <span className="text-on-surface-variant">{SNAPSHOT_LABELS[k] ?? k}</span>
                <span className="font-data font-semibold tabular-nums text-on-surface">
                  {formatSnapshotValue(snapshot[k])}
                </span>
              </span>
            ))}
        </div>
      )}
    </div>
  );
}

function MetricCell({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-surface px-2 py-2">
      <p className="label-caps text-on-surface-variant">{label}</p>
      <p className="font-data mt-0.5 text-sm font-bold tabular-nums text-on-surface">{value}</p>
    </div>
  );
}
