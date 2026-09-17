---
name: integration-event-design
description: Design or review async flows — SignalR realtime pushes, scheduled pipeline jobs, external market-data and LLM calls, Telegram alerts — covering idempotency, retries, ordering, failure handling, and observability. Use for background workers, realtime publishing, and outbound integrations.
---

# Integration Event Design

## Start

Verify existing patterns in source before designing: `backend/StockRadar.Api/Hubs/MarketHub.cs`, `backend/StockRadar.Api/Realtime/SignalRMarketRealtimePublisher.cs`, `backend/StockRadar.Application/Jobs/JobCatalog.cs`, `DailyAnalysisRunner`, and `IJobStatusService`. Use current code over diagrams or docs. Use `observability-instrumentation` for logs, metrics, correlation, and alerting.

This repo has no message broker. Async work is scheduled jobs plus SignalR fan-out plus outbound HTTP. Do not introduce a queue or broker without an approved spec — that is an architecture change (constitution, Nguyên tắc II).

## Design Checklist

- Producer, consumer, ownership boundary, and trigger are clear.
- Payload has a stable schema and a version strategy. SignalR payloads are a client contract — see `api-contract-review` for mobile and web impact.
- Idempotency is defined. Pipeline jobs re-run: a re-run must not double-append bars, double-count realized P&L, or resend an alert already sent.
- Ordering and overlap: what happens when the previous run has not finished, or when the intraday refresh overlaps the daily analysis.
- Retry, timeout, and failure behavior for each external dependency — market data source, the LLM veto call, Telegram delivery. A degraded dependency must fail the step loudly, not silently emit an empty result that reads as "no opportunity".
- Partial failure: a job that processed 400 of 1600 symbols reports partial, not success.
- Transaction boundary is explicit; a scan that writes analysis rows and then alerts must not alert on rows that were rolled back.
- Observability includes logs, run duration, per-symbol failures, and an alert on a missed scheduled window.
- Rollout and rollback are planned.

## Output

Return:

- Flow summary.
- Payload contract.
- Producer and consumer ownership.
- Failure, retry, and idempotency behavior.
- Test plan.
- Rollout and rollback notes.

## Quality Gate

Do not approve an async design without idempotency, failure handling, and observability. Treat fire and forget as a risk unless the loss is acceptable and documented. Do not approve a change that lets a failed dependency look like a legitimate empty result.
