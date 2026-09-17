---
name: doubt-driven-review
description: Run an adversarial review for risky plans, architecture, security, data, release, or implementation decisions. Use when a proposal feels too easy, stakes are high, evidence is thin, or the agent should challenge assumptions before recommending a path.
---

# Doubt Driven Review

## Purpose

Prevent confident but weak recommendations. Use this for high-risk decisions, not every small task.

## Workflow

1. State the claim or recommendation being reviewed.
2. Extract the evidence that supports it, with source paths or docs.
3. List assumptions and what would make the claim false.
4. Challenge the plan from PO, SA, DB, Security, QA, Release, and Ops angles as relevant.
5. Look for cheaper, safer, or more reversible alternatives.
6. Reconcile: keep, change, defer, or reject the recommendation.
7. Identify owner decisions and required verification before implementation.

## Output

```markdown
| Claim | Evidence | Doubt / Failure Mode | Mitigation | Decision |
| --- | --- | --- | --- | --- |
```

## Quality Gate

Do not rubber-stamp. If evidence is missing, say exactly what must be verified. If the recommendation still stands, explain why the doubts are acceptable.
