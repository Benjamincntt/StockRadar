---
name: sql-data-review
description: Review SQL scripts, data fixes, data migrations, reporting queries, and production data operations. Use for raw SQL, stored procedures, ad-hoc fixes, row updates/deletes, backfills, validation queries, and database safety checks.
---

# SQL Data Review

## Start

Treat data operations as high risk. Verify table ownership, filters, environment target, backup/restore plan, and expected row counts before recommending execution.

Repo facts: local DB `StockRadarDb` on SQL Server, reached with `sqlcmd`. Helpers live in `scripts/db/`, `scripts/db-config.ps1`, `scripts/export-db.ps1`. Backfill and purge scripts already exist (`scripts/run-backfill*.ps1`, `scripts/purge-setup-tracks-before-date.*`, `scripts/criteria-backfill-only.sh`, `scripts/server-reset-stockradar-db.sh`) — prefer extending a reviewed script over inventing a new ad-hoc statement.

## Checklist

- The script states its environment and database target explicitly; it is never assumed.
- Reads validate target rows before writes: a `SELECT COUNT(*)` with the same `WHERE` precedes every `UPDATE` or `DELETE`.
- Writes are scoped by stable keys (`Symbol`, date, id) — never by a computed or ranked predicate alone.
- Transaction, lock, timeout, and batching strategy are appropriate. Batch anything touching the full `Stocks` universe.
- `OPENJSON` and `JSON_VALUE` work over `HistoryJson` is bounded: no per-row query loops, and no full-universe JSON parse where a persisted column (`LastClose`, `LastVolume`, `LastChangePercent`) already answers the question. Measure with `performance-review`.
- Backup, rollback, or a compensating script is defined before the first write.
- A dry-run or count query is included.
- Credentials are not copied into docs, logs, or committed scripts.

## Production Rule

The production database behind `http://103.226.248.6` is owner-only. Read-only inspection needs a stated reason; any `INSERT`, `UPDATE`, `DELETE`, `DROP`, `TRUNCATE`, or `ALTER` against it is an owner action executed by the owner. Hand over the exact script plus pre and post-check queries; do not run it.

## Output

Return:

- Risk classification.
- Pre-check queries.
- Execution plan.
- Rollback or compensation.
- Post-check queries.
- Owner approvals needed.

## Quality Gate

Do not approve a script with broad `UPDATE` or `DELETE` behavior, unbounded JSON scans, unknown row counts, an unstated environment, or no rollback and validation plan.
