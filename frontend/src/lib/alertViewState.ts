import type { AlertFeedScope } from "@/types";

const MAX_STORED = 500;

function storageKey(feed: AlertFeedScope) {
  return feed === "universe"
    ? "stockradar-viewed-universe-alerts"
    : "stockradar-viewed-alerts";
}

function loadViewedIds(feed: AlertFeedScope): Set<string> {
  try {
    const raw = localStorage.getItem(storageKey(feed));
    if (!raw) return new Set();
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return new Set();
    return new Set(parsed.filter((id): id is string => typeof id === "string"));
  } catch {
    return new Set();
  }
}

function saveViewedIds(feed: AlertFeedScope, ids: Set<string>) {
  const list = [...ids].slice(-MAX_STORED);
  localStorage.setItem(storageKey(feed), JSON.stringify(list));
}

export function getViewedAlertIds(feed: AlertFeedScope = "opportunity"): Set<string> {
  return loadViewedIds(feed);
}

export function markAlertViewed(id: string, feed: AlertFeedScope = "opportunity"): Set<string> {
  const next = loadViewedIds(feed);
  next.add(id);
  saveViewedIds(feed, next);
  return next;
}

export function isAlertViewed(
  id: string,
  feed: AlertFeedScope = "opportunity",
  viewedIds?: Set<string>,
): boolean {
  return (viewedIds ?? loadViewedIds(feed)).has(id);
}
