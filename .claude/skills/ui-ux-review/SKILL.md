---
name: ui-ux-review
description: Review UI/UX quality before frontend implementation or redesign. Use for UI audits, usability review, visual hierarchy, information architecture, responsive/accessibility checks, design-system consistency, screen redesign proposals, before/after critique, and prioritizing frontend improvement work before using frontend-web-dev.
---

# UI UX Review

## Start

Inspect the current surface, routes, views/components, design tokens, screenshots, user workflow, and API/state behavior. Use `browser-qa-execution` when a running surface is available. Do not redesign from taste alone.

## Review Workflow

1. Identify users, jobs-to-be-done, entry points, frequency, and success signals.
2. Map information hierarchy and the full interaction-state matrix: loading, empty, populated, validation, permission, error, recovery, disabled, and destructive confirmation.
3. Review the user journey, defaults, feedback, cognitive load, keyboard flow, and error prevention.
4. Review visual hierarchy, density, alignment, contrast, grouping, scanability, consistency, and component reuse.
5. Review responsive and accessibility behavior: focus, labels, semantics, contrast, overflow, touch targets, and viewport changes.
6. Score only dimensions supported by evidence from `1-10`; state what a `10` would require and avoid averaging away a blocking defect.
7. For a material redesign, offer two to four distinct directions with tradeoffs before implementation. For incremental work, prefer one evidence-backed recommendation.
8. When implementation exists, capture before/after evidence and rerun the affected journey.
9. Hand approved changes and state-level acceptance checks to `frontend-web-dev`.

## Output

Return evidence checked, dimension scores, blocking state gaps, prioritized findings, quick wins, redesign options when appropriate, responsive/accessibility issues, and implementation handoff.

## Rules

- Prefer incremental improvements unless redesign is requested.
- Keep operational tools dense but readable.
- Do not hide status, permission, loading, empty, or error states.
- Preserve localization, accessibility, and existing component conventions.
- Do not claim a screen or improvement exists without source or visual evidence.

## Quality Gate

Do not approve UI work when the main journey, interaction states, responsive behavior, accessibility, or design-system fit is unknown. A high score requires evidence, not optimism.
