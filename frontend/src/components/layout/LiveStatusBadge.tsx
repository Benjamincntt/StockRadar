import { useLiveMarket, type LiveConnectionState } from "@/context/LiveMarketContext";
import { formatTime } from "@/lib/utils";
import { theme } from "@/theme/tokens";

const labels: Record<LiveConnectionState, string> = {
  connecting: "Đang kết nối",
  connected: "Realtime",
  reconnecting: "Đang kết nối lại",
  disconnected: "Offline",
};

const colors: Record<LiveConnectionState, string> = {
  connecting: theme.amber,
  connected: theme.green,
  reconnecting: theme.amber,
  disconnected: theme.textMuted,
};

export function LiveStatusBadge() {
  const { connectionState, lastUpdated } = useLiveMarket();
  const color = colors[connectionState];
  const pulse = connectionState === "connected";

  return (
    <div
      className="flex items-center gap-1.5 rounded-full px-2 py-1"
      style={{ backgroundColor: theme.surfaceMuted }}
      title={lastUpdated ? `Cập nhật: ${formatTime(lastUpdated)}` : undefined}
    >
      <span
        className={`h-2 w-2 rounded-full ${pulse ? "animate-pulse" : ""}`}
        style={{ backgroundColor: color }}
      />
      <span className="text-[10px] font-semibold text-gray-600">{labels[connectionState]}</span>
    </div>
  );
}
