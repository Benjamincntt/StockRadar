import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatPercent(value: number) {
  const sign = value > 0 ? "+" : "";
  return `${sign}${value.toFixed(2)}%`;
}

export function formatPrice(value: number) {
  return value.toLocaleString("vi-VN", { maximumFractionDigits: 2 });
}

export function formatVolume(value: number) {
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)}B`;
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(3)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return value.toLocaleString("vi-VN");
}

const VN_TIME_ZONE = "Asia/Ho_Chi_Minh";

/** API lưu UTC; chuỗi ISO đôi khi thiếu hậu tố Z → parse như UTC. */
export function parseApiDate(iso: string): Date {
  const trimmed = iso.trim();
  if (!trimmed) return new Date(NaN);
  const hasZone = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(trimmed);
  return new Date(hasZone ? trimmed : `${trimmed}Z`);
}

export function formatTime(iso: string) {
  const d = parseApiDate(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: VN_TIME_ZONE,
  });
}

function vietnamCalendarDate(isoOrDate: string | Date): string {
  const d = typeof isoOrDate === "string" ? parseApiDate(isoOrDate) : isoOrDate;
  return d.toLocaleDateString("en-CA", { timeZone: VN_TIME_ZONE });
}

export function isTodayVietnam(iso: string): boolean {
  const d = parseApiDate(iso);
  if (Number.isNaN(d.getTime())) return false;
  return vietnamCalendarDate(d) === vietnamCalendarDate(new Date());
}

/** Giờ trong phiên hôm nay; ngày khác thì kèm ngày để tránh nhầm với phiên cũ. */
export function formatAlertTime(iso: string) {
  if (isTodayVietnam(iso)) return formatTime(iso);
  return formatDateTime(iso);
}

export function formatDateTime(iso: string) {
  const d = parseApiDate(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: VN_TIME_ZONE,
  });
}

export function formatShortDate(iso: string) {
  const d = parseApiDate(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    timeZone: VN_TIME_ZONE,
  });
}

/** Thời gian còn lại đến khi hết cooldown (hiển thị trên UI). */
export function formatCooldownRemaining(untilIso: string, nowMs = Date.now()): string {
  const until = parseApiDate(untilIso).getTime();
  if (Number.isNaN(until)) return "";
  const totalSec = Math.max(0, Math.ceil((until - nowMs) / 1000));
  const min = Math.floor(totalSec / 60);
  const sec = totalSec % 60;
  if (min > 0) {
    return sec > 0 ? `${min} phút ${sec} giây` : `${min} phút`;
  }
  return `${sec} giây`;
}

export function formatBasePricePeriods(
  periods: { fromDate: string; toDate: string }[],
): string {
  return periods
    .map((p) => `${formatShortDate(p.fromDate)}–${formatShortDate(p.toDate)}`)
    .join(", ");
}

const BASE_SESSION_MIN = 12;
const BASE_SESSION_MAX = 28;

function lerpChannel(a: number, b: number, t: number) {
  return Math.round(a + (b - a) * t);
}

/** Màu số phiên trong nền: 12 phiên = đỏ nhạt → càng nhiều phiên càng xanh đậm. */
export function getBaseSessionDaysStyle(sessionDays: number): {
  color: string;
  backgroundColor: string;
  borderColor: string;
} {
  const t = Math.max(0, Math.min(1, (sessionDays - BASE_SESSION_MIN) / (BASE_SESSION_MAX - BASE_SESSION_MIN)));

  const color = `rgb(${lerpChannel(248, 22, t)}, ${lerpChannel(113, 101, t)}, ${lerpChannel(113, 52, t)})`;
  const backgroundColor = `rgb(${lerpChannel(254, 187, t)}, ${lerpChannel(226, 247, t)}, ${lerpChannel(226, 208, t)})`;
  const borderColor = `rgb(${lerpChannel(252, 34, t)}, ${lerpChannel(165, 197, t)}, ${lerpChannel(165, 94, t)})`;

  return { color, backgroundColor, borderColor };
}

export function trendLabel(trend: string) {
  switch (trend) {
    case "Uptrend":
      return "UPTREND";
    case "Downtrend":
      return "DOWNTREND";
    default:
      return "SIDEWAY";
  }
}

export function signalLabelVi(type: string) {
  const map: Record<string, string> = {
    Breakout: "Vượt đỉnh",
    VolumeSpike: "Bùng nổ khối lượng",
    Accumulation: "Tích lũy",
    Shakeout: "Rũ hàng",
    Distribution: "Phân phối",
    RelativeStrength: "Mạnh hơn thị trường",
  };
  return map[type] ?? type;
}
export function sparklineTrend(changePercent: number): "up" | "down" | "flat" {
  if (changePercent > 0.5) return "up";
  if (changePercent < -0.5) return "down";
  return "flat";
}
