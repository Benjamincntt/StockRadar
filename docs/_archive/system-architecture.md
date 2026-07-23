# StockRadar (JUICE) — Kiến trúc hệ thống toàn diện

> **Mục đích:** Review một lượt trước production — từ Job 1 đến mọi output (UI, alert, chấm điểm, đo hiệu quả, ML/AI).  
> **Nguồn sự thật:** code trên disk (`backend/StockRadar.*`, `frontend/`, `mobile/`).  
> **Cập nhật:** 2026-07-08.

---

## 1. Bản đồ một trang

```mermaid
flowchart TB
    subgraph EXT["Nguồn & Client"]
        KBS["KB Buddy API (KBS)\nlisting · history · bảng giá"]
        USER["User\nWeb React · Flutter APK"]
    end

    subgraph SCHED["Quartz Scheduler (VN timezone)"]
        J1["Job 1 — History Backfill\n(thủ công / startup)"]
        J2["Job 2 — Daily Session Sync\n5 phút trong phiên + cron 15:00"]
        DA["Daily Analysis\n11:30 + ~15:05"]
        KS["KBS Market Sync\n~60s"]
        IS["Intraday Scanner\n~60s"]
        OM["Opportunity Monitor\n~60s"]
        WR["Weekly Review\nT6 15:30"]
    end

    subgraph CORE["Backend .NET :5280"]
        DB[("SQL Server")]
        ENG["Domain Engines\nBuyDecision · SmartMoney · Signals\nDarvas · VSA · Criterion"]
        API["REST /api/v1"]
        HUB["SignalR /hubs/market"]
        TG["TelegramNotifier"]
    end

    subgraph OUT["Output cuối"]
        WEB["Web stock.baobiantea.com"]
        MOB["Mobile JUICE APK"]
        ALERTS["Alerts DB + UI"]
        PERF["North Star / Performance API"]
        ML["ML Ranker + HPO"]
    end

    KBS --> J1 & J2 & KS & IS & OM
    J1 --> DB
    J2 --> DB
    J2 --> DAR["DarvasBreakoutAlertPublisher"]
    DA --> ENG --> DB
    KS --> HUB
    IS --> DB
    OM --> VSA["TradeEventDetector"] --> HUB
    OM --> VIP["TopOpportunityVipAlertPublisher"] --> TG

    DB --> API
    ENG --> API
    API --> WEB & MOB
    HUB --> WEB & MOB
    DAR --> ALERTS & HUB
    VIP --> ALERTS & HUB & TG
    DA --> PERF
    WR --> PERF & ML
    USER --> API
```

**Monorepo:** `backend/` (.NET 10 API) · `frontend/` (Vite React) · `mobile/` (Flutter) · `scripts/` (deploy, HPO, train).

**Production:** API `http://103.226.248.6/api/v1` · Web `https://stock.baobiantea.com/` · Deploy `.\scripts\ship-all.ps1`.

---

## 2. Timeline phiên giao dịch (chu kỳ T → T+1)

```mermaid
gantt
    title Chu kỳ dữ liệu điển hình (ngày giao dịch T)
    dateFormat HH:mm
    axisFormat %H:%M

    section T (hôm nay)
    KBS sync 60s           :09:00, 6h
    Intraday Scanner       :09:00, 6h
    Opportunity Monitor    :09:00, 6h
    Job 2 append nến T     :15:00, 30m
    Daily Analysis         :15:05, 20m
    Criterion scoring T-1  :15:10, 15m
    Đo T+2.5 pending       :15:10, 10m

    section T+1 (mai)
    User xem Top list      :09:00, 6h
    Entry Ready + Master   :09:00, 6h
    Job 2 append T+1       :15:00, 30m
```

### Ví dụ cụ thể

| Thời điểm | Việc xảy ra | Output |
|-----------|-------------|--------|
| **T-1 đêm** | Job 1 xong (hoặc rescreen) | `Stocks.HistoryJson` đến hết T-1, universe active |
| **T 9:00–14:45** | Sync + Scanner + Monitor | Giá live, `SessionRadarHits`, `TradeEvent`, VIP Telegram |
| **T 15:00** | Job 2 | Nến ngày T merged vào history; Darvas breakout mới |
| **T 15:05** | Daily Analysis | `DailyOpportunities` cho **phiên T+1** |
| **T 15:05+** | Post-processing | Shadow variants, criterion snapshot, đo T+2.5 |
| **T+1 9:00** | Monitor Top | Entry Ready (T-1 actionable) + Master alerts (momentum) |

`ForTradingDate` ghi DB: `TradingCalendar.GetPostSessionAnalysisDate()` (cutoff 15:00 VN).  
UI hiển thị target: `GetActiveOpportunityDate()` (cutoff 15:10 VN).

---

## 3. Pipeline Job — chi tiết từng bước

```mermaid
flowchart TB
    subgraph JOB1["Job 1 — History Backfill (một lần)"]
        L1["KBS Listing + Sector"]
        H1["KBS History 2000→T-1"]
        U1["Lọc universe\nKL≥500K · giá≥8K · IPO"]
        RS["UniverseRescreenRunner\n(DB only)"]
        L1 --> H1 --> SJ["Stocks + HistoryJson"]
        SJ --> U1 --> RS
    end

    subgraph JOB2["Job 2 — Daily Session Sync"]
        B2["KBS bảng giá batch\nchỉ mã active Job 1"]
        M2["MarketSyncService\nmerge nến ngày T"]
        D2["DarvasBreakoutAlertPublisher\nsignal MỚI trong ngày"]
        B2 --> M2 --> DB2[("HistoryJson T")]
        M2 --> D2
    end

    subgraph ANALYSIS["Daily Analysis"]
        CTX["BuildContext\nVNINDEX · pha TT · adaptive"]
        SM["SmartMoneyOpportunitySelector\nstrict filter"]
        BD["BuyDecisionEngine\nBuy Score + Entry + gates"]
        RK["IOpportunityRanker\nML P(hit) T+2.5"]
        TOP["Top N + relaxed fallback"]
        SAVE["DailyOpportunities\n+ SetupTracks"]
        CTX --> SM --> BD --> RK --> TOP --> SAVE
    end

    subgraph POST["Post-processing (sau analysis)"]
        SH["ShadowAnalysisService\nvariant MinPassScore"]
        CR["DailyCriterionScoringRunner\nT-1 snapshot"]
        PF["OpportunityPerformanceRunner\nđo T+2.5"]
        SH --> CR --> PF
    end

    SJ --> JOB2
    DB2 --> ANALYSIS
    SAVE --> POST
```

### Bảng Job Quartz

| Job ID | Runner | Lịch mặc định | Input | Output chính |
|--------|--------|---------------|-------|--------------|
| `history-backfill` | `HistoryBackfillRunner` | Thủ công / `RunOnStartup` | KBS listing, history | `Stocks`, `HistoryJson`, universe |
| `daily-session-sync` | `DailySessionSyncRunner` | **5 phút** trong phiên + cron 15:00 | KBS board active | Nến T; Darvas alerts |
| `daily-analysis` | `DailyAnalysisRunner` | **11:30** + **15:05** VN T2–T6 | DB universe | `DailyOpportunities`, `SetupTracks` |
| `kbs-market-sync` | `KbsMarketSyncRunner` | **60s** (nếu `AutoSyncEnabled`) | KBS board | `QuoteTickCache`, SignalR quotes |
| `intraday-scanner` | `IntradayScannerRunner` | **60s** | KBS board | `SessionRadarHits` |
| `opportunity-monitor` | `OpportunityIntradayMonitorRunner` | **60s** | KBS board + Top map | `TradeEvent`, VIP Telegram |
| `weekly-opportunity-review` | `WeeklyOpportunityReviewJob` | **T6 15:30** VN | SetupTracks đo xong | Weekly review, ML retrain, HPO |

### API trigger (header `X-Sync-Key`)

| Endpoint | Tương đương |
|----------|-------------|
| `POST /api/v1/market/jobs/history` | Job 1 |
| `POST /api/v1/market/jobs/session` | Job 2 |
| `POST /api/v1/market/jobs/analysis` | Phân tích full + post-processing |
| `POST /api/v1/market/jobs/daily` | Job 2 + Analysis |
| `POST /api/v1/market/jobs/opportunity-monitor` | 1 vòng Monitor |
| `POST /api/v1/opportunities/run-analysis` | Analysis UI (bỏ shadow nặng, cooldown 15p) |

---

## 4. Engine chấm điểm & quyết định mua

```mermaid
flowchart LR
    STOCK["Stock + HistoryJson"]
    CTX["SmartMoneyMarketContext\nPha TT · ngành · adaptive · calibration"]

    STOCK --> SA["SignalAnalyzer"]
    CTX --> SA
    SA --> SIG["Signals\nBreakout · DarvasBreakout\nVolumeSpike · Shakeout…"]
    SA --> FB["FlatBoxProfile / Darvas"]

    STOCK --> BDE["BuyDecisionEngine"]
    CTX --> BDE
    SA --> BDE

    BDE --> SCORE["Buy Score 0–100\n9 nhóm điểm"]
    BDE --> ENTRY["EntryPoint\nReady/Watch/Late/Invalid"]
    BDE --> GATE["Top gates\nFOMO · PP · MA · breakout…"]
    BDE --> TS["TradeState\nStrongBuy/Watch/Avoid…"]

    STOCK --> SMS["SmartMoneyOpportunitySelector"]
    BDE --> SMS
    SMS --> PASS{"PassesFilter?\nscore ≥ MinPassScore"}
    PASS -->|yes| STRICT["Top strict"]
    PASS -->|no| RELAX["Relaxed fallback\nBuyScore≥45, loại FOMO/PP"]
```

### Buy Score — 9 nhóm (tối đa ~100 điểm)

| ID | Nhãn | Max | Ghi chú |
|----|------|-----|---------|
| `market` | Pha thị trường | 10 | Favorable/Neutral/Unfavorable |
| `sector` | Ngành | 15 | Top sector rank |
| `rs` | Relative Strength | 20 | RS 5 phiên vs VNINDEX |
| `base` | Nền giá (Darvas/VCP/Spring) | 18 | `BaseQualityEvaluator` |
| `breakout` | Breakout + volume | 22 | Vol×, xác nhận |
| `shakeout` | Shakeout đáy nền | 10 | Hồi phục sau rũ |
| `volume` | Volume spike | 8 | KL bất thường |
| `wyckoff` | Pha tăng giá | 5 | Markup |
| `trend` | Xu hướng MA | 12 | Stack / slope |

**Top gates** (chặn vào list strict): FOMO (`PriceRunupFilter`), phân phối, MA stack theo pha (Full/Medium/Loose), breakout session, shakeout, RS (+ percentile khi Unfavorable), sector, thanh khoản, đủ history. Early Recovery: `GET /api/v1/early-recovery`.

**Nền giá:** `DarvasBreakoutAnalyzer.AnalyzeFlatBox` + parallel gates VCP/Spring — chi tiết [`base-price-engine.md`](./base-price-engine.md).

**Top strict:** `SmartMoneyOpportunitySelector` + `MinPassScore` (prod ~62). Chi tiết [`opportunity-scan-rules.md`](./opportunity-scan-rules.md).

---

## 5. Hai lớp tín hiệu Telegram (T-1 vs Intraday)

> Thiết kế cốt lõi: **Entry Ready** = bộ lọc tĩnh T-1; **Master Alerts** = momentum độc lập trong phiên.

```mermaid
flowchart TB
    subgraph T1["Lớp 1 — Sau Daily Analysis (T-1)"]
        DO["DailyOpportunities\nEntryPointJson · IsActionable\nAverageDailyVolume · MarketPhase"]
    end

    subgraph INTRA["Lớp 2 — OpportunityIntradayMonitor ~60s"]
        Q["KBS quote live"]
        ER["🎯 Entry Ready\n1 lần/phiên · IsActionable\nvùng BaseLow→Trigger"]
        M1["🟢 Mua 1/2\nBP1 band 3–6% + 3 ticks + vol 1.5×"]
        M2["🔥 Mua hết\n≥6% + 3 ticks + vol 1.8×"]
        CUT["🟡/🔴 Trailing + Distribution\nsau BuyPoint1"]
        Q --> ER & M1 & M2 & CUT
    end

    DO --> INTRA
    ER --> TG["Telegram HTML"]
    M1 --> TG
    M2 --> TG
    CUT --> TG
```

| | Entry Ready | Master (Mua/Bán) |
|--|-------------|------------------|
| Đồng bộ `IsActionable` | **Có** | **Không** |
| Ngưỡng | Vùng entry AI | `gainFromBase%` từ `BaseHigh` |
| Volume | Không (early warning) | Paced vol ratio + floor ADV |
| Lặp | 1 lần/phiên | Mỗi kind 1 lần/phiên |

**Công thức gain:** `(close − BaseHigh) / BaseHigh × 100` — **không** dùng `ChangePercent` phiên KBS.

**Paced volume:** `projectedVol = sessionVol / max(elapsedFraction, 0.2)` → ratio vs `AverageDailyVolume`.

Chi tiết đầy đủ: [`telegram-vip-alerts-flow.md`](./telegram-vip-alerts-flow.md).

### Alert khác (không qua VIP Master)

| Nguồn | Khi | Kênh | Loại |
|-------|-----|------|------|
| `DarvasBreakoutAlertPublisher` | Cuối Job 2 | DB `Alerts` + SignalR | Phá hộp Darvas toàn universe |
| `IntradayScannerRunner` | 60s trong phiên | `SessionRadarHits` + UI | Đột biến \|±3%\|, KL≥1M |
| `TradeEventDetector` | Monitor 60s | SignalR + `/market/trades` | Gom im, Đẩy giá, Xả… |
| HPO weekly | T6 sau review | Telegram text | Gợi ý tham số Optuna (không auto-apply) |

---

## 6. Luồng đo hiệu quả & vòng lặp AI

```mermaid
flowchart TB
    DA["Daily Analysis"] --> ST["SetupTracks\n(seed mỗi Top mã)"]
    ST --> WAIT["Chờ T+2.5 phiên"]
    WAIT --> MEAS["OpportunityPerformanceRunner\nMeasurePendingOutcomes"]
    MEAS --> OUT["Hit/Flat/Fail\nMFE · MAE · RS vs index"]

    OUT --> NS["GET /performance/north-star\nHit@T+2.5 Top3/5/10"]
    OUT --> DS["GET /ml/dataset/t25-ranking"]
    DS --> TRAIN["POST /ml/train/t25-ranking\nLogistic Regression"]
    TRAIN --> RANK["IOpportunityRanker\nsort Top list"]

    OUT --> WR["Weekly Review T6 15:30"]
    WR --> RV["WeeklyOpportunityReview DB"]
    WR --> AR["Auto-retrain ranker\n(nếu bật)"]
    WR --> HPO["HyperparameterTuningRunner\nscripts/tune-optuna.py → Telegram"]

    RANK --> DA
```

### North Star (Phase 1 baseline)

- Đo **T+2.5** (horizon DB = 2 phiên, TB đóng T+2 & T+3).
- Báo cáo: `GET /api/v1/performance/north-star?days=90`.
- Ngưỡng success: `SuccessThresholdPercent` (mặc định +1% — cover thuế/phí bán).

### Criterion scoring (Phase 1–3 trader)

Chạy sau mỗi analysis: `DailyCriterionScoringRunner.RunAfterAnalysisAsync`.

| Phase | Mục tiêu | Horizon |
|-------|----------|---------|
| Setup trend | Nền + breakout/shakeout | 5 phiên |
| Outcome swing | MFE/MAE, RS vs VNINDEX | T+2.5 |
| Reliability | Hit rate, edge, bucket score | Rolling 7/30 ngày |

API: `GET /api/v1/criteria/*` · Config: `CriterionAccuracy` trong `appsettings.json`.

### ML OpportunityRanker (Phase 2–3)

| API | Mục đích |
|-----|----------|
| `GET /ml/dataset/t25-ranking` | Export features + label |
| `POST /ml/train/t25-ranking` | Train logistic regression |
| `GET /ml/ranker/status` | Model active? |
| `POST /ml/backfill/setup-tracks` | Lấp tracks lịch sử |
| `POST /ml/tune/evaluate` | HPO evaluate 1 trial |

Sort Top list: `MlProb` nếu model active, else `PredictedHitPercent` heuristic.

### HPO tuần (Phase 0–1)

- Trigger: `WeeklyOpportunityReviewJob` → `HyperparameterTuningRunner`.
- Script: `scripts/tune-optuna.py` (Optuna TPE).
- **Không auto-apply** — chỉ Telegram gợi ý tham số.

---

## 7. Realtime & luồng client

```mermaid
sequenceDiagram
    participant KBS as KBS API
    participant Job as Quartz Job
    participant DB as SQL Server
    participant Cache as QuoteTickCache
    participant Hub as SignalR Hub
    participant API as REST API
    participant FE as Web / Mobile

    Note over Job,KBS: Trong phiên — 60s
    Job->>KBS: Fetch price board
    KBS-->>Job: OHLCV, Vol, NN, sổ lệnh
    Job->>DB: Merge bar T (Job 2) hoặc cache (sync)
    Job->>Cache: SetQuotes
    Job->>Hub: QuotesUpdated / TradeEvent / AlertPublished

    FE->>API: GET /opportunities, /stocks/{sym}
    API->>DB: Snapshot
    DB-->>FE: Top list, BuyDecision detail
    FE->>Hub: Subscribe symbols
    Hub-->>FE: Push live
```

### Output theo màn hình

| Màn hình | API / nguồn | Hiển thị chính |
|----------|-------------|----------------|
| **Cơ hội tốt nhất** | `GET /opportunities` | Rank, Buy Score, P(hit), TradeState, entry, setup DNA |
| **Tín hiệu mới** | `GET /radar/live` | SessionRadar ±3%, KL |
| **Khớp lệnh / Trades** | `GET /market/trades` + SignalR | VSA labels, NN phiên |
| **Chi tiết CP** | `GET /stocks/{sym}` | Full BuyDecision, nền giá, chart |
| **Alerts** | `GET /alerts` | Darvas, buy alerts lịch sử |
| **Performance** | `GET /performance/*` | North Star, summary |
| **Phân tích chỉ báo** | `GET /criteria/*` | Reliability từng criterion |

**Mobile:** `mobile/lib/core/api/api_client.dart` · **Web:** `frontend/src/` · Default API prod trong `api_config.dart`.

---

## 8. Lớp dữ liệu (SQL chính)

| Bảng / Entity | Ghi bởi | Đọc bởi |
|---------------|---------|---------|
| `Stocks` (`HistoryJson`, sector, giá) | Job 1, 2, sync | Mọi engine, stock API |
| `DailyOpportunities` | Daily analysis | Home, VIP monitor, opportunities API |
| `SetupTracks` | Analysis + backfill | Performance, ML dataset |
| `SessionRadarHits` | Intraday scanner | Radar live |
| `Alerts` | Darvas, VIP dispatch | Alerts UI, SignalR |
| `CriterionScoreSnapshots` | Criterion scoring | Criteria API |
| `WeeklyOpportunityReviews` | Weekly review | Performance API |
| `DailyAnalysisRuns` | Analysis | Status / debug |
| Trade events | In-memory `TradeEventStore` | Trades API, SignalR |

---

## 9. Config production quan trọng

### `MarketJobs.DailyAnalysis`

| Key | Prod gợi ý | Ý nghĩa |
|-----|--------------|---------|
| `MaxResults` | 10 | Top list size |
| `RelaxedFallbackEnabled` | false (Phase 1) | Không nới khi strict=0 |
| `MorningRunEnabled` | true | Phân tích 11:30 |
| `MinScore` | 60 | SmartMoney pre-filter |

### `SmartMoney`

| Key | Prod | Ý nghĩa |
|-----|------|---------|
| `MinPassScore` | 62 | Ngưỡng strict Top |

### `MasterAlerts` (VIP intraday)

```json
{
  "BuyPoint1MinChangePercent": 3,
  "BuyPoint2MinChangePercent": 6,
  "MinVolumeRatioPaced": 1.5,
  "BuyPoint2MinVolumeRatio": 1.8,
  "MinElapsedFractionForPacing": 0.2,
  "RequiredConfirmationTicks": 3,
  "BaseTrailingStopPercent1": 2.5,
  "BaseTrailingStopPercent2": 4.0,
  "MarketPhaseMultipliers": { "Favorable": 0.8, "Neutral": 1.0, "Unfavorable": 2.25 }
}
```

### `TelegramNotify`

- `Enabled` + `VipAlertsEnabled` — bật VIP Master + Entry Ready.
- Bot có thể hiện tên "StockRadar HPO" nhưng VIP đi chung `TelegramNotifier`.

---

## 10. Sơ đồ end-to-end (tất cả output)

```mermaid
flowchart TB
    KBS[(KB Buddy)]

    KBS --> J1[Job 1 Universe + History]
    J1 --> DB[(Database)]

    KBS --> J2[Job 2 Session T]
    J2 --> DB
    J2 --> DAR[Darvas Alerts]

    DB --> DA[Daily Analysis]
    DA --> DO[DailyOpportunities]
    DA --> ST[SetupTracks]
    DA --> SH[Shadow + Criterion + T+2.5 measure]

    DO --> UI[Web / Mobile Top list]

    KBS --> SYNC[KBS Sync 60s]
    SYNC --> LIVE[SignalR Quotes]

    KBS --> SCAN[Intraday Scanner]
    SCAN --> RAD[SessionRadar → UI]

    KBS --> MON[Opportunity Monitor 60s]
    MON --> VSA[TradeEvents → UI]
    MON --> VIP[VIP Telegram\nEntry + Master]

    ST --> PERF[North Star API]
    ST --> ML[Ranker train/sort]
    ML --> DA

    SH --> CRIT[Criteria reliability API]
    PERF --> WR[Weekly Review]
    WR --> HPO[HPO Telegram]

    DAR --> UI
    VIP --> TG[Telegram user]
```

---

## 11. File entry & tài liệu chuyên sâu

| Chủ đề | File code | Doc |
|--------|-----------|-----|
| Quartz lịch | `QuartzSchedulingExtensions.cs` | [`pipeline-jobs.md`](./pipeline-jobs.md) |
| Phân tích Top | `DailyAnalysisRunner.cs` | [`opportunity-scan-rules.md`](./opportunity-scan-rules.md) |
| Buy / gates | `BuyDecisionEngine.cs` | [`smartmoney-checklist.md`](./smartmoney-checklist.md) |
| Nền giá Darvas | `DarvasBreakoutAnalyzer.cs` | [`base-price-engine.md`](./base-price-engine.md) |
| VIP Telegram | `TopOpportunityVipAlertPublisher.cs` | [`telegram-vip-alerts-flow.md`](./telegram-vip-alerts-flow.md) |
| ML / HPO | `MlController.cs`, `HyperparameterTuningRunner.cs` | [`pipeline-jobs.md`](./pipeline-jobs.md) §Phase 2–3 |
| Deploy | `scripts/ship-all.ps1` | [`DEPLOY-GDATA.md`](./DEPLOY-GDATA.md) |
| Luồng cũ (tham chiếu) | — | [`project-data-flow.md`](./project-data-flow.md) |

---

## 12. Checklist review trước production

- [ ] Job 1 đã chạy xong — universe active, `lastAnalysisAt` gần đây
- [ ] Job 2 interval 5 phút trong phiên hoạt động (`DailySession.IntervalMinutes`)
- [ ] `DailyAnalysis` 11:30 + 15:05 tạo `DailyOpportunities` > 0 (hoặc fallback có chủ đích)
- [ ] `OpportunityMonitor.Enabled=true`, `MasterAlerts.Enabled=true`
- [ ] `TelegramNotify` token + `VipAlertsEnabled`
- [ ] Migration mới (`AverageDailyVolume`, `MarketPhase`) đã apply trên prod DB
- [ ] `SmartMoney.MinPassScore=62`, `RelaxedFallbackEnabled=false` (Phase 1 North Star)
- [ ] Ship: `.\scripts\ship-all.ps1 -Message "..."` → verify `GET /performance/north-star`
- [ ] Theo dõi 2–3 phiên VIP: Entry Ready không spam; Master có ticks + paced vol hợp lý

---

*Tài liệu kiến trúc tổng hợp — v1.0 (2026-07-08). Khi code lệch doc → tin code, cập nhật doc sau.*
