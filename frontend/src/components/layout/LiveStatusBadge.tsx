import { useLiveMarket, type LiveConnectionState } from "@/context/LiveMarketContext";
import { useThemeTokens } from "@/context/ThemeContext";
import { formatTime } from "@/lib/utils";
import { cn } from "@/lib/utils";

const labels: Record<LiveConnectionState, string> = {
  connecting: "Đang kết nối",
  connected: "Trực tiếp",
  reconnecting: "Đang kết nối lại",
  disconnected: "Offline",
};

export function LiveStatusBadge({
  inline = false,
  className,
}: {
  inline?: boolean;
  className?: string;
}) {
  const { connectionState, lastUpdated } = useLiveMarket();
  const theme = useThemeTokens();

  const colors: Record<LiveConnectionState, string> = {
    connecting: theme.amber,
    connected: theme.primary,
    reconnecting: theme.amber,
    disconnected: theme.textSubtle,
  };

  const color = colors[connectionState];
  const pulse = connectionState === "connected";
  const title = lastUpdated ? `Cập nhật: ${formatTime(lastUpdated)}` : undefined;

  if (inline) {
    if (connectionState === "connected") {
      return (
        <span
          className={cn("inline-flex h-2 w-2 shrink-0", className)}
          title={title ?? "Đang nhận dữ liệu trực tiếp"}
        >
          <span
            className="h-full w-full animate-pulse rounded-full"
            style={{ backgroundColor: color, boxShadow: `0 0 6px ${color}` }}
          />
        </span>
      );
    }

    return (
      <span
        className={cn("text-[10px] text-on-surface-variant", className)}
        title={title}
      >
        {labels[connectionState]}
      </span>
    );
  }

  return (
    <div
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border border-outline-variant/40 bg-surface-high/60 px-2.5 py-1",
        className,
      )}
      title={title}
    >
      <span
        className={cn("h-1.5 w-1.5 rounded-full", pulse && "animate-pulse")}
        style={{ backgroundColor: color }}
      />
      <span className="text-[10px] font-medium text-on-surface-variant">
        {labels[connectionState]}
      </span>
    </div>
  );
}
