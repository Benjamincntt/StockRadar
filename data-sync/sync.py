#!/usr/bin/env python3
"""Đồng bộ bảng giá KBS (vnstock) → StockRadar API."""

from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime, time as dt_time
from pathlib import Path
from zoneinfo import ZoneInfo

import requests

_cfg = os.environ.get("STOCKRADAR_SYNC_CONFIG", "").strip()
CONFIG_PATH = Path(_cfg) if _cfg else Path(__file__).with_name("config.json")
VN_TZ = ZoneInfo("Asia/Ho_Chi_Minh")
INDEX_ALIASES = ("VNINDEX", "VN-INDEX", "VN_INDEX")


def load_config() -> dict:
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(f"Thiếu file cấu hình: {CONFIG_PATH}")
    with CONFIG_PATH.open(encoding="utf-8") as f:
        return json.load(f)


def is_market_open(now: datetime | None = None) -> bool:
    now = now or datetime.now(VN_TZ)
    if now.weekday() >= 5:
        return False
    t = now.time()
    morning = dt_time(9, 0) <= t <= dt_time(11, 30)
    afternoon = dt_time(13, 0) <= t <= dt_time(14, 45)
    return morning or afternoon


def _col(row, *names, default=None):
    for name in names:
        if name in row.index:
            val = row[name]
            if val is not None and str(val) != "nan":
                return val
    return default


def _to_float(val, default: float = 0.0) -> float:
    try:
        if val is None:
            return default
        return float(val)
    except (TypeError, ValueError):
        return default


def _to_int(val, default: int = 0) -> int:
    try:
        if val is None:
            return default
        return int(float(val))
    except (TypeError, ValueError):
        return default


def fetch_price_board(symbols: list[str]):
    from vnstock import Trading

    trading = Trading(source="KBS")
    unique = list(dict.fromkeys(s.upper().strip() for s in symbols if s))
    if not unique:
        return None
    return trading.price_board(symbols_list=unique)


def fetch_sector_map() -> dict[str, str]:
    from vnstock import Listing

    listing = Listing(source="KBS")
    df = listing.symbols_by_industries()
    if df is None or df.empty:
        return {}

    symbol_col = "symbol" if "symbol" in df.columns else None
    sector_col = "industry_name" if "industry_name" in df.columns else None
    if not symbol_col or not sector_col:
        return {}

    return {
        str(row[symbol_col]).upper().strip(): str(row[sector_col]).strip()
        for _, row in df.iterrows()
        if str(row[symbol_col]).strip() and str(row[sector_col]).strip()
    }


def row_to_quote(row, sector_map: dict[str, str]) -> dict | None:
    symbol = str(_col(row, "symbol", default="")).upper().strip()
    if not symbol:
        return None

    close = _to_float(_col(row, "close_price", "match_price", "price", "close"))
    if close <= 0:
        close = _to_float(_col(row, "reference_price", "ref_price"))
    if close <= 0:
        return None

    open_ = _to_float(_col(row, "open_price", "open"), close)
    high = _to_float(_col(row, "high_price", "high"), close)
    low = _to_float(_col(row, "low_price", "low"), close)
    volume = _to_int(
        _col(
            row,
            "accumulated_volume",
            "match_volume",
            "total_volume",
            "volume",
            "accumulated_vol",
        )
    )
    change_pct = _to_float(_col(row, "percent_change", "change_percent", "pct_change"))

    sector = sector_map.get(symbol, "")
    if not sector:
        sector = str(_col(row, "industry_name", "icb_name3", "sector", default="")).strip()

    return {
        "symbol": symbol,
        "name": str(_col(row, "organ_name", "name", "company_name", default=symbol)),
        "open": open_,
        "high": high,
        "low": low,
        "close": close,
        "volume": volume,
        "changePercent": change_pct,
        "sector": sector or None,
    }


def fetch_vnindex_kbs() -> dict | None:
    """VNINDEX từ KBS index API (stock/iss không trả giá chỉ số)."""
    from datetime import datetime, timedelta

    today = datetime.now(VN_TZ).date()
    start = today - timedelta(days=7)
    fmt = lambda d: d.strftime("%d-%m-%Y")

    def _bars(suffix: str, s, e):
        url = (
            "https://kbbuddywts.kbsec.com.vn/iis-server/investment/index/VNINDEX/"
            f"{suffix}?sdate={fmt(s)}&edate={fmt(e)}"
        )
        try:
            resp = requests.get(url, headers={"x-lang": "vi"}, timeout=20)
            if not resp.ok:
                return []
            data = resp.json().get(suffix) or []
            return sorted(data, key=lambda x: str(x.get("t", "")))
        except requests.RequestException:
            return []

    intraday = _bars("data_5P", today, today)
    daily = _bars("data_day", start, today)
    if not intraday and not daily:
        return None

    prev_close = float(daily[-2]["c"]) if len(daily) >= 2 else float(daily[-1]["c"])
    if intraday:
        price = float(intraday[-1]["c"])
        ref = float(intraday[0].get("o") or prev_close) or prev_close
    else:
        price = float(daily[-1]["c"])
        ref = prev_close

    if price <= 0:
        return None

    change_pct = ((price - ref) / ref * 100) if ref else 0.0
    return {"symbol": "VNINDEX", "price": price, "changePercent": round(change_pct, 2)}


def split_index_and_stocks(quotes: list[dict], index_symbol: str) -> tuple[dict | None, list[dict]]:
    index_symbol = index_symbol.upper()
    index_aliases = {index_symbol, *INDEX_ALIASES}
    index_dto = None
    stocks: list[dict] = []

    for q in quotes:
        sym = q["symbol"].upper()
        if sym in index_aliases:
            index_dto = {
                "symbol": index_symbol,
                "price": q["close"],
                "changePercent": q["changePercent"],
            }
        else:
            stocks.append(q)

    return index_dto, stocks


def resolve_symbols(cfg: dict) -> list[str]:
    if not cfg.get("use_api_symbols", True):
        return list(cfg.get("symbols") or [])

    url = f"{cfg['api_base_url'].rstrip('/')}/market/sync/symbols"
    headers = {"X-Sync-Key": cfg["sync_api_key"]}
    try:
        resp = requests.get(url, headers=headers, timeout=15)
        if resp.ok:
            api_symbols = [s for s in resp.json() if s.upper() not in INDEX_ALIASES]
            return api_symbols
    except requests.RequestException as exc:
        print(f"[warn] Không lấy được symbols từ API: {exc}")

    return []


def push_sync(cfg: dict, payload: dict) -> dict:
    url = f"{cfg['api_base_url'].rstrip('/')}/market/sync"
    headers = {
        "X-Sync-Key": cfg["sync_api_key"],
        "Content-Type": "application/json",
    }
    resp = requests.post(url, headers=headers, json=payload, timeout=30)
    resp.raise_for_status()
    return resp.json()


def run_sync(cfg: dict) -> None:
    index_symbol = str(cfg.get("index_symbol", "VNINDEX")).upper()
    symbols = resolve_symbols(cfg)
    if not symbols:
        print("[sync] DB trống — không có mã để sync. Chạy Job 1 (backfill) trước.")
        return

    fetch_symbols = list(dict.fromkeys([*symbols, index_symbol]))

    print(f"[sync] KBS price_board: {len(fetch_symbols)} mã...")
    sector_map = fetch_sector_map()
    if sector_map:
        print(f"[sync] sector map: {len(sector_map)} mã từ vnstock Listing")
    board = fetch_price_board(fetch_symbols)
    if board is None or board.empty:
        print("[sync] Không có dữ liệu từ vnstock KBS.")
        return

    quotes = [q for q in (row_to_quote(row, sector_map) for _, row in board.iterrows()) if q]
    index_dto, stock_quotes = split_index_and_stocks(quotes, index_symbol)

    if index_dto is None:
        index_dto = fetch_vnindex_kbs()
        if index_dto:
            print("[sync] VNINDEX từ KBS index API")
        else:
            print("[warn] Không lấy được VNINDEX — chỉ sync cổ phiếu.")

    payload = {"index": index_dto, "quotes": stock_quotes}
    result = push_sync(cfg, payload)
    print(
        f"[ok] stocks={result.get('stocksUpdated', 0)}, "
        f"index={'yes' if result.get('indexUpdated') else 'no'}, "
        f"at={result.get('syncedAt')}"
    )


def main() -> int:
    cfg = load_config()
    run_once = bool(cfg.get("run_once", False))
    interval = max(30, int(cfg.get("interval_seconds", 120)))

    print("StockRadar vnstock KBS sync worker")
    print(f"  API: {cfg['api_base_url']}")
    print(f"  Interval: {interval}s | run_once={run_once}")

    while True:
        try:
            if cfg.get("force_sync") or is_market_open():
                run_sync(cfg)
            else:
                print(f"[skip] Ngoài giờ giao dịch VN ({datetime.now(VN_TZ):%H:%M})")
        except Exception as exc:  # noqa: BLE001
            print(f"[error] {exc}", file=sys.stderr)

        if run_once:
            break
        time.sleep(interval)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
