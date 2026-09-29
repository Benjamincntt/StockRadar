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
  formatDateTime,
  formatPercent,
  formatPrice,
  formatShortDate,
  getBaseSessionDaysStyle,
} from "@/lib/utils";
import { BASE_PRICE_LABELS, flatBoxCardSubtitle } from "@/lib/basePriceLabels";
import type {
  BangChungKichBan,
  ChartBar,
  ChartInterval,
  ChiTietKichBan,
  KeHoachGiaoDichDetail,
  KichBanTheoSymbol,
  StockDetail,
} from "@/types";
import { Card, SectionTitle } from "@/components/ui/Card";
import { PriceVolumeChart } from "@/components/ui/PriceVolumeChart";
import { AccumulationLegend } from "@/components/chart/AccumulationLegend";
import { useThemeTokens } from "@/context/ThemeContext";
import { Check, ChevronDown, ChevronLeft, X } from "lucide-react";

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
  /** undefined = đang tải · null = 404 (chưa có dữ liệu kịch bản) · object = có dữ liệu. */
  const [kichBan, setKichBan] = useState<KichBanTheoSymbol | null | undefined>(undefined);
  /** Trạng thái mở rộng từng kịch bản trong mục Bằng chứng (key = loaiKichBan). */
  const [moRongKichBan, setMoRongKichBan] = useState<Record<string, boolean>>({});

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
    setKichBan(undefined);
    setMoRongKichBan({});
    api
      .getKichBanTheoSymbol(symbol)
      .then(setKichBan)
      .catch(() => setKichBan(null)); // Lỗi tải → coi như chưa có dữ liệu kịch bản
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

  const danhSachKichBan = kichBan?.danhSachKichBan ?? [];
  // Thẻ Kế hoạch giao dịch chỉ hiển thị khi kịch bản đã kích hoạt và có kế hoạch.
  const kichBanDaKichHoat =
    danhSachKichBan.find((kb) => kb.trangThai === "DaKichHoat" && kb.keHoachGiaoDich) ?? null;
  // Mặc định mở rộng kịch bản đã kích hoạt (nếu không có thì kịch bản đầu tiên).
  const kichBanMoRongMacDinh =
    danhSachKichBan.find((kb) => kb.trangThai === "DaKichHoat")?.loaiKichBan ??
    danhSachKichBan[0]?.loaiKichBan ??
    null;
  const laMoRongKichBan = (kb: ChiTietKichBan) =>
    moRongKichBan[kb.loaiKichBan] ?? kb.loaiKichBan === kichBanMoRongMacDinh;
  const toggleKichBan = (kb: ChiTietKichBan) =>
    setMoRongKichBan((prev) => ({ ...prev, [kb.loaiKichBan]: !laMoRongKichBan(kb) }));

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

      {danhSachKichBan.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {danhSachKichBan.map((kb) => (
            <KichBanBadge key={kb.loaiKichBan} kb={kb} />
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
      {kichBanDaKichHoat && kichBanDaKichHoat.keHoachGiaoDich && (
        <KeHoachGiaoDichCard kb={kichBanDaKichHoat} plan={kichBanDaKichHoat.keHoachGiaoDich} />
      )}

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
          title="Bằng chứng kịch bản"
          subtitle="Điều kiện kịch bản V2 theo 3 giai đoạn: bối cảnh · hình thái · kích hoạt"
        />
        {kichBan === undefined ? (
          <div className="space-y-2">
            {Array.from({ length: 2 }).map((_, i) => (
              <div key={i} className="h-16 animate-pulse rounded-xl bg-surface-low" />
            ))}
          </div>
        ) : danhSachKichBan.length === 0 ? (
          <p className="py-4 text-center text-sm text-on-surface-variant">
            Chưa có đánh giá kịch bản. Dữ liệu sẽ cập nhật sau phiên giao dịch tiếp theo.
          </p>
        ) : (
          <div className="space-y-2">
            {danhSachKichBan.map((kb) => (
              <KichBanAccordion
                key={kb.loaiKichBan}
                kb={kb}
                moRong={laMoRongKichBan(kb)}
                onToggle={() => toggleKichBan(kb)}
              />
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

/** Nhãn tiếng Việt cho trạng thái vòng đời kịch bản V2. */
const TRANG_THAI_KICH_BAN_LABEL: Record<string, string> = {
  DangTheoDoi: "Đang theo dõi",
  DangHinhThanh: "Đang hình thành",
  DaKichHoat: "Đã kích hoạt",
  DangGiu: "Đang giữ",
  ChotLoi: "Chốt lời",
  HuyLenh: "Hủy lệnh",
  ThoatLenh: "Thoát lệnh",
};

function trangThaiKichBanLabel(trangThai: string) {
  return TRANG_THAI_KICH_BAN_LABEL[trangThai] ?? trangThai;
}

/** Màu theo trạng thái: xanh = đã kích hoạt/đang giữ, cam = đang theo dõi, xám = hình thành. */
function trangThaiKichBanStyle(trangThai: string, theme: ThemeTokens) {
  switch (trangThai) {
    case "DaKichHoat":
    case "DangGiu":
    case "ChotLoi":
      return { bg: theme.greenBg, color: theme.primary };
    case "DangTheoDoi":
      return { bg: theme.amberBg, color: theme.amber };
    case "HuyLenh":
      return { bg: theme.redBg, color: theme.red };
    default:
      return { bg: theme.neutralBg, color: theme.textMuted };
  }
}

/** Badge trạng thái kịch bản — "Nổ hướng lên — Đang theo dõi 65%". */
function KichBanBadge({ kb }: { kb: ChiTietKichBan }) {
  const theme = useThemeTokens();
  const style = trangThaiKichBanStyle(kb.trangThai, theme);
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[11px] font-semibold"
      style={{ backgroundColor: style.bg, color: style.color }}
    >
      <span>{kb.tenKichBan}</span>
      <span className="opacity-60">—</span>
      <span>{trangThaiKichBanLabel(kb.trangThai)}</span>
      <span className="font-data tabular-nums">{Math.round(kb.mucHoanThien)}%</span>
    </span>
  );
}

/** Thẻ Kế hoạch giao dịch V2 — chỉ hiển thị khi kịch bản đã kích hoạt. */
function KeHoachGiaoDichCard({ kb, plan }: { kb: ChiTietKichBan; plan: KeHoachGiaoDichDetail }) {
  const theme = useThemeTokens();
  const giaVao =
    plan.giaVaoLenhMin === plan.giaVaoLenhMax
      ? formatPrice(plan.giaVaoLenhMin)
      : `${formatPrice(plan.giaVaoLenhMin)} – ${formatPrice(plan.giaVaoLenhMax)}`;

  return (
    <div
      className="overflow-hidden rounded-2xl border"
      style={{ borderColor: theme.primary, backgroundColor: theme.greenBg }}
    >
      <div className="px-4 pt-4 pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="label-caps text-on-surface-variant">Kế hoạch giao dịch</p>
            <h3 className="mt-1 text-base font-bold leading-snug text-on-surface">{kb.tenKichBan}</h3>
          </div>
          <span
            className="shrink-0 rounded-full px-2.5 py-1 text-[11px] font-bold"
            style={{ backgroundColor: theme.primary, color: theme.onPrimary }}
          >
            Đã kích hoạt
          </span>
        </div>
        {kb.thoiGianKichHoat && (
          <p className="mt-2 text-[11px] text-on-surface-variant">
            Kích hoạt lúc{" "}
            <span className="font-semibold text-on-surface">{formatDateTime(kb.thoiGianKichHoat)}</span>
          </p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-px border-t border-outline-variant bg-outline-variant">
        <PlanCell label="Giá vào lệnh" value={giaVao} color={theme.primary} />
        <PlanCell
          label="Giá dừng lỗ"
          value={plan.giaDungLo > 0 ? formatPrice(plan.giaDungLo) : "—"}
          color={theme.red}
        />
        <PlanCell
          label="Chốt lời 1"
          value={plan.giaChotLoi1 > 0 ? formatPrice(plan.giaChotLoi1) : "—"}
          color={theme.primary}
        />
        <PlanCell
          label="Chốt lời 2"
          value={plan.giaChotLoi2 > 0 ? formatPrice(plan.giaChotLoi2) : "—"}
          color={theme.primary}
        />
      </div>

      {plan.tyLeLaiLo > 0 && (
        <div className="border-t border-outline-variant px-4 py-2 text-center">
          <span className="text-xs text-on-surface-variant">R:R </span>
          <span className="font-data text-sm font-bold text-on-surface">
            1 : {plan.tyLeLaiLo.toFixed(1)}
          </span>
        </div>
      )}

      {plan.dieuKienHuy && (
        <div className="border-t border-outline-variant bg-surface px-4 py-2.5">
          <p className="label-caps text-on-surface-variant">Điều kiện hủy</p>
          <p className="mt-0.5 text-xs text-on-surface">{plan.dieuKienHuy}</p>
        </div>
      )}
    </div>
  );
}

function PlanCell({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="bg-surface px-3 py-2.5 text-center">
      <p className="label-caps text-on-surface-variant">{label}</p>
      <p className="font-data mt-0.5 text-sm font-bold tabular-nums" style={{ color }}>
        {value}
      </p>
    </div>
  );
}

/** Thứ tự hiển thị nhóm bằng chứng theo vai trò chỉ báo. */
const THU_TU_VAI_TRO = ["BoiCanh", "HinhThai", "CoKichHoat", "RuiRo"];

const NHAN_VAI_TRO: Record<string, string> = {
  BoiCanh: "Bối cảnh",
  HinhThai: "Hình thái",
  CoKichHoat: "Kích hoạt",
  RuiRo: "Rủi ro / Thoát",
};

/** Gom bằng chứng theo vai trò, sắp xếp theo đúng thứ tự 3 giai đoạn + rủi ro. */
function nhomBangChungTheoVaiTro(danhSach: BangChungKichBan[]) {
  const theoVaiTro = new Map<string, BangChungKichBan[]>();
  for (const ev of danhSach) {
    const nhom = theoVaiTro.get(ev.vaiTro);
    if (nhom) nhom.push(ev);
    else theoVaiTro.set(ev.vaiTro, [ev]);
  }

  return [...theoVaiTro.entries()]
    .sort(([a], [b]) => xepHangVaiTro(a) - xepHangVaiTro(b))
    .map(([vaiTro, items]) => ({
      vaiTro,
      label: NHAN_VAI_TRO[vaiTro] ?? vaiTro,
      items,
    }));
}

function xepHangVaiTro(vaiTro: string) {
  const index = THU_TU_VAI_TRO.indexOf(vaiTro);
  return index === -1 ? THU_TU_VAI_TRO.length : index;
}

/** Mục kịch bản mở rộng/thu gọn trong "Bằng chứng kịch bản". */
function KichBanAccordion({
  kb,
  moRong,
  onToggle,
}: {
  kb: ChiTietKichBan;
  moRong: boolean;
  onToggle: () => void;
}) {
  const theme = useThemeTokens();
  const style = trangThaiKichBanStyle(kb.trangThai, theme);
  const nhomBangChung = nhomBangChungTheoVaiTro(kb.bangChung);
  const phanTram = Math.max(0, Math.min(100, kb.mucHoanThien));

  return (
    <div className="rounded-xl border border-outline-variant bg-surface-low">
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center gap-3 px-3 py-3 text-left"
      >
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="text-sm font-semibold text-on-surface">{kb.tenKichBan}</span>
            <span
              className="rounded-full px-2 py-0.5 text-[10px] font-bold"
              style={{ backgroundColor: style.bg, color: style.color }}
            >
              {trangThaiKichBanLabel(kb.trangThai)}
            </span>
            <span className="font-data text-xs font-bold tabular-nums" style={{ color: style.color }}>
              {Math.round(kb.mucHoanThien)}%
            </span>
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
            <GiaiDoanDat label="Bối cảnh" dat={kb.datBoiCanh} />
            <GiaiDoanDat label="Hình thái" dat={kb.datHinhThai} />
            <GiaiDoanDat label="Kích hoạt" dat={kb.datCoKichHoat} />
          </div>
          <div
            className="mt-2 h-1 w-full overflow-hidden rounded-full"
            style={{ backgroundColor: theme.neutralBg }}
          >
            <div
              className="h-full rounded-full"
              style={{ width: `${phanTram}%`, backgroundColor: style.color }}
            />
          </div>
        </div>
        <ChevronDown
          className={cn(
            "h-4 w-4 shrink-0 text-on-surface-variant transition-transform",
            moRong && "rotate-180",
          )}
        />
      </button>

      {moRong && (
        <div className="border-t border-outline-variant/60 px-3 pb-3 pt-3">
          <p className="text-[11px] text-on-surface-variant">
            Đánh giá ngày{" "}
            <span className="font-semibold text-on-surface">{formatShortDate(kb.ngayDanhGia)}</span>
          </p>
          {nhomBangChung.length === 0 ? (
            <p className="mt-2 text-xs text-on-surface-variant">
              Chưa có bằng chứng cho kịch bản này.
            </p>
          ) : (
            <div className="mt-2.5 space-y-3">
              {nhomBangChung.map((nhom) => (
                <div key={nhom.vaiTro}>
                  <p className="label-caps text-on-surface-variant">{nhom.label}</p>
                  <ul className="mt-1.5 space-y-1.5">
                    {nhom.items.map((ev, index) => (
                      <BangChungRow key={`${nhom.vaiTro}-${index}`} ev={ev} />
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function GiaiDoanDat({ label, dat }: { label: string; dat: boolean }) {
  const theme = useThemeTokens();
  return (
    <span className="inline-flex items-center gap-1 text-[11px]">
      {dat ? (
        <Check className="h-3 w-3" style={{ color: theme.primary }} />
      ) : (
        <X className="h-3 w-3" style={{ color: theme.textSubtle }} />
      )}
      <span className={dat ? "font-medium text-on-surface" : "text-on-surface-variant"}>{label}</span>
    </span>
  );
}

function BangChungRow({ ev }: { ev: BangChungKichBan }) {
  const theme = useThemeTokens();
  return (
    <li className="flex items-start gap-2 rounded-lg border border-outline-variant/60 bg-surface px-2.5 py-2">
      {ev.dat ? (
        <Check className="mt-0.5 h-3.5 w-3.5 shrink-0" style={{ color: theme.primary }} />
      ) : (
        <X className="mt-0.5 h-3.5 w-3.5 shrink-0" style={{ color: theme.red }} />
      )}
      <div className="min-w-0 flex-1">
        <p className="text-xs font-medium text-on-surface">{ev.moTa}</p>
        <p className="mt-0.5 text-[11px] text-on-surface-variant">
          {ev.giaTriThucTe && (
            <>
              Thực tế{" "}
              <span
                className="font-data font-semibold tabular-nums"
                style={{ color: ev.dat ? theme.primary : theme.red }}
              >
                {ev.giaTriThucTe}
              </span>
            </>
          )}
          {ev.giaTriThucTe && ev.nguong && <span> · </span>}
          {ev.nguong && (
            <>
              Ngưỡng <span className="font-data font-semibold tabular-nums">{ev.nguong}</span>
            </>
          )}
        </p>
      </div>
    </li>
  );
}
