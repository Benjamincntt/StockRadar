---
name: api-contract-review
description: Review or design API contracts for correctness, compatibility, validation, error behavior, versioning, pagination, idempotency, Swagger fit, client impact, and tests. Use before or after controller, endpoint, or response DTO changes.
---

# API Contract Review

## Start

Find the current controller and route definitions and the contract DTOs in source under `backend/StockRadar.Api/` and `backend/StockRadar.Application/`. Do not trust documentation until source and tests are checked.

## Client Impact — read this first

`http://103.226.248.6/api/v1` has two shipped clients that are not deployed together with the API:

- `mobile/` — Flutter app `juice_app`, released by version (`0.1.1+2`). Users keep old builds. A removed or renamed field is a crash or a blank screen on a device you cannot update.
- `frontend/` — React + Vite web client, plus a `@microsoft/signalr` realtime channel.

So a field rename is a breaking change even when nothing in this repo fails to compile. Additive-only is the default; anything else needs an explicit migration note and a mobile version floor.

## Checklist

- Route, HTTP method, `v1` versioning, and naming are stable and intentional.
- Response DTOs are backward compatible, or the migration impact is documented with which mobile build is affected.
- Validation, nullability, required fields, enums, and default values are explicit.
- Field names match the UI labels and the domain vocabulary in `docs/domain/*` (for example `flatBox`, not the retired `basePrice` card semantics).
- Error codes and response shape are consistent with the existing API style.
- Pagination, sorting, filtering, idempotency, and concurrency are handled where needed.
- Swagger output at `/swagger` still describes the real shape.
- Any new gate, score, or phase field exposed to clients is reflected in the matching `docs/domain/*` living doc in the same change set (constitution, Nguyên tắc IV).
- Tests cover success, validation failure, not found, and edge cases.

## Output

Return findings first:

```markdown
| Severity | Endpoint / Contract | Issue | Source Evidence | Recommendation |
| --- | --- | --- | --- | --- |
```

Then summarize compatibility risk, affected client builds, test gaps, and owner decisions.

## Quality Gate

Block approval for a breaking contract change without a migration plan and a stated mobile impact, for routes documented differently from source, or for a new scoring field that no living doc explains.
