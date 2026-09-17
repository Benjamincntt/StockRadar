---
name: browser-qa-execution
description: Execute evidence-based browser QA against the JUICE web client or the API Swagger UI. Use for live user-journey validation, screenshots, responsive checks, console or network failures, and release smoke tests.
---

# Browser QA Execution

## Start

Read the acceptance criteria or test plan, confirm the target environment, and use `sr-build-run-test` to bring up the right surface. Default to report-only. Editing code, committing, pushing, or touching production requires separate explicit authorization.

Environments:

- Local web: `http://localhost:5173` (React dev) against API `http://localhost:5280`.
- Local Swagger: `http://localhost:5280/swagger`.
- Production: `http://103.226.248.6/api/v1`. Read-only smoke only; never submit a write operation here as a verification step.

## Workflow

1. Record URL, environment, viewport, test-data strategy, and expected journey.
2. Confirm the environment is reachable. Never print, export, persist, or copy session token values.
3. Execute smoke first, then the scoped journey. Cover loading, empty, validation, error, and responsive states that apply.
4. Capture evidence: exact step, expected versus actual, screenshot when useful, console error, failed request and status, correlation ID or job ID, and timestamp.
5. Re-check a suspected failure once to separate a deterministic defect from flakiness. Do not loop on the same blocked step.
6. In fix mode, use `debugging-error-recovery`, add a regression test when practical, and rerun the original browser repro.
7. Hand release-critical signals to `release-deploy-gate` and `observability-instrumentation`.

## Output

```markdown
| ID | Severity | Journey / Step | Expected | Actual | Evidence | Reproducible? |
| --- | --- | --- | --- | --- | --- | --- |
```

Also report environment, coverage executed, blocked scenarios, and console or network summary.

## Quality Gate

Do not call a journey passed without executing it. Do not call an environment issue a product defect without evidence. Never widen browser access or change production state implicitly.
