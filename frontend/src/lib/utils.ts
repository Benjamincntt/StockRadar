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

export function sparklineTrend(changePercent: number): "up" | "down" | "flat" {
  if (changePercent > 0.5) return "up";
  if (changePercent < -0.5) return "down";
  return "flat";
}
