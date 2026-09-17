---
name: secrets-config-review
description: Review secrets, runtime configuration, environment variables, appsettings, connection strings, feature flags, certificates, tokens, and deployment config. Use when changing config files, launch profiles, CI/CD secrets, integrations, or environment-specific settings.
---

# Secrets Config Review

## Start

Treat committed config as non-secret unless the repo owner says otherwise. Verify runtime precedence: environment variables, secret stores, deployment variables, appsettings, and local overrides.

## Checklist

- No credentials, tokens, connection strings, certificates, or private keys are added to docs or source.
- New config keys have safe defaults and are documented in the correct active doc.
- Environment-specific values are not hardcoded.
- Feature flags have owner, default, rollout, rollback, and cleanup plan.
- Logs and errors do not expose secret values.
- CI/CD variables and local launch settings are separated.
- Rotation is planned when a secret has been exposed.

## Output

Return:

- Config keys changed.
- Runtime precedence.
- Secret exposure risk.
- Required environment updates.
- Rollout/rollback notes.

## Quality Gate

Block approval for committed secrets, undocumented required config, or feature flags without rollback/cleanup expectations.
