---
name: observability-instrumentation
description: Design or review logs, metrics, tracing, correlation IDs, audit events, dashboards, alerts, release baselines, and canary diagnostics. Use for async workflows, queues, SignalR, integrations, background jobs, incident prevention, and post-deploy verification.
---

# Observability Instrumentation

## Purpose

Make features diagnosable before production and make releases measurable after deployment.

## Checklist

- Define the incident and canary questions operators must answer.
- Correlate inbound request, command/event, queue/job, outbound call, database effect, and user-visible result.
- Log lifecycle events with stable IDs and safe tenant/user context; never secrets or excessive PII.
- Measure volume, latency percentiles, failures, retries, dead letters, queue age, saturation, and external dependency results.
- Add audit events for security/compliance actions.
- Define actionable alerts with owner, threshold, evaluation window, and runbook.
- For releases, record pre-deploy baseline, expected change, monitoring window, stop/rollback trigger, and comparison query/dashboard.
- Verify telemetry itself with a test or smoke check; a planned log that was never observed is unverified.

## Output

Return operator questions, signals, correlation fields, dashboards/queries, alert candidates, data-safety notes, verification checks, and release baseline/canary thresholds.

## Quality Gate

Do not approve async/external work that cannot answer what happened, for whom, where it failed, whether retry occurs, and what action is needed. Do not approve a release-critical canary without a measurable baseline and rollback trigger.
