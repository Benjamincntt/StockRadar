#!/usr/bin/env python3
"""Backfill toàn bộ lịch sử OHLCV (KBS/vnstock) → StockRadar API."""

from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path

import requests

_cfg = os.environ.get("STOCKRADAR_SYNC_CONFIG", "").strip()
CONFIG_PATH = Path(_cfg) if _cfg else Path(__file__).with_name("config.json")

DEFAULT_GROUPS = ("VN100", "VNMidCap")
DEFAULT_START = "2000-01-01"


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(f"Thiếu file cấu hình: {CONFIG_PATH}")
    with CONFIG_PATH.open(encoding="utf-8") as f:
        return json.load(f)


def resolve_groups(cfg: dict) -> list[str]:
    groups = cfg.get("backfill_groups") or list(DEFAULT_GROUPS)
    return [g.strip() for g in groups if g and str(g).strip()]


def resolve_start(cfg: dict) -> str:
    return str(cfg.get("backfill_start_date") or DEFAULT_START).strip()


def fetch_union_symbols(groups: list[str]) -> list[str]:
    from vnstock import Listing

    listing = Listing(source="KBS")
    symbols: set[str] = set()
    for group in groups:
        series = listing.symbols_by_group(group=group)
        for sym in series:
            if sym and str(sym).strip():
                symbols.add(str(sym).strip().upper())
        print(f"  {group}: {len(series)} mã")
    return sorted(symbols)


def fetch_sector_map() -> dict[str, str]:
    from vnstock import Listing

    listing = Listing(source="KBS")
    df = listing.symbols_by_industries()
    if df is None or df.empty:
        return {}
    out: dict[str, str] = {}
    for _, row in df.iterrows():
        sym = str(row.get("symbol", "")).strip().upper()
        sector = str(row.get("industry_name", "")).strip()
        if sym:
            out[sym] = sector
    return out


def fetch_history(symbol: str, start: str, end: str | None = None):
    from vnstock import Quote

    quote = Quote(source="KBS", symbol=symbol)
    kwargs: dict = {"start": start}
    if end:
        kwargs["end"] = end
    return quote.history(**kwargs)


def push_history(cfg: dict, symbol: str, name: str | None, sector: str | None, bars: list[dict]) -> bool:
    base = cfg["api_base_url"].rstrip("/")
    key = cfg["sync_api_key"]
    payload = {
        "symbol": symbol,
        "name": name,
        "sector": sector,
        "bars": bars,
    }
    r = requests.post(
        f"{base}/api/v1/market/history/bulk",
        headers={"X-Sync-Key": key, "Content-Type": "application/json"},
        json=payload,
        timeout=120,
    )
    if r.status_code == 200:
        return True
    print(f"  API {symbol} HTTP {r.status_code}: {r.text[:200]}")
    return False


def bars_from_df(df) -> list[dict]:
    if df is None or df.empty:
        return []
    rows: list[dict] = []
    for _, row in df.iterrows():
        ts = row.get("time") if "time" in row.index else row.get("date")
        if ts is None:
            continue
        date_str = str(ts)[:10]
        rows.append(
            {
                "date": date_str,
                "open": float(row.get("open", 0) or 0),
                "high": float(row.get("high", 0) or 0),
                "low": float(row.get("low", 0) or 0),
                "close": float(row.get("close", 0) or 0),
                "volume": int(float(row.get("volume", 0) or 0)),
            }
        )
    return rows


def trigger_api_backfill(cfg: dict, groups: list[str], start: str) -> int:
    """Gọi job C# built-in (khuyến nghị khi không cài vnstock)."""
    base = cfg["api_base_url"].rstrip("/")
    key = cfg["sync_api_key"]
    r = requests.post(
        f"{base}/api/v1/market/history/backfill",
        headers={"X-Sync-Key": key, "Content-Type": "application/json"},
        json={"groups": groups, "startDate": start},
        timeout=3600,
    )
    print(f"API backfill HTTP {r.status_code}")
    if r.ok:
        data = r.json()
        print(json.dumps(data, indent=2, ensure_ascii=False))
        return 0
    print(r.text[:500])
    return 1


def run_backfill(cfg: dict, use_api: bool = False) -> int:
    groups = resolve_groups(cfg)
    start = resolve_start(cfg)

    if use_api:
        print(f"Kích hoạt backfill qua API C# — nhóm {groups}, từ {start}")
        return trigger_api_backfill(cfg, groups, start)

    print(f"Backfill Python/vnstock — nhóm {groups}, từ {start}")
    symbols = fetch_union_symbols(groups)
    print(f"Tổng {len(symbols)} mã (VN100 ∪ VNMidCap).")

    sectors = fetch_sector_map()
    delay = float(cfg.get("backfill_delay_seconds", 0.4))
    ok = 0
    fail = 0

    for i, symbol in enumerate(symbols, 1):
        try:
            df = fetch_history(symbol, start)
            bars = bars_from_df(df)
            if not bars:
                print(f"[{i}/{len(symbols)}] {symbol}: không có dữ liệu")
                fail += 1
                continue
            sector = sectors.get(symbol)
            if push_history(cfg, symbol, None, sector, bars):
                print(f"[{i}/{len(symbols)}] {symbol}: {len(bars)} nến")
                ok += 1
            else:
                fail += 1
        except Exception as exc:
            print(f"[{i}/{len(symbols)}] {symbol}: lỗi {exc}")
            fail += 1
        if i < len(symbols):
            time.sleep(delay)

    print(f"Xong: {ok} thành công, {fail} lỗi / {len(symbols)} mã.")
    return 0 if fail == 0 else 1


def main() -> int:
    use_api = "--api" in sys.argv or "--vnstock" not in sys.argv
    cfg = load_config()
    return run_backfill(cfg, use_api=use_api)


if __name__ == "__main__":
    raise SystemExit(main())
