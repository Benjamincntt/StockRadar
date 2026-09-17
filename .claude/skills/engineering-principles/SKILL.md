---
name: engineering-principles
description: Apply compact engineering guardrails before and during coding. Use for any implementation, refactor, bug fix, or risky edit where the agent must think before coding, keep changes simple and surgical, tie every edit to success criteria, and verify results without over-engineering.
---

# Engineering Principles

## Purpose

Keep agents from over-building, drifting from the request, or touching unrelated code. Use this as a lightweight discipline skill, not a replacement for domain-specific skills.

## Guardrails

1. Define success criteria, source evidence, and verification before editing.
2. Read the smallest source area that can answer the question.
3. Prefer the simplest change that satisfies the criteria.
4. Keep edits surgical and trace every changed file to the task.
5. Preserve existing patterns unless there is a concrete reason to change them.
6. Avoid speculative abstractions, broad rewrites, and cleanup outside scope.
7. Complete the approved slice: cover its error paths, tests, docs, and operational needs without expanding into unrelated work.
8. Verify with the narrowest meaningful check, then broaden only when risk warrants.
9. State assumptions, skipped checks, and residual risk.
10. Record material decisions and failed approaches in the current workstream or `context-handoff`; do not make the user rediscover them.

## Stop Conditions

Pause for owner decision when the change requires a product tradeoff, scope expansion, breaking API behavior, destructive data migration, security exception, production secret/config decision, or release go/no-go.

## Closeout

Report:

- Success criteria met.
- Files changed and why.
- Checks run.
- Risks left.
- Follow-up decisions.
