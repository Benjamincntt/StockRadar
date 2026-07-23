# Data Model: 002-confirmed-market-uptrend

Không thêm bảng SQL bắt buộc. Mô hình logic Domain + persist hiện có.

## Entities / Value Objects

### IndexBarSeries (logical)

| Field | Type | Notes |
|-------|------|-------|
| Bars | list OHLCV (Date, O,H,L,C, Volume) | Từ `MarketIndices.HistoryJson` |
| Symbol | string | `VNINDEX` |

**Validation**: Favorable yêu cầu đủ ≥ 20 phiên (MA20); Higher Low lookback tới 60; thiếu dữ liệu → không Favorable (Unfavorable hoặc Neutral an toàn — research: Close thiếu → Unfavorable nếu không tính được MA20).

### MarketPhaseClassification (result VO)

| Field | Type | Notes |
|-------|------|-------|
| Phase | Favorable \| Neutral \| Unfavorable | Map Attempted=Neutral, Correction=Unfavorable |
| CloseAboveMa20 | bool | |
| Ma20SlopeNonNegative | bool | |
| HasFollowThroughDay | bool | |
| FollowThroughDate | DateOnly? | Phiên FTD nếu có |
| HasHigherLow | bool | |
| RallyDayOne | DateOnly? | Ngày 1 đợt nỗ lực |

### MarketPhaseThresholds (settings)

| Field | Default | Source |
|-------|---------|--------|
| FtdMinGainPercent | 1.2 | Spec |
| FtdMinRallyDay | 4 | Spec |
| FtdMaxRallyDay | 7 | Spec |
| Ma20SlopeLookbackSessions | 3 | Assumptions |
| HigherLowLookbackSessions | 60 | Spec |
| HigherLowPivotRadius | 2 | Research |
| RallyLookbackSessions | 20 | Spec ngày 1 |

Có thể hard-code constants Domain ở MVP hoặc bind `SmartMoney:MarketPhase:*` trong Options.

### Persisted (unchanged schema)

| Store | Field | Behavior |
|-------|-------|----------|
| `DailyOpportunities.MarketPhase` | string | Ghi `Favorable`/`Neutral`/`Unfavorable` từ classifier mới |
| `DailyOpportunities.TradeStateReason` | string | Có thể là “Chờ xác nhận thị trường chung” |
| `MarketIndices.HistoryJson` | JSON OHLCV | Input — không đổi schema |

## State transitions (pha)

```text
                    Close < MA20
                 ┌──────────────────► Unfavorable (Correction)
                 │
  (mỗi phiên) ───┤  Close ≥ MA20 nhưng thiếu FTD hoặc HL hoặc slope
                 │──────────────────► Neutral (Attempted Rally)
                 │
                 │  Close > MA20 ∧ slope OK ∧ FTD ∧ Higher Low
                 └──────────────────► Favorable
```

Thủng lại dưới MA20 → Unfavorable ngay (edge spec).

## Relationships

- `SmartMoneyMarketContext.MarketPhase` ← Classification.Phase  
- `BuyDecisionEngine.ResolveMaStackStrictness(phase)` ← không đổi chữ ký; đổi **input phase**  
- ReversalBounce `MarketRegime` — **không** quan hệ

## Validation rules (from FR)

- Favorable ⇒ cả bốn điều kiện cứng (FR-003/004)  
- Không Favorable chỉ vì ChangePercent phiên (FR-002)  
- Phase Neutral + lý do cổng kiểu MA trên list ⇒ reason UX chờ xác nhận TT (FR-008)
