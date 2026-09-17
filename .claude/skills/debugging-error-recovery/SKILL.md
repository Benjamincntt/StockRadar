---
name: debugging-error-recovery
description: Diagnose and fix bugs, failing tests, build errors, runtime errors, flaky behavior, and agent mistakes. Use when something fails, when implementation does not work, or when the agent needs a disciplined reproduce-localize-fix-guard loop.
---

# Debugging Error Recovery

## Start

A fix that was never reproduced is a hypothesis, not a fix. Before touching code, use `sr-build-run-test` to select the affected project, host, port, test filter, and pipeline script.

If the user only asked what is wrong, stop after root cause plus options. Reproducing and reading code is allowed; editing is not, until they confirm. See `CLAUDE.md` section "Hỏi trước" and `.specify/memory/bug-fix-constitution.md`.

## Workflow

1. Capture the exact symptom: command, error/log, input, symbol/date context, environment (local vs prod), expected behavior, and timestamp.
2. Reproduce before changing code using the smallest deterministic scenario. If reproduction is impossible, report a hypothesis, not a fix.
3. Localize through the failing path, recent diff/history, callers, tests, config, and minimal instrumentation.
4. State and test one hypothesis at a time; record why each disproved approach failed.
5. After three materially different failed hypotheses, stop, summarize evidence with `context-handoff`, and request the missing input or change of strategy instead of continuing random fixes.
6. Explain the root cause: why the code was wrong. Label symptom suppression as mitigation.
7. Fix the root cause with the smallest coherent change and search for the same defect class elsewhere.
8. Add a regression test that fails without the fix and passes with it, or state a specific waiver and replacement repro.
9. Re-run the original repro, then adjacent high-risk checks. Backend fixes need `backend/restart-api.ps1` before any runtime re-check.

## StockRadar Specifics

- A score/gate symptom (Top trống, điểm vào sai, Buy Score mâu thuẫn) is usually a threshold or phase-mapping bug, not a display bug. Read the owning engine first: `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, `SignalAnalyzer`, `DarvasBreakoutAnalyzer`, `DailyAnalysisRunner`.
- If the fix would change published gate/score semantics, stop and escalate to Spec Kit — that is no longer a surgical bug fix (`.specify/memory/constitution.md`, Nguyên tắc IV and V).
- `MarketWyckoffPhase` and ReversalBounce `MarketRegime` are parallel systems. Do not merge or silently overwrite one with the other.
- Regression tests belong in the matching folder under `backend/StockRadar.Tests`: `MarketPhase`, `Playbook`, `RealizedPnl`, `ReversalBounce`, `SectorWave`, `SellExit`, `VipAlerts`.
- Prefer `docs/domain/*` to read intended behavior, but trust code on disk when they disagree.

## Anti-Patterns

- Random patching, weakened assertions, deleted tests, swallowed exceptions, or broad try/catch.
- Claiming a root cause without source evidence.
- Fixing one copied occurrence while ignoring the defect class.
- Switching tools or approaches without recording why.
- Calling sandbox/network/dependency failures without checking the actual error.
- Loosening a threshold so the symptom disappears.

## Output

Return symptom and exact repro, failing evidence, hypotheses tested, root cause, fix versus mitigation, similar occurrences, regression evidence, passing repro, commands run, and remaining risk.

## Quality Gate

Report `fixed` only when the failure was reproduced before, the same scenario passes after, the root cause is explained, and a regression test or explicit owner-reviewable waiver exists. Otherwise report `not verified`.
