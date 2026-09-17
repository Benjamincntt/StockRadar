---
name: release-deploy-gate
description: Assess release and deployment readiness. Use for deployment checklists, migration ordering, smoke tests, rollback planning, and go/no-go recommendations before running ship-all or a production deploy.
---

# Release Deploy Gate

## Start

Read the release scope, diff, tests, migrations, config, and the deploy script. Use `doubt-driven-review` for risky decisions and `browser-qa-execution` for live smoke.

This skill assesses readiness. It does not authorize commit, push, deploy, migration, or production mutation. The user ships themselves: `.\scripts\ship-all.ps1 -Message "..."`.

## Repo Deploy Path

`scripts/ship-all.ps1` is the single entry point: optional commit, push, SSH deploy to `103.226.248.6`, then pipeline jobs. Options: `-SkipCommit`, `-SkipDeploy`, `-SkipJobs`, `-LocalOnly`, `-DeployAction (all|fe|be)`. Read the script header for the full flag list before recommending a command.

`scripts/check-prod-pipeline.ps1` is safe to run read-only (no `-RunDaily`); use it to confirm the live API is responding before closing a release.

`backend/restart-api.ps1` applies to the local dev instance only. Production restart is handled by the SSH deploy step inside `ship-all.ps1`.

## Gate Checklist

- Scope maps to delivered code and acceptance criteria.
- `dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj` passes with output shown.
- Backend changes were built and the API was restarted locally; the affected endpoint or job was verified.
- Migrations are reviewed (`efcore-migration-review`): generated body read, active migration set confirmed, deployment order clear.
- Config and secrets changes are reviewed (`secrets-config-review`): no credentials committed, production config updated if needed.
- API contract changes are backward compatible or mobile impact is documented (`api-contract-review`).
- Score, gate, or pipeline semantic changes went through Spec Kit and the relevant `docs/domain/*` is updated in the same change set.
- Rollback or compensating action is defined.
- Production smoke plan is ready (`check-prod-pipeline.ps1`, or specific endpoint curl commands).

## Readiness Dashboard

```markdown
| Gate | Required | Evidence | Status | Owner / Blocker |
| --- | --- | --- | --- | --- |
```

Use `Green`, `Yellow`, `Red`, or `Unknown`. Record owner-accepted risk explicitly.

## Output

Return dashboard, go/no-go recommendation, blockers, deployment order, migration and config plan, smoke checks, rollback trigger, and the exact `ship-all.ps1` command the owner should run.

## Quality Gate

Do not recommend `Go` with failing tests, an unread migration, unknown production config state, or an undocumented score or gate semantic change. A release review is not permission to deploy — hand the command to the owner.
