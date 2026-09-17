---
name: test-automation-dotnet
description: Add, update, or review .NET automated tests. Use for xUnit, EF Core InMemory, unit tests, integration tests, regression tests, and targeted dotnet test execution in StockRadar.Tests.
---

# Test Automation Dotnet

## Start

There is one test project: `backend/StockRadar.Tests` (xUnit, `net10.0`, `Microsoft.EntityFrameworkCore.InMemory`). It covers domain logic and application services. There are no separate API or E2E test projects. Verify this before running anything.

Test suites by folder:

| Folder | Covers |
|--------|--------|
| `MarketPhase` | `MarketPhaseClassifier` |
| `Playbook` | bundle gates, classifier, outcomes, regression |
| `RealizedPnl` | aggregate math, service |
| `ReversalBounce` | analyzer, regime classifier, fill simulator, shadow evaluator |
| `SectorWave` | RSI divergence, sector waves |
| `SellExit` | anchor window, blue-sky stop, Darvas regression |
| `VipAlerts` | VIP alert rules |

## Workflow

1. Map the change or bug risk to test cases. Prefer the narrowest test that catches the risk.
2. Choose unit for pure domain logic, InMemory-backed for service behavior. The InMemory provider does not catch SQL Server DDL issues — say so instead of implying coverage.
3. For a bug fix, write the regression test first and confirm it fails for the same reason as the bug, then make it pass. Name it so the bug is traceable.
4. Reuse `OhlcvFixtures` and existing base helpers. Follow the naming, arrangement (Arrange / Act / Assert), and file-folder conventions in the surrounding tests.
5. Keep tests deterministic: fix clock, symbol, and input data in the test itself — do not depend on system state.
6. Assert behavior and outcomes, not internal implementation details unless the implementation is the contract.
7. Run targeted `dotnet test` for the affected folder and paste the result summary; broaden only when risk warrants.

```powershell
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter "FullyQualifiedName~ReversalBounce"
dotnet test backend/StockRadar.Tests/StockRadar.Tests.csproj --filter "FullyQualifiedName~BuyDecision"
```

## Output

Report:

- Tests added or updated, with names and what each one guards.
- For bug fixes: evidence the regression test failed before the fix.
- Exact commands run and their result summary (pass and fail counts).
- Failures and how they were resolved.
- Remaining coverage gaps.

## Quality Gate

Do not mark work done if the feature or bug fix has no automated coverage and no stated explicit manual repro. Do not report tests as passing without the command output. Do not accept a bug fix without a regression test unless a specific owner-reviewable reason is stated.
