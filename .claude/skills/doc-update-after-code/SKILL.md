---
name: doc-update-after-code
description: Update governed documentation after code, config, schema, API, or pipeline changes. Use before finishing a change when docs must reflect verified behavior.
---

# Doc Update After Code

## Required Start

Determine what changed from the request, source, and `git diff`. Read only the docs needed for the task; verify claims against source before updating content.

## Governed Doc Locations

| Area | Living doc |
|------|-----------|
| Buy Score / Top / VIP / display | `docs/domain/buy-decision.md` |
| MA stack and market phase | `docs/domain/ma-stack-and-market-phase.md` |
| Base price / flatBox / Darvas | `docs/domain/base-price-flatbox.md` |
| ReversalBounce | `docs/domain/reversal-bounce.md` |
| Realized P&L | `docs/domain/realized-pnl.md` |
| Pipeline jobs | `docs/domain/pipeline-jobs.md` |
| LLM veto | `docs/features/vip-deepseek-veto/spec.md` |
| Indicator playbooks | `docs/features/indicator-playbooks/spec.md` |
| Sector wave and entry patterns | `docs/features/sector-wave-entry-patterns/spec.md` |
| Win-rate overhaul | `docs/features/win-rate-overhaul/spec.md` |

## Workflow

1. Identify changed APIs, services, config, migrations, scores, gates, scripts, and pipeline steps.
2. Find affected docs from the table above. Update the existing main document; create one only when no governed doc exists.
3. Verify every claim against source on disk — do not copy from test output or chat context.
4. For a gate, threshold, or phase-mapping change: the living doc update is mandatory and belongs in the same change set (constitution, Nguyên tắc IV). Do not finish the code change without it.
5. Check `docs/README.md` for stale paths, wrong gate values, or obsolete feature references.
6. Do not describe planned behavior as implemented without source evidence.
7. Do not create completion reports, implementation summaries, or duplicate feature docs.

## Update Rules

- Keep one main doc per feature or domain concept.
- Prefer a short current-behavior and source-evidence section over an implementation diary.
- State tests not run and unverified categories explicitly.

## Closeout

Summarize docs updated, claims verified against source, and any gaps deferred with a stated reason.
