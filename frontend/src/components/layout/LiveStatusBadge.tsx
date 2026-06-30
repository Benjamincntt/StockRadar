import { useLiveMarket, type LiveConnectionState } from "@/context/LiveMarketContext";
import { useThemeTokens } from "@/context/ThemeContext";
import { formatTime } from "@/lib/utils";

const labels: Record<LiveConnectionState, string> = {
  connecting: "Đang kết nối",
  connected: "Realtime",
  reconnecting: "Đang kết nối lại",
  disconnected: "Offline",
};

export function LiveStatusBadge({ inline = false }: { inline?: boolean }) {
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

  if (inline) {
    return (
      <span
        className="text-[10px] font-normal uppercase tracking-wider text-primary"
        title={lastUpdated ? `Cập nhật: ${formatTime(lastUpdated)}` : undefined}
      >
        {labels[connectionState]}
      </span>
    );
  }

  return (
    <div
      className="flex items-center gap-1.5 rounded-full bg-surface-low px-2 py-1"
      title={lastUpdated ? `Cập nhật: ${formatTime(lastUpdated)}` : undefined}
    >
      <span
        className={`h-2 w-2 rounded-full ${pulse ? "animate-pulse" : ""}`}
        style={{ backgroundColor: color }}
      />
      <span className="text-[10px] font-semibold text-on-surface-variant">
        {labels[connectionState]}
      </span>
    </div>
  );
}
