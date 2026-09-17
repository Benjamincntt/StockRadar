---
name: context-handoff
description: Save or restore concise task context across sessions. Use when pausing work, resuming after context loss, or recording decisions, evidence, failed approaches, current Git state, remaining tasks, and blockers.
---

# Context Handoff

## Save

1. Verify the current objective, scope, branch state, and source evidence.
2. Capture decisions, assumptions, changed files, commands and results, failed approaches, remaining tasks, risks, and the exact next action.
3. Keep durable product and architecture decisions in the relevant governed doc under `docs/domain/*` or `specs/*`. Use a handoff only for local execution state.
4. Redact credentials, connection strings, and large raw logs. Reference safe file paths or command names instead.

## Restore

1. Read the handoff, then re-check Git state and referenced files — the workspace may have changed.
2. Separate still-valid facts from stale assumptions.
3. Re-run the narrowest critical check before continuing risky work.
4. Resume from the stated next action; do not repeat completed work unless evidence is missing.

## Handoff Shape

```json
{
  "objective": "",
  "scope": [],
  "decisions": [],
  "changed_files": [],
  "checks": [],
  "failed_approaches": [],
  "remaining": [],
  "risks": [],
  "next_action": ""
}
```

## Quality Gate

Do not treat a handoff as source truth after the repository changes. Do not store credentials. Do not create a second Markdown plan or summary for a governed workstream — update the living doc instead.
