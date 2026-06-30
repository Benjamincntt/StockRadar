import { useIntradayMonitorStatus } from "@/hooks/useIntradayMonitorStatus";
import { formatTime } from "@/lib/utils";
import { useThemeTokens } from "@/context/ThemeContext";
import { cn } from "@/lib/utils";

export function IntradayMonitorStatusLine({ className }: { className?: string }) {
  const theme = useThemeTokens();
  const { status, loading, error } = useIntradayMonitorStatus();

  if (loading && !status) {
    return (
      <p className={cn("text-xs text-on-surface-variant", className)}>
        Đang tải trạng thái quét lệnh đột biến…
      </p>
    );
  }

  if (error || !status) {
    return (
      <p className={cn("text-xs", className)} style={{ color: theme.red }}>
        Không đọc được trạng thái quét lệnh đột biến
      </p>
    );
  }

  const dotColor = !status.enabled
    ? theme.textSubtle
    : status.isStale
      ? theme.red
      : status.marketOpen
        ? theme.primary
        : theme.amber;

  const scanTime = status.lastScanAt ? formatTime(status.lastScanAt) : "—";
  const scanDetail =
    status.lastScanAt && status.lastSymbolsScanned > 0
      ? `${status.lastSymbolsScanned} mã · ${status.lastAlertsSent > 0 ? `+${status.lastAlertsSent} tín hiệu` : "0 tín hiệu mới"}`
      : status.lastScanAt
        ? "0 mã"
        : "chưa quét";

  return (
    <p
      className={cn("flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs", className)}
      title={`Quét lệnh đột biến mỗi ${status.intervalSeconds}s trong phiên · ${status.status}`}
    >
      <span className="inline-flex items-center gap-1.5 font-medium" style={{ color: dotColor }}>
        <span
          className={cn(
            "inline-block h-1.5 w-1.5 rounded-full",
            status.marketOpen && status.enabled && !status.isStale && "animate-pulse",
          )}
          style={{ backgroundColor: dotColor }}
        />
        Quét lệnh đột biến
      </span>
      <span className="text-on-surface-variant">
        Quét gần nhất: <span className="font-mono text-on-surface">{scanTime}</span>
        {" · "}
        {scanDetail}
      </span>
      <span
        className={cn(status.isStale && "font-medium")}
        style={{ color: status.isStale ? theme.red : theme.textSubtle }}
      >
        · {status.status}
      </span>
    </p>
  );
}
