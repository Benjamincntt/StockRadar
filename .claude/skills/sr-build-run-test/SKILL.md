---
name: sr-build-run-test
description: Build, run, and test the StockRadar (JUICE) workspace correctly. Use before any debugging, testing, or verification work to pick the right project, host, port, test filter, and script in this monorepo. Also use to reproduce a bug against the running API, web, or mobile client.
---

# StockRadar Build Run Test

## Purpose

One repo, three runtimes (.NET API, React web, Flutter mobile) plus a scheduled job pipeline and a production host. Guessing the wrong project or script wastes a session and can hit production. Read this before running any `dotnet`, `npm`, `flutter`, or `scripts/*` command. Facts below are snapshots verified against disk on 2026-08-18 — if disk disagrees, trust disk and fix this skill in the same change.

## Workspace Layout

- `backend/StockRadar.slnx` — 5 projects, all `net10.0`: `StockRadar.Api`, `.Application`, `.Domain`, `.Infrastructure`, `.Tests`. There is no separate migrator host.
- `frontend/` — React + TypeScript + Vite, `@microsoft/signalr` client.
- `mobile/` — Flutter app `juice_app` (Dart SDK `>=3.3.0 <4.0.0`).
- `scripts/` — pipeline, deploy, backfill, DB helpers.
- `docs/domain/` — living rules; `specs/` — Spec Kit artifacts.

## Hosts and Ports

| Surface | Command | URL |
|---|---|---|
| API (dev) | `backend/restart-api.ps1` | `http://localhost:5280` (Swagger at `/swagger`) |
| Web (dev) | `frontend/run-dev.ps1` or `npm run dev` | `http://localhost:5173` |
| API (prod) | do not start; already running | `http://103.226.248.6/api/v1` |

`backend/restart-api.ps1` stops the old process, builds Debug, and relaunches detached — logs at `backend/logs/api-dev.log`, PID at `backend/logs/api-dev.pid`. Use `-SkipBuild` when only restarting. Companion scripts: `stop-api.ps1`, `run-api.ps1`, `publish-api.ps1`, `start-api-published.ps1`.

Per project convention: after any backend change, run `backend/restart-api.ps1` rather than a bare `dotnet run`.

## Commands

Build the narrowest thing that proves the change:

```powershell
dotnet build backend/StockRadar.Domain/StockRadar.Domain.csproj
dotnet build backend/StockRadar.slnx
```

Tests are xUnit in the single project `backend/StockRadar.Tests` (`Microsoft.EntityFrameworkCore.InMemory` for data paths). Suites by folder: `MarketPhase`, `Playbook`, `RealizedPnl`, `ReversalBounce`, `SectorWave`, `SellExit`, `VipAlerts`.

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter "FullyQualifiedName~ReversalBounce"
```

Per project convention, do not run a full-solution build or `flutter analyze` for a 1–2 file edit unless the user asks.

## Reproducing a Bug

1. Scoring / gate / Top / VIP behavior → run the API on `5280`, hit the endpoint, and read `backend/logs/api-dev.log`.
2. Pipeline behavior (universe, append, daily analysis, monitor) → `scripts/run-daily-jobs.ps1`, `scripts/run-pipeline-jobs.sh`, `scripts/run-opportunity-monitor.ps1`. Prefer local (`-LocalOnly` where the script offers it).
3. Web UI → Vite dev on `5173` against the local API; use `browser-qa-execution` for evidence.
4. Mobile → `flutter run` in `mobile/`; state the device/emulator used.
5. Data questions → read-only `sqlcmd` against the local `StockRadarDb`. Any write goes through `sql-data-review` first.

## Database and Migrations

`ApplicationDbContext` lives at `backend/StockRadar.Infrastructure/Persistence/ApplicationDbContext.cs`. The **live** migration set is `backend/StockRadar.Infrastructure/Migrations/` (namespace `StockRadar.Infrastructure.Migrations`) — it owns `ApplicationDbContextModelSnapshot.cs`.

Two traps, both verified:

- `backend/StockRadar.Infrastructure/Persistence/Migrations/` holds one stray migration (`AddEarlyRecoveryRadar`) in namespace `StockRadar.Infrastructure.Persistence.Migrations` with no snapshot. It is not the active set. Do not add to it and do not "clean it up" inside an unrelated change.
- `dotnet ef migrations add` has produced wrong `Up`/`Down` bodies in this repo. Always read the generated migration file before applying. See `efcore-migration-review`.

## Production Boundary

`scripts/ship-all.ps1`, `scripts/deploy-remote.ps1`, `scripts/server-*.sh`, and any write against `http://103.226.248.6` change production. Never run them to "verify" something. Hand release work to `release-deploy-gate` and let the owner run the command — the user ships themselves.

`scripts/check-prod-pipeline.ps1` is read-only by default (it only adds writes with `-RunDaily`); still ask before pointing anything at prod.

## Quality Gate

Do not report "builds" or "tests pass" without the command output for the project that owns the changed code. Do not claim a runtime behavior was verified if the API was never restarted after the edit. Do not touch production to answer a development question.
