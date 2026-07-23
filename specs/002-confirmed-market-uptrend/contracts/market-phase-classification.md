# Contract: Phân loại pha thị trường tăng trưởng & nhãn Top

**Feature**: 002-confirmed-market-uptrend  
**Audience**: Backend Domain/Infra, mobile Home (đọc reason), docs living

## 1. Phase semantics (growth only)

| Business name | Stored / enum | DNA / UI label (VI) | MA strictness |
|---------------|---------------|---------------------|---------------|
| Correction | `Unfavorable` | Điều chỉnh / TT khó (giữ copy Unfavorable hiện có nếu đã có) | Loose |
| Attempted Rally | `Neutral` | **Nỗ lực hồi phục** | Medium |
| Confirmed Uptrend | `Favorable` | **TT thuận** | Full |

**MUST NOT** equate these with ReversalBounce `MarketRegime` values.

## 2. Favorable predicate (normative)

All MUST be true:

1. `Index.Close > MA20(Close)`
2. `MA20` slope non-negative (MA20[t] ≥ MA20[t−3])
3. Valid **Follow-Through Day** in rally window days 4–7:
   - Gain ≥ 1.2% vs prior close
   - Volume > prior session volume
   - Volume > average volume of 20 sessions preceding FTD
4. At least one **Higher Low** (pivot lows, radius 2) in last 60 sessions

Otherwise MUST NOT be `Favorable`.

If `Close < MA20` → MUST be `Unfavorable`.  
Else if not Favorable → MUST be `Neutral`.

## 3. Trade state reason (Top list)

| Condition | `TradeStateReason` / gate display |
|-----------|-----------------------------------|
| Phase ≠ Favorable AND underlying gate was MA-stack failure | **`Chờ xác nhận thị trường chung`** |
| Phase = Favorable AND MA-stack failure | Keep **`Chưa đạt MA stack / xu hướng dài hạn`** (or equivalent) |
| Other gates (FOMO, phân phối, …) | Unchanged |

## 4. API surface

No new HTTP route required.

Existing fields that MUST reflect new classifier after analysis job:

- `DailyOpportunity` / opportunities DTO: `marketPhase`, `tradeState`, `tradeStateReason`, setup DNA phase segment
- Stock detail buy decision: `gateFailure` / `tradeStateReason` consistent with §3 when snapshot-aligned

Optional (non-blocking): log line on analysis: phase + flags AboveMa20 / HasFtd / HasHigherLow.

## 5. Config (optional)

```json
"SmartMoney": {
  "MarketPhase": {
    "FtdMinGainPercent": 1.2,
    "FtdMinRallyDay": 4,
    "FtdMaxRallyDay": 7,
    "HigherLowLookbackSessions": 60
  }
}
```

Defaults = table in data-model.md if section omitted.

## 6. Docs contract

Same change set MUST update:

- `docs/domain/ma-stack-and-market-phase.md` (as-is + G-MA-1 resolved)
- Cross-link from `docs/domain/buy-decision.md` if UX reason mentioned
