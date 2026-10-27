import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { api } from "@/lib/api";
import { Card, SectionTitle } from "@/components/ui/Card";
import { useThemeTokens } from "@/context/ThemeContext";
import { cn, formatPercent, formatPrice, formatShortDate } from "@/lib/utils";
import type { HieuQuaPeriod, HieuQuaTomTatV3, ViTheV3, ViTheV3Paged } from "@/types";

const PERIODS: { value: HieuQuaPeriod; label: string }[] = [
  { value: "week", label: "Tuần" },
  { value: "month", label: "Tháng" },
  { value: "quarter", label: "Quý" },
  { value: "all", label: "Tất cả" },
];

/** Bộ lọc trạng thái vị thế cho lịch sử lệnh V3 (tham số status của /v3/hieu-qua/lich-su). */
const STATUS_FILTERS: { value: string; label: string }[] = [
  { value: "", label: "Tất cả" },
  { value: "mo", label: "Đang mở" },
  { value: "dong", label: "Đã đóng" },
];

const PAGE_SIZE = 20;

function toISODate(d: Date) {
  return d.toISOString().slice(0, 10);
}

/** Quy đổi bộ lọc kỳ sang mốc từ–đén (YYYY-MM-DD) cho endpoint tom-tat V3. */
function rangeFromPeriod(period: HieuQuaPeriod): { tu?: string; den?: string } {
  const now = new Date();
  if (period === "all") return {};
  const tu = new Date(now);
  if (period === "week") tu.setDate(tu.getDate() - 7);
  else if (period === "month") tu.setMonth(tu.getMonth() - 1);
  else if (period === "quarter") tu.setMonth(tu.getMonth() - 3);
  return { tu: toISODate(tu), den: toISODate(now) };
}

/** nghìn đồng → tỷ đồng (2 chữ số), dùng cho khối danh mục. */
function formatTy(nghinDong: number) {
  return `${(nghinDong / 1_000_000).toFixed(2)} tỷ`;
}

/** Định nghĩa thắng: lãi ròng > 0,05%; −0,05%..+0,05% là hoà vốn (khớp Phụ lục H). */
function useKetQuaStyle() {
  const theme = useThemeTokens();
  return useCallback(
    (ketQua: string | null): { label: string; color: string; bg: string } => {
      switch (ketQua) {
        case "Thang":
          return { label: "Thắng", color: theme.primary, bg: theme.greenBg };
        case "Thua":
          return { label: "Thua", color: theme.red, bg: theme.redBg };
        case "HoaVon":
          return { label: "Hoà vốn", color: theme.textMuted, bg: theme.neutralBg };
        default:
          return { label: "Chờ đo", color: theme.amber, bg: theme.amberBg };
      }
    },
    [theme],
  );
}

const TRANG_THAI_VI_THE_LABEL: Record<string, string> = {
  DanhDau: "Đánh dấu",
  ChoMua: "Chờ mua",
  DangGiu: "Đang giữ",
  DaThoat: "Đã thoát",
  BiHuy: "Bị hủy",
};

export function HieuQuaPage() {
  const theme = useThemeTokens();
  const ketQuaStyle = useKetQuaStyle();

  const [period, setPeriod] = useState<HieuQuaPeriod>("month");
  const [summary, setSummary] = useState<HieuQuaTomTatV3 | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [summaryError, setSummaryError] = useState<string | null>(null);

  const [history, setHistory] = useState<ViTheV3Paged | null>(null);
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [historyError, setHistoryError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setSummaryLoading(true);
    setSummaryError(null);
    const { tu, den } = rangeFromPeriod(period);
    api
      .getV3HieuQuaTomTat(tu, den)
      .then((data) => {
        if (active) setSummary(data);
      })
      .catch((e: unknown) => {
        if (active)
          setSummaryError(
            e instanceof Error ? e.message : "Không tải được tóm tắt hiệu quả.",
          );
      })
      .finally(() => {
        if (active) setSummaryLoading(false);
      });
    return () => {
      active = false;
    };
  }, [period]);

  useEffect(() => {
    let active = true;
    setHistoryLoading(true);
    setHistoryError(null);
    api
      .getV3HieuQuaLichSu(page, PAGE_SIZE, status || undefined)
      .then((data) => {
        if (active) setHistory(data);
      })
      .catch((e: unknown) => {
        if (active)
          setHistoryError(
            e instanceof Error ? e.message : "Không tải được lịch sử lệnh.",
          );
      })
      .finally(() => {
        if (active) setHistoryLoading(false);
      });
    return () => {
      active = false;
    };
  }, [page, status]);

  const totalPages = history
    ? Math.max(1, Math.ceil(history.total / (history.size || PAGE_SIZE)))
    : 1;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-bold text-on-surface">Hiệu quả kịch bản V3</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Bộ kịch bản P1++ (bán realtime) · thắng = lãi ròng &gt; 0,05% trên giá khớp trong phiên
        </p>
      </div>

      {/* Bộ lọc kỳ */}
      <div className="flex flex-wrap gap-2">
        {PERIODS.map((p) => {
          const active = p.value === period;
          return (
            <button
              key={p.value}
              type="button"
              onClick={() => setPeriod(p.value)}
              className={cn(
                "rounded-xl border px-4 py-1.5 text-sm font-medium transition-colors",
                active
                  ? "border-primary/40 bg-primary/10 text-primary"
                  : "border-outline-variant bg-surface-low text-on-surface-variant hover:bg-surface-high",
              )}
            >
              {p.label}
            </button>
          );
        })}
      </div>

      {summaryError && (
        <div
          className="rounded-2xl border border-outline-variant p-4 text-sm text-negative"
          style={{ backgroundColor: theme.redSoft }}
        >
          {summaryError}
        </div>
      )}

      {summaryLoading ? (
        <div className="space-y-3">
          <div className="h-24 animate-pulse rounded-2xl bg-surface-low" />
          <div className="h-40 animate-pulse rounded-2xl bg-surface-low" />
        </div>
      ) : summary ? (
        <>
          {/* Khối 2 — danh mục */}
          {summary.danhMuc && (
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <MetricCard
                label="Giá trị danh mục"
                value={formatTy(summary.danhMuc.giaTriHienTai)}
                color={theme.text}
              />
              <MetricCard
                label="Tiền mặt"
                value={formatTy(summary.danhMuc.tienMat)}
                color={theme.primary}
              />
              <MetricCard
                label="Vị thế đang giữ"
                value={String(summary.danhMuc.soViTheDangGiu)}
                color={theme.text}
              />
              <MetricCard
                label="Sụt giảm tối đa"
                value={`${summary.danhMuc.sutGiamToiDaPhanTram.toFixed(1)}%`}
                color={summary.danhMuc.sutGiamToiDaPhanTram > 0 ? theme.red : theme.textMuted}
              />
            </div>
          )}

          {/* Khối 1 — theo kịch bản */}
          <Card>
            <SectionTitle
              title="Theo kịch bản"
              subtitle={
                summary.denNgay
                  ? `Đo đến ngày ${formatShortDate(summary.denNgay)}`
                  : undefined
              }
            />
            <div className="-mx-1 overflow-x-auto">
              <table className="w-full min-w-[640px] border-collapse text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-on-surface-variant">
                    <th className="px-2 py-2 font-medium">Kịch bản</th>
                    <th className="px-2 py-2 text-right font-medium">Đã đóng</th>
                    <th className="px-2 py-2 text-right font-medium">Thắng</th>
                    <th className="px-2 py-2 text-right font-medium">Tỷ lệ</th>
                    <th className="px-2 py-2 text-right font-medium">Lãi TB</th>
                    <th className="px-2 py-2 text-right font-medium">Lỗ TB</th>
                    <th className="px-2 py-2 text-right font-medium">Tổng lãi/lỗ</th>
                    <th className="px-2 py-2 text-right font-medium">Tệ nhất</th>
                    <th className="px-2 py-2 text-right font-medium">Giữ TB</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.theoKichBan.map((row) => (
                    <tr key={row.loai} className="border-t border-outline-variant/60">
                      <td className="px-2 py-2.5 font-medium text-on-surface">{row.ten}</td>
                      <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface-variant">
                        {row.soLenhDong}
                      </td>
                      <td
                        className="px-2 py-2.5 text-right font-data tabular-nums"
                        style={{ color: theme.primary }}
                      >
                        {row.soThang}
                      </td>
                      <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface">
                        {row.soLenhDong > 0 ? `${row.tyLeThangPhanTram.toFixed(0)}%` : "—"}
                      </td>
                      <td
                        className="px-2 py-2.5 text-right font-data tabular-nums"
                        style={{ color: row.laiTBPhanTram >= 0 ? theme.primary : theme.red }}
                      >
                        {formatPercent(row.laiTBPhanTram)}
                      </td>
                      <td
                        className="px-2 py-2.5 text-right font-data tabular-nums"
                        style={{ color: theme.red }}
                      >
                        {formatPercent(row.loTBPhanTram)}
                      </td>
                      <td
                        className="px-2 py-2.5 text-right font-data tabular-nums"
                        style={{ color: row.tongLaiTongLo >= 0 ? theme.primary : theme.red }}
                      >
                        {formatPercent(row.tongLaiTongLo)}
                      </td>
                      <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface-variant">
                        {row.soLenhDong > 0 ? formatPercent(row.teNhatPhanTram) : "—"}
                      </td>
                      <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface-variant">
                        {row.giuTBPhien.toFixed(1)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </>
      ) : null}

      {/* Khối 3 — lịch sử lệnh */}
      <Card>
        <SectionTitle
          title="Lịch sử vị thế"
          subtitle={
            history && history.total > 0 ? `${history.total} vị thế` : undefined
          }
        />

        <div className="mb-3 flex flex-wrap gap-2">
          {STATUS_FILTERS.map((s) => {
            const active = s.value === status;
            return (
              <button
                key={s.value}
                type="button"
                onClick={() => {
                  setStatus(s.value);
                  setPage(1);
                }}
                className={cn(
                  "rounded-lg border px-3 py-1 text-xs font-medium transition-colors",
                  active
                    ? "border-primary/40 bg-primary/10 text-primary"
                    : "border-outline-variant bg-surface-low text-on-surface-variant hover:bg-surface-high",
                )}
              >
                {s.label}
              </button>
            );
          })}
        </div>

        {historyError && <p className="mb-3 text-sm text-negative">{historyError}</p>}

        {historyLoading ? (
          <div className="space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="h-12 animate-pulse rounded-xl bg-surface-low" />
            ))}
          </div>
        ) : history && history.items.length > 0 ? (
          <>
            <div className="-mx-1 overflow-x-auto">
              <table className="w-full min-w-[680px] border-collapse text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-on-surface-variant">
                    <th className="px-2 py-2 font-medium">Mã</th>
                    <th className="px-2 py-2 font-medium">Kịch bản</th>
                    <th className="px-2 py-2 text-right font-medium">Vào</th>
                    <th className="px-2 py-2 text-right font-medium">Thoát</th>
                    <th className="px-2 py-2 text-right font-medium">%</th>
                    <th className="px-2 py-2 text-center font-medium">KQ</th>
                    <th className="px-2 py-2 text-center font-medium">Trạng thái</th>
                    <th className="px-2 py-2 text-right font-medium">Ngày tín hiệu</th>
                  </tr>
                </thead>
                <tbody>
                  {history.items.map((item) => {
                    const style = ketQuaStyle(item.ketQua);
                    return (
                      <ViTheRow key={item.id} item={item} style={style} theme={theme} />
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Phân trang */}
            <div className="mt-4 flex items-center justify-between gap-3">
              <button
                type="button"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || historyLoading}
                className="flex items-center gap-1 rounded-xl border border-outline-variant bg-surface-low px-3 py-1.5 text-sm text-on-surface transition-colors hover:bg-surface-high disabled:opacity-40"
              >
                <ChevronLeft className="h-4 w-4" />
                Trước
              </button>
              <span className="text-xs text-on-surface-variant">
                Trang {page}/{totalPages}
              </span>
              <button
                type="button"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || historyLoading}
                className="flex items-center gap-1 rounded-xl border border-outline-variant bg-surface-low px-3 py-1.5 text-sm text-on-surface transition-colors hover:bg-surface-high disabled:opacity-40"
              >
                Sau
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </>
        ) : (
          <EmptyState />
        )}
      </Card>
    </div>
  );
}

function ViTheRow({
  item,
  style,
  theme,
}: {
  item: ViTheV3;
  style: { label: string; color: string; bg: string };
  theme: ReturnType<typeof useThemeTokens>;
}) {
  const phanTram = item.phanTramLoiNhuan;
  return (
    <tr className="border-t border-outline-variant/60">
      <td className="px-2 py-2.5">
        <Link
          to={`/stocks/${item.symbol}`}
          className="font-bold text-primary hover:underline"
        >
          {item.symbol}
        </Link>
      </td>
      <td className="px-2 py-2.5 text-on-surface-variant">{item.loai}</td>
      <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface">
        {item.giaVao != null && item.giaVao > 0 ? formatPrice(item.giaVao) : "—"}
      </td>
      <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface">
        {item.giaThoat != null ? formatPrice(item.giaThoat) : "—"}
      </td>
      <td
        className="px-2 py-2.5 text-right font-data tabular-nums"
        style={{
          color: phanTram == null ? theme.textMuted : phanTram >= 0 ? theme.primary : theme.red,
        }}
      >
        {phanTram != null ? formatPercent(phanTram) : "—"}
      </td>
      <td className="px-2 py-2.5 text-center">
        <span
          className="inline-block rounded-full px-2 py-0.5 text-[10px] font-bold"
          style={{ backgroundColor: style.bg, color: style.color }}
        >
          {style.label}
        </span>
      </td>
      <td className="px-2 py-2.5 text-center text-xs text-on-surface-variant">
        {TRANG_THAI_VI_THE_LABEL[item.trangThai] ?? item.trangThai}
      </td>
      <td className="px-2 py-2.5 text-right text-xs text-on-surface-variant">
        {formatShortDate(item.ngayTinHieu)}
      </td>
    </tr>
  );
}

function MetricCard({
  label,
  value,
  color,
}: {
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div className="glass-card rounded-2xl border border-outline-variant px-3 py-4 text-center">
      <p className="font-data text-2xl font-bold tabular-nums lg:text-3xl" style={{ color }}>
        {value}
      </p>
      <p className="mt-1 text-[11px] text-on-surface-variant">{label}</p>
    </div>
  );
}

function EmptyState() {
  return (
    <p className="py-6 text-center text-sm text-on-surface-variant">
      Chưa có vị thế V3. Dữ liệu xuất hiện sau khi bộ kịch bản P1++ bắn tín hiệu và khớp lệnh.
    </p>
  );
}
