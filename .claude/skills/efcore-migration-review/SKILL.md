---
name: efcore-migration-review
description: Review or plan Entity Framework Core migrations and schema changes. Use for EF migrations, DbContext/model changes, data preservation, indexes, constraints, seed data, rollback, deployment ordering, and database compatibility review.
---

# EF Core Migration Review

## Start

Read the entity/model changes, `ApplicationDbContext` configuration, the generated migration, seed data, and target database assumptions before editing or approving.

Repo facts (`backend/StockRadar.Infrastructure/`):

- Live migration set: `Migrations/` — namespace `StockRadar.Infrastructure.Migrations`, owns `ApplicationDbContextModelSnapshot.cs`.
- `Persistence/Migrations/` is a stray set with no snapshot. Not active. Do not add to it and do not clean it up inside an unrelated change.
- Context: `Persistence/ApplicationDbContext.cs`. There is no separate migrator host.

## Known Trap — read the generated file

`dotnet ef migrations add` has produced wrong `Up`/`Down` bodies in this repo (empty or mismatched against the model change). Always open the generated `.cs` and verify it matches the intended change before applying it anywhere. If it is wrong, hand-correct the body or regenerate — never apply unread.

## Checklist

- Migration matches the intended model change and contains no unrelated churn.
- Generated `Up`/`Down` bodies were read and match the model diff (see trap above).
- It landed in the live `Migrations/` folder with the correct namespace.
- Destructive operations are explicit and owner-approved.
- Data backfill, default values, nullability transitions, and constraint timing are safe.
- Indexes, uniqueness, foreign keys, cascade behavior, and performance impact are reviewed — check any column read on a hot pipeline path.
- Large JSON columns such as `Stocks.HistoryJson` are not widened, re-typed, or rewritten row-by-row without a size and throughput estimate.
- Deployment order is clear for app code versus database schema; local and production schema will not diverge.
- Rollback limits are documented; not all migrations are safely reversible.
- Tests or dry-run checks exist for critical data paths. `backend/StockRadar.Tests` uses EF Core InMemory, which will not catch SQL Server specific DDL issues — say so instead of implying coverage.

## Output

Return:

- Migration summary.
- High-risk operations.
- Required data/backfill steps.
- Rollout/rollback notes.
- Test or dry-run commands.

## Quality Gate

Block approval for an unread generated migration, unreviewed drops, nullable-to-non-nullable changes without a backfill or default plan, broad seed churn, or migrations that cannot be deployed with the application rollout. Applying a migration to production is an owner action — never run it to check something.
