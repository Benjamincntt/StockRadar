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


def evaluate(api_base: str, sync_key: str, min_pass_score: int, max_results: int, timeout: int) -> dict:
    url = f"{api_base.rstrip('/')}/ml/tune/evaluate"
    payload = json.dumps(
        {"minPassScore": min_pass_score, "maxResults": max_results}
    ).encode("utf-8")
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
    parser.add_argument("--timeout", type=int, default=120, help="Seconds per C# evaluate call")
    parser.add_argument(
        "--api-base",
        default=os.environ.get("API_BASE", "http://localhost:5280/api/v1"),
    )
    args = parser.parse_args()
    sync_key = load_sync_key()
    api_base = args.api_base.rstrip("/")

    print("=== StockRadar Hyperparameter Tuning (Optuna TPE) ===")
    print(f"API: {api_base}/ml/tune/evaluate")
    print(f"Trials: {args.trials}")

    def objective(trial: optuna.Trial) -> float:
        min_pass = trial.suggest_int("min_pass_score", 55, 75)
        max_results = trial.suggest_int("max_results", 5, 15)
        try:
            data = evaluate(api_base, sync_key, min_pass, max_results, args.timeout)
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as exc:
            print(f"Trial {trial.number} failed: {exc}")
            return float("-inf")

        fitness = float(data.get("fitnessScore", float("-inf")))
        print(
            f"Trial {trial.number} | MinPass={min_pass} MaxRes={max_results} "
            f"-> fitness={fitness} trades={data.get('totalTrades')} "
            f"hit={data.get('hitRateTopK')} mfe={data.get('avgMfe')}"
        )
        return fitness

    study = optuna.create_study(direction="maximize")
    study.optimize(objective, n_trials=args.trials)

    print("\n=== TUNING DONE ===")
    print(f"Best fitness: {study.best_value}")
    print("Best params:")
    for k, v in study.best_params.items():
        print(f"  {k}: {v}")
    print("\nGợi ý appsettings (KHÔNG auto-apply):")
    print(f"  SmartMoney.MinPassScore: {study.best_params.get('min_pass_score')}")
    print(f"  DailyAnalysis.MaxResults: {study.best_params.get('max_results')}")


if __name__ == "__main__":
    main()
