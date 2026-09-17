---
name: frontend-web-dev
description: Implement or review frontend web UI changes with design-system fit, responsive layout, state handling, validation, and API integration. Use for the React/TypeScript/Vite JUICE web client — components, styles, SignalR realtime, and backend API calls.
---

# Frontend Web Dev

## Start

Inspect existing UI conventions, component structure, routes, scripts, styles, and API integration before editing. This is a React + TypeScript + Vite SPA (`frontend/src/`), **not** Razor or MVC. LeptonX, Blazor, and Angular patterns do not apply.

Use `sr-build-run-test` to start the dev server (`frontend/run-dev.ps1` or `npm run dev` from `frontend/`) and see the change against the real API. Use `ui-ux-review` first for redesign-scale work.

## Stack Facts (verified 2026-08-18)

- `@microsoft/signalr` connects to `MarketHub` for realtime pushes.
- Icons: `lucide-react`. Utilities: `clsx`.
- Build: `tsc -b && vite build`. Dev: `http://localhost:5173`.
- API base: `http://localhost:5280/api/v1` (dev), `http://103.226.248.6/api/v1` (prod). Never hardcode the prod URL in committed source.

## Workflow

1. Identify the user workflow, entry point, loading state, empty state, error state, and permission state.
2. Reuse existing components, CSS conventions, icons, and API helpers before creating new ones.
3. Keep controls predictable: buttons for commands, inputs/selects/toggles for settings, tables and lists for operational data.
4. Verify responsive behavior and text overflow for the viewports the app targets.
5. Handle API errors, race conditions, disabled states, and optimistic updates deliberately.
6. The realtime channel is SignalR. If a change touches score, Top, or VIP pushes, read `backend/StockRadar.Api/Hubs/MarketHub.cs` and `SignalRMarketRealtimePublisher.cs` first — the shape pushed from the server is the contract.
7. Verify the change against the running page; if not possible, state exactly what was not verified.

## Quality Gate

Do not introduce a new visual language for one feature. Do not hide loading, empty, error, or permission states. Do not hardcode production URLs or environment-specific values in committed source. Do not rename a SignalR message field without treating it as an API contract change — see `api-contract-review`.
