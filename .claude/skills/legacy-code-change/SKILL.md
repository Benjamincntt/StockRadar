---
name: legacy-code-change
description: Make safe changes in legacy, fragile, or poorly documented code. Use when code has unclear ownership, low tests, old patterns, high coupling, duplicated logic, stale docs, or risky behavior changes.
---

# Legacy Code Change

## Start

Assume existing behavior may be relied on even if it looks odd. Use `engineering-principles` first. Gather source evidence and tests before editing. Use `debugging-error-recovery` if the change starts from a failing behavior or test.

## Workflow

1. Characterize current behavior with source paths, callers, config, data dependencies, and tests.
2. Identify the narrowest change that satisfies the request.
3. Add characterization tests or targeted assertions before a risky edit; if genuinely not possible, state the specific reason and what manual check replaces them.
4. Preserve public contracts unless the owner approves a breaking change.
5. Avoid broad cleanup, style churn, and opportunistic rewrites.
6. Add comments only for non-obvious legacy constraints.
7. Run targeted tests and include the output summary, or explain specifically why they cannot be run.

## Quality Gate

If behavior is ambiguous, present the assumption and evidence. Do not "modernize" fragile code as part of a feature unless modernization is required to deliver safely.
