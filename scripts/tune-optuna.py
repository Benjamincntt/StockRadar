#!/usr/bin/env python3
"""StockRadar HPO — Optuna TPE gọi headless C# backtest (Phase 1).

Cài (server Ubuntu — dùng venv, tránh PEP 668):
  apt install -y python3-venv python3-full   # một lần
  bash scripts/setup-tune-venv.sh
  source .venv-tune/bin/activate

Ví dụ:
  export API_BASE=http://127.0.0.1:5281/api/v1
  export SYNC_KEY=your-sync-key
  python scripts/tune-optuna.py --trials 50
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone

try:
    import optuna
except ImportError:
    print("Thiếu optuna: pip install -r scripts/tune-optuna-requirements.txt", file=sys.stderr)
    sys.exit(1)


def load_sync_key() -> str:
    key = os.environ.get("SYNC_KEY", "").strip()
    if key:
        return key
    for path in (
        "/var/www/publish/stockradar-api/appsettings.Production.json",
        "backend/StockRadar.Api/appsettings.json",
    ):
        if os.path.isfile(path):
            with open(path, encoding="utf-8") as f:
                key = json.load(f)["MarketData"]["SyncApiKey"]
            if key:
                return key
    raise RuntimeError("SYNC_KEY not set and appsettings not found")


def default_api_base() -> str:
    if os.environ.get("API_BASE", "").strip():
        return os.environ["API_BASE"].strip().rstrip("/")
    if os.path.isfile("/var/www/publish/stockradar-api/appsettings.Production.json"):
        return "http://127.0.0.1:5281/api/v1"
    return "http://127.0.0.1:5280/api/v1"


def evaluate(
    api_base: str,
    sync_key: str,
    min_pass_score: int,
    max_results: int,
    timeout: int,
    days: int | None,
) -> dict:
    url = f"{api_base.rstrip('/')}/ml/tune/evaluate"
    body: dict[str, int] = {"minPassScore": min_pass_score, "maxResults": max_results}
    if days is not None:
        body["days"] = days
    payload = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=payload,
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
            "X-Sync-Key": sync_key,
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main() -> None:
    parser = argparse.ArgumentParser(description="StockRadar Optuna hyperparameter tuning")
    parser.add_argument("--trials", type=int, default=50)
    parser.add_argument("--timeout", type=int, default=300, help="Seconds per C# evaluate call")
    parser.add_argument("--days", type=int, default=30, help="Backtest lookback days (30=nhanh hon tren VPS)")
    parser.add_argument("--api-base", default=None, help="Mac dinh 5281 prod / 5280 dev")
    parser.add_argument("--output", default=None, help="Ghi ket qua JSON (weekly job)")
    args = parser.parse_args()
    sync_key = load_sync_key()
    api_base = (args.api_base or default_api_base()).rstrip("/")

    print("=== StockRadar Hyperparameter Tuning (Optuna TPE) ===")
    print(f"API: {api_base}/ml/tune/evaluate")
    print(f"Trials: {args.trials}, days: {args.days}, timeout: {args.timeout}s")

    failed_trials = 0
    last_metrics: dict | None = None

    def objective(trial: optuna.Trial) -> float:
        nonlocal failed_trials, last_metrics
        min_pass = trial.suggest_int("min_pass_score", 55, 75)
        max_results = trial.suggest_int("max_results", 5, 15)
        try:
            data = evaluate(
                api_base, sync_key, min_pass, max_results, args.timeout, args.days)
        except urllib.error.HTTPError as exc:
            failed_trials += 1
            body = exc.read().decode("utf-8", errors="replace")
            print(f"Trial {trial.number} HTTP {exc.code}: {body[:200]}")
            return float("-inf")
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as exc:
            failed_trials += 1
            print(f"Trial {trial.number} failed: {exc}")
            return float("-inf")

        fitness = float(data.get("fitnessScore", float("-inf")))
        last_metrics = data
        print(
            f"Trial {trial.number} | MinPass={min_pass} MaxRes={max_results} "
            f"-> fitness={fitness} trades={data.get('totalTrades')} "
            f"hit={data.get('hitRateTopK')} mfe={data.get('avgMfe')}"
        )
        return fitness

    study = optuna.create_study(direction="maximize")
    study.optimize(objective, n_trials=args.trials)

    print("\n=== TUNING DONE ===")
    result_doc: dict = {
        "completedAtUtc": datetime.now(timezone.utc).isoformat(),
        "trials": args.trials,
        "days": args.days,
        "successfulTrials": args.trials - failed_trials,
        "failedTrials": failed_trials,
        "bestFitness": None,
        "bestParams": None,
        "bestMetrics": None,
    }

    if study.best_value == float("-inf"):
        print("Khong trial nao thanh cong. Kiem tra:")
        print("  - API dung port (prod: http://127.0.0.1:5281/api/v1)")
        print("  - SYNC_KEY trong appsettings.Production.json")
        print("  - Tang --timeout neu backtest cham")
        if args.output:
            with open(args.output, "w", encoding="utf-8") as f:
                json.dump(result_doc, f, indent=2)
        sys.exit(1)

    result_doc["bestFitness"] = study.best_value
    result_doc["bestParams"] = study.best_params

    bp = study.best_params
    try:
        best_eval = evaluate(
            api_base,
            sync_key,
            int(bp["min_pass_score"]),
            int(bp["max_results"]),
            args.timeout,
            args.days,
        )
        result_doc["bestMetrics"] = {
            "hitRateTopK": best_eval.get("hitRateTopK"),
            "avgMfe": best_eval.get("avgMfe"),
            "maxDrawdown": best_eval.get("maxDrawdown"),
            "totalTrades": best_eval.get("totalTrades"),
        }
    except Exception as exc:
        print(f"Khong do lai best metrics: {exc}")
        if last_metrics:
            result_doc["bestMetrics"] = {
                "hitRateTopK": last_metrics.get("hitRateTopK"),
                "avgMfe": last_metrics.get("avgMfe"),
                "maxDrawdown": last_metrics.get("maxDrawdown"),
                "totalTrades": last_metrics.get("totalTrades"),
            }

    print(f"Best fitness: {study.best_value}")
    print("Best params:")
    for k, v in study.best_params.items():
        print(f"  {k}: {v}")
    print("\nGoi y appsettings (KHONG auto-apply):")
    print(f"  SmartMoney.MinPassScore: {study.best_params.get('min_pass_score')}")
    print(f"  DailyAnalysis.MaxResults: {study.best_params.get('max_results')}")

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            json.dump(result_doc, f, indent=2)
        print(f"Da ghi: {args.output}")


if __name__ == "__main__":
    main()
