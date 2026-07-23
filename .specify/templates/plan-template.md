# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]

**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]

**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]

**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]

**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]

**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]

**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]

**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]

**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `.specify/memory/constitution.md` v1.0.0*

- [ ] **I. Code as truth**: Plan cites concrete entry files to read/change; does not treat CLAUDE.md/graph as runtime truth
- [ ] **II. Spec-first**: Material gate/score/pipeline/API/nav changes have `spec.md` (and clarify if needed) before this plan
- [ ] **III. Minimal surface**: Diff scope limited to approved intent; no drive-by refactors; bug-only work has Change Plan
- [ ] **IV. Domain gates**: Buy Score / Top / MA·phase / flatBox / ReversalBounce changes update living docs + this `specs/` set together; Wyckoff phase ≠ Reversal regime
- [ ] **V. Simplicity**: No new abstraction/dependency without Complexity Tracking row; focused file reads only
- [ ] **Stack**: Changes respect Domain vs Infra vs Api vs `mobile/` vs `frontend/` boundaries; restart/ship scripts noted if applicable

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Keep only the trees this feature touches. StockRadar default
  layout is below — delete unused branches. Delivered plan must not keep
  "Option" labels.
-->

```text
backend/
├── StockRadar.Api/              # Controllers, Program.cs
├── StockRadar.Application/      # Services, Options, DTOs
├── StockRadar.Domain/           # Engines (BuyDecision, Signal, Darvas, ReversalBounce, …)
├── StockRadar.Infrastructure/   # Runners, EF, market data
└── StockRadar.Tests/

mobile/lib/                      # Flutter: screens, widgets, core/api, core/navigation
frontend/src/                    # React: pages, components
docs/                            # Living + feature BA prep
scripts/                         # ship-all, deploy, tooling
```

**Structure Decision**: [Document which of the paths above this feature touches]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
