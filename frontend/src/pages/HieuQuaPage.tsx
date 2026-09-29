import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { api } from "@/lib/api";
import { Card, SectionTitle } from "@/components/ui/Card";
import { useThemeTokens } from "@/context/ThemeContext";
import { cn, formatPercent, formatPrice, formatShortDate } from "@/lib/utils";
import type { HieuQuaPeriod, HieuQuaTomTat, LichSuResponse } from "@/types";

const PERIODS: { value: HieuQuaPeriod; label: string }[] = [
  { value: "week", label: "Tuần" },
  { value: "month", label: "Tháng" },
  { value: "quarter", label: "Quý" },
  { value: "all", label: "Tất cả" },
];

const PAGE_SIZE = 20;

/** Màu sắc theo kết quả đo (nhãn có dấu trả về từ backend). */
function useKetQuaStyle() {
  const theme = useThemeTokens();
  return useCallback(
    (ketQua: string): { label: string; color: string; bg: string } => {
      switch (ketQua) {
        case "Thắng":
          return { label: "Thắng", color: theme.primary, bg: theme.greenBg };
        case "Thua":
          return { label: "Thua", color: theme.red, bg: theme.redBg };
        case "Ngang":
          return { label: "Ngang", color: theme.textMuted, bg: theme.neutralBg };
        default:
          return { label: "Chờ đo", color: theme.amber, bg: theme.amberBg };
      }
    },
    [theme],
  );
}

export function HieuQuaPage() {
  const theme = useThemeTokens();
  const ketQuaStyle = useKetQuaStyle();

  const [period, setPeriod] = useState<HieuQuaPeriod>("month");
  const [summary, setSummary] = useState<HieuQuaTomTat | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [summaryError, setSummaryError] = useState<string | null>(null);

  const [history, setHistory] = useState<LichSuResponse | null>(null);
  const [page, setPage] = useState(1);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [historyError, setHistoryError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setSummaryLoading(true);
    setSummaryError(null);
    api
      .getHieuQuaTomTat(period)
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
      .getHieuQuaLichSu(page, PAGE_SIZE)
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
  }, [page]);

  const totalPages = history
    ? Math.max(1, Math.ceil(history.totalCount / (history.pageSize || PAGE_SIZE)))
    : 1;

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-xl font-bold text-on-surface">Hiệu quả kịch bản</h1>
        <p className="mt-1 text-xs text-on-surface-variant">
          Đo lường outcome các kịch bản đã kích hoạt · cập nhật tự động sau T+2.5 phiên
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

      {/* Thẻ chỉ số */}
      {summaryLoading ? (
        <div className="grid grid-cols-3 gap-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-24 animate-pulse rounded-2xl bg-surface-low" />
          ))}
        </div>
      ) : summary ? (
        <>
          <div className="grid grid-cols-3 gap-3">
            <MetricCard
              label="Tỷ lệ Thắng"
              value={`${summary.tyLeThang.toFixed(1)}%`}
              color={theme.primary}
            />
            <MetricCard
              label="R:R TB"
              value={summary.tbRR.toFixed(2)}
              color={summary.tbRR >= 1 ? theme.primary : theme.red}
            />
            <MetricCard
              label="Tổng kích hoạt"
              value={String(summary.tongKichHoat)}
              color={theme.text}
            />
          </div>

          {/* Phân bố kết quả */}
          <div className="grid grid-cols-4 gap-2 text-center text-xs">
            <CountChip label="Thắng" value={summary.thang} color={theme.primary} bg={theme.greenBg} />
            <CountChip label="Thua" value={summary.thua} color={theme.red} bg={theme.redBg} />
            <CountChip label="Ngang" value={summary.ngang} color={theme.textMuted} bg={theme.neutralBg} />
            <CountChip label="Chờ đo" value={summary.choDo} color={theme.amber} bg={theme.amberBg} />
          </div>

          <p className="text-xs text-on-surface-variant">
            Lợi nhuận TB (đã đo):{" "}
            <span
              className="font-data font-semibold tabular-nums"
              style={{ color: summary.tbPhanTram >= 0 ? theme.primary : theme.red }}
            >
              {formatPercent(summary.tbPhanTram)}
            </span>
          </p>

          {/* Theo loại kịch bản */}
          <Card>
            <SectionTitle title="Theo loại kịch bản" />
            {summary.theoLoaiKichBan.length === 0 ? (
              <EmptyState />
            ) : (
              <div className="-mx-1 overflow-x-auto">
                <table className="w-full min-w-[440px] border-collapse text-sm">
                  <thead>
                    <tr className="text-left text-[11px] uppercase tracking-wide text-on-surface-variant">
                      <th className="px-2 py-2 font-medium">Kịch bản</th>
                      <th className="px-2 py-2 text-right font-medium">Tổng</th>
                      <th className="px-2 py-2 text-right font-medium">Thắng</th>
                      <th className="px-2 py-2 text-right font-medium">Tỷ lệ</th>
                      <th className="px-2 py-2 text-right font-medium">R:R</th>
                    </tr>
                  </thead>
                  <tbody>
                    {summary.theoLoaiKichBan.map((row) => (
                      <tr
                        key={row.tenKichBan}
                        className="border-t border-outline-variant/60"
                      >
                        <td className="px-2 py-2.5 font-medium text-on-surface">
                          {row.tenKichBan}
                        </td>
                        <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface-variant">
                          {row.tong}
                        </td>
                        <td
                          className="px-2 py-2.5 text-right font-data tabular-nums"
                          style={{ color: theme.primary }}
                        >
                          {row.thang}
                        </td>
                        <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface">
                          {row.tyLeThang.toFixed(0)}%
                        </td>
                        <td
                          className="px-2 py-2.5 text-right font-data tabular-nums"
                          style={{ color: row.tbRR >= 1 ? theme.primary : theme.red }}
                        >
                          {row.tbRR.toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      ) : null}

      {/* Lịch sử lệnh */}
      <Card>
        <SectionTitle
          title="Lịch sử lệnh"
          subtitle={
            history && history.totalCount > 0
              ? `${history.totalCount} kịch bản đã kích hoạt`
              : undefined
          }
        />

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
              <table className="w-full min-w-[560px] border-collapse text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-on-surface-variant">
                    <th className="px-2 py-2 font-medium">Mã</th>
                    <th className="px-2 py-2 font-medium">Kịch bản</th>
                    <th className="px-2 py-2 text-right font-medium">Vào</th>
                    <th className="px-2 py-2 text-right font-medium">Thoát</th>
                    <th className="px-2 py-2 text-right font-medium">%</th>
                    <th className="px-2 py-2 text-center font-medium">KQ</th>
                    <th className="px-2 py-2 text-right font-medium">Ngày</th>
                  </tr>
                </thead>
                <tbody>
                  {history.items.map((item) => {
                    const style = ketQuaStyle(item.ketQua);
                    return (
                      <tr
                        key={item.id}
                        className="border-t border-outline-variant/60"
                      >
                        <td className="px-2 py-2.5">
                          <Link
                            to={`/stocks/${item.symbol}`}
                            className="font-bold text-primary hover:underline"
                          >
                            {item.symbol}
                          </Link>
                        </td>
                        <td className="px-2 py-2.5 text-on-surface-variant">
                          {item.loaiKichBan}
                        </td>
                        <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface">
                          {item.giaVao > 0 ? formatPrice(item.giaVao) : "—"}
                        </td>
                        <td className="px-2 py-2.5 text-right font-data tabular-nums text-on-surface">
                          {item.giaThoat != null ? formatPrice(item.giaThoat) : "—"}
                        </td>
                        <td
                          className="px-2 py-2.5 text-right font-data tabular-nums"
                          style={{
                            color:
                              item.phanTram == null
                                ? theme.textMuted
                                : item.phanTram >= 0
                                  ? theme.primary
                                  : theme.red,
                          }}
                        >
                          {item.phanTram != null ? formatPercent(item.phanTram) : "—"}
                        </td>
                        <td className="px-2 py-2.5 text-center">
                          <span
                            className="inline-block rounded-full px-2 py-0.5 text-[10px] font-bold"
                            style={{ backgroundColor: style.bg, color: style.color }}
                          >
                            {style.label}
                          </span>
                        </td>
                        <td className="px-2 py-2.5 text-right text-xs text-on-surface-variant">
                          {formatShortDate(item.ngayKichHoat)}
                        </td>
                      </tr>
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

function CountChip({
  label,
  value,
  color,
  bg,
}: {
  label: string;
  value: number;
  color: string;
  bg: string;
}) {
  return (
    <div className="rounded-xl px-2 py-2" style={{ backgroundColor: bg }}>
      <p className="font-data text-lg font-bold tabular-nums" style={{ color }}>
        {value}
      </p>
      <p className="text-on-surface-variant">{label}</p>
    </div>
  );
}

function EmptyState() {
  return (
    <p className="py-6 text-center text-sm text-on-surface-variant">
      Chưa có dữ liệu. Hệ thống sẽ tự động đo lường sau T+2.5 phiên.
    </p>
  );
}
