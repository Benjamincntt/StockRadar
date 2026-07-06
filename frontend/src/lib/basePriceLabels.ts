import type { FlatBox } from "../types";

export const BASE_PRICE_LABELS = {
  base: "Nền giá",
  breakout: "Phá vỡ nền giá",
  breakUp: "Nổ hướng lên",
  breakDown: "Gãy nền",
} as const;

export const BREAK_UP_MIN_PERCENT = 3;

export function resolveBasePriceEventLabel(
  flatBox: FlatBox,
  latestPrice: number,
): string {
  if (flatBox.eventLabel) return flatBox.eventLabel;

  if (latestPrice < flatBox.boxLow) return BASE_PRICE_LABELS.breakDown;

  if (
    flatBox.isBreakoutConfirmed &&
    (flatBox.priceGainPercent ?? 0) >= BREAK_UP_MIN_PERCENT
  ) {
    return BASE_PRICE_LABELS.breakUp;
  }

  if (flatBox.isBreakoutConfirmed) return BASE_PRICE_LABELS.breakout;

  return BASE_PRICE_LABELS.base;
}

export function flatBoxCardSubtitle(flatBox: FlatBox, latestPrice: number): string {
  if (!flatBox.isBreakoutConfirmed) {
    return `Đang tích lũy — ${flatBox.refBoxPeriod}`;
  }
  return resolveBasePriceEventLabel(flatBox, latestPrice);
}
