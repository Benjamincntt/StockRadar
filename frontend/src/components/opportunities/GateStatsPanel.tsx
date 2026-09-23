import { useMemo, useState } from "react";
import { cn } from "@/lib/utils";
import type { OpportunityAnalysisStatus } from "@/types";

interface GateStatsPanelProps {
  /** Nhãn gate (tiếng Việt) → số mã bị loại. */
  gateStats?: Record<string, number> | null;
  analysisStatus?: OpportunityAnalysisStatus | null;
  /** Tổng số mã đã quét (universe). */
  stocksScored?: number | null;
  /** Số mã đạt strict (đã lưu). */
  opportunitiesSaved?: number | null;
  className?: string;
}

/**
 * Bảng thống kê số mã bị loại theo từng gate của lần quét gần nhất.
 * - `zero_matches`: mở sẵn, nhấn mạnh để giải thích VÌ SAO không có mã nào đạt.
 * - `has_results`: thu gọn mặc định, hiển thị như chi tiết phụ.
 */
export function GateStatsPanel({
  gateStats,
  analysisStatus,
  stocksScored,
  opportunitiesSaved,
  className,
}: GateStatsPanelProps) {
  const rows = useMemo(() => {
    if (!gateStats) return [];
    return Object.entries(gateStats)
      .filter(([, count]) => Number.isFinite(count) && count > 0)
      .sort((a, b) => b[1] - a[1]);
  }, [gateStats]);

  // zero_matches → mở sẵn để người dùng thấy lý do; các trạng thái khác thu gọn.
  const [open, setOpen] = useState(analysisStatus === "zero_matches");

  const totalRejected = useMemo(
    () => rows.reduce((sum, [, count]) => sum + count, 0),
    [rows],
  );
  const maxCount = rows.length > 0 ? rows[0][1] : 0;

  if (rows.length === 0) return null;

  const prominent = analysisStatus === "zero_matches";

  return (
    <div
      className={cn(
        "mb-3 overflow-hidden rounded-xl border",
        prominent
          ? "border-orange-500/30 bg-orange-500/[0.06]"
          : "border-outline-variant bg-surface-low/60",
        className,
      )}
    >
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full items-center justify-between gap-3 px-3 py-2.5 text-left transition-colors hover:bg-surface-high/50"
      >
        <span className="flex min-w-0 items-center gap-2">
          <span
            className={cn(
              "shrink-0 text-sm font-semibold",
              prominent ? "text-orange-900 dark:text-orange-100" : "text-on-surface",
            )}
          >
            Thống kê lọc
          </span>
          <span className="truncate text-xs text-on-surface-variant">
            {totalRejected.toLocaleString("vi-VN")} mã bị loại
            {typeof stocksScored === "number" && stocksScored > 0
              ? ` / ${stocksScored.toLocaleString("vi-VN")} mã quét`
              : ""}
            {typeof opportunitiesSaved === "number"
              ? ` · ${opportunitiesSaved.toLocaleString("vi-VN")} đạt`
              : ""}
          </span>
        </span>
        <svg
          viewBox="0 0 20 20"
          fill="none"
          aria-hidden="true"
          className={cn(
            "h-4 w-4 shrink-0 text-on-surface-variant transition-transform duration-200",
            open && "rotate-180",
          )}
        >
          <path
            d="M5 7.5L10 12.5L15 7.5"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </button>

      <div
        className={cn(
          "grid transition-[grid-template-rows] duration-300 ease-out",
          open ? "grid-rows-[1fr]" : "grid-rows-[0fr]",
        )}
      >
        <div className="overflow-hidden">
          <ul className="space-y-2 border-t border-outline-variant/60 px-3 py-3">
            {rows.map(([gate, count]) => {
              const pct = maxCount > 0 ? (count / maxCount) * 100 : 0;
              const share =
                totalRejected > 0 ? (count / totalRejected) * 100 : 0;
              return (
                <li key={gate} className="space-y-1">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="min-w-0 truncate text-xs text-on-surface">
                      {gate}
                    </span>
                    <span className="shrink-0 text-xs font-semibold tabular-nums text-on-surface-variant">
                      {count.toLocaleString("vi-VN")}
                      <span className="ml-1 font-normal opacity-70">
                        ({share.toFixed(0)}%)
                      </span>
                    </span>
                  </div>
                  <div className="h-1.5 w-full overflow-hidden rounded-full bg-surface-high">
                    <div
                      className={cn(
                        "h-full rounded-full transition-[width] duration-500 ease-out",
                        prominent
                          ? "bg-orange-500/70"
                          : "bg-primary/60",
                      )}
                      style={{ width: `${pct}%` }}
                    />
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      </div>
    </div>
  );
}
