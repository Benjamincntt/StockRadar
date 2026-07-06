# Luồng dự án StockRadar / JUICE — Input → Output

Tài liệu tổng hợp kiến trúc dữ liệu và luồng xử lý. Cập nhật: 2026-07-06.

---

## 1. Tổng quan một trang

```mermaid
flowchart LR
    subgraph INPUT["INPUT"]
        KBS["KB Buddy API\n(KBS)"]
        USER["User\n(web / mobile)"]
    end

    subgraph CORE["BACKEND .NET :5280"]
        JOBS["Quartz Jobs\nJob 1–3 + sync"]
        DB[("SQL Server\nStocks, Opportunities,\nAlerts, Radar…")]
        ENG["Engines\nSmartMoney · BuyDecision\nSignals · VSA trades"]
        API["REST /api/v1"]
        HUB["SignalR\n/hubs/market"]
    end

    subgraph OUTPUT["OUTPUT"]
        WEB["Web React\nstock.baobiantea.com"]
        MOB["Flutter APK\nJUICE"]
        ZALO["Zalo webhook\n(cảnh báo)"]
    end

    KBS --> JOBS
    JOBS --> DB
    DB --> ENG
    ENG --> DB
    JOBS --> ENG
    USER --> API
    API --> ENG
    API --> DB
    DB --> API
    API --> WEB
    API --> MOB
    JOBS --> HUB
    HUB --> WEB
    HUB --> MOB
    JOBS --> ZALO
```

**Nguồn dữ liệu duy nhất:** KB Buddy (bảng giá, lịch sử, danh sách mã, ngành).  
**Không seed mẫu** — DB đầy sau Job 1 backfill.

---

## 2. Pipeline theo thời gian (Job 1 → 3)

```mermaid
flowchart TB
    subgraph T0["Một lần / định kỳ"]
        J1["Job 1 — History Backfill\nPOST /market/jobs/history"]
        J1 --> U["Lọc universe HOSE+HNX+UPCOM\nKL, IPO, giá tối thiểu"]
        U --> H["KBS History API\n2000 → T-1"]
        H --> SJ["Stocks.HistoryJson\n+ Active universe"]
    end

    subgraph T_SESSION["Mỗi phiên (sau 15h VN)"]
        J2["Job 2 — Daily Session Sync\nPOST /market/jobs/session"]
        J2 --> APP["Append nến ngày T\nvào HistoryJson"]
        APP --> DAR["DarvasBreakoutAlertPublisher\n→ Alerts DB + SignalR"]
        J2A["+2 phút — Daily Analysis\nPOST /market/jobs/analysis"]
        J2A --> SM["SmartMoney + BuyDecisionEngine"]
        SM --> DO["DailyOpportunities\n(list T+1)"]
    end

    subgraph T_INTRADAY["Trong giờ giao dịch"]
        SYNC["KbsMarketSyncJob\n~60s"]
        SCAN["IntradayScanner\n|±3%|, KL≥1M"]
        MON["OpportunityIntradayMonitor\nVSA lô lớn"]
        SYNC --> Q["Quote live + SignalR"]
        SCAN --> RAD["SessionRadarHits"]
        MON --> TR["TradeEvents\nGom im / Đẩy giá…"]
    end

    SJ --> J2
    DO --> MON
```

### Bảng job

| Job | Lịch | Input | Output DB / push |
|-----|------|-------|------------------|
| **Job 1** | Thủ công / startup | KBS history, listing | `Stocks` + `HistoryJson` full |
| **Job 2** | 15:00 VN T2–T6 | KBS bảng giá universe | Nến T merged; Darvas alerts |
| **Phân tích** | 15:02 VN | History + market context | `DailyOpportunities` |
| **KBS sync** | 60s trong phiên | KBS board + VNINDEX | Giá live, `QuoteTickCache` |
| **Intraday scan** | Config | KBS board | `SessionRadarHits` |
| **Trade monitor** | ~60s | KBS board delta | `TradeEvent` + SignalR |
| **Job 3*** | Trong phiên | Watchlist = symbols từ opportunities | Zalo (nếu bật) |

\* Job 3 trong doc pipeline = monitor intraday; `OpportunityIntradayMonitor` quét **toàn universe** (trade prints), Zalo gắn alert Darvas / monitor riêng.

---

## 3. Luồng dữ liệu KBS → DB → Realtime

```mermaid
sequenceDiagram
    participant KBS as KB Buddy API
    participant Job as Quartz Job / Runner
    participant W as MarketDataWriter
    participant DB as SQL Server
    participant S as MarketSyncService
    participant C as QuoteTickCache
    participant H as SignalR Hub
    participant FE as Web / Mobile

    Note over Job,KBS: Job 1 — History
    Job->>KBS: GET history (symbol, from→to)
    KBS-->>Job: OHLCV bars
    Job->>W: UpsertStockHistory
    W->>DB: HistoryJson

    Note over Job,KBS: Job 2 / Sync — Quote phiên
    Job->>KBS: Fetch price board (batch)
    KBS-->>Job: O,H,L,C, Vol, NN, sổ lệnh…
    Job->>S: MarketSyncRequest
    S->>W: UpsertQuotes + merge bar T
    S->>C: SetQuotes
    S->>H: QuotesUpdated
    H-->>FE: push giá live

    Note over FE,DB: Client lần đầu
    FE->>DB: GET /market, /stocks/{sym}
    DB-->>FE: snapshot REST
    FE->>H: subscribe symbols
```

---

## 4. Engine phân tích (sau khi có HistoryJson)

```mermaid
flowchart TB
    STOCK["Stock + HistoryJson\n+ LatestPrice"]
    CTX["SmartMoneyMarketContext\nVNINDEX, ngành, adaptive,\ncalibration, runup filter"]

    STOCK --> SA["SignalAnalyzer"]
    CTX --> SA
    SA --> SIG["Signals\nBreakout, DarvasBreakout,\nVolumeSpike, Shakeout…"]
    SA --> FB["FlatBoxProfile\n(nền giá)"]
    SA --> MA["MA stack, RS, Wyckoff…"]

    STOCK --> BDE["BuyDecisionEngine.Evaluate"]
    CTX --> BDE
    SA --> BDE

    BDE --> SCORE["Buy Score 0–100\n+ breakdown"]
    BDE --> ENTRY["EntryPoint\nReady/Watch/Late/Invalid\n+ giá vào/SL/target"]
    BDE --> GATE["gateFailure?\npassesTopFilter"]
    BDE --> REC["BuyRecommendation\nAvoid/Watch/StrongBuy"]

    STOCK --> SMS["SmartMoneyOpportunitySelector"]
    BDE --> SMS
    SMS --> PASS{"PassesFilter?\nscore ≥ MinPassScore"}

    PASS -->|yes| STRICT["Top list strict"]
    PASS -->|no| RELAX["Relaxed fallback\nscore ≥ 45, loại FOMO/PP"]

    STRICT --> DAR["DailyAnalysisRunner"]
    RELAX --> DAR
    DAR --> OLR["OpportunityListRecommendation\n(ghi đè recommendation list)"]
    OLR --> DB2[("DailyOpportunities")]

    BDE --> DETAIL["GET /stocks/{sym}\n(không ghi đè list)"]
```

### Các engine phụ

| Engine | Vai trò |
|--------|---------|
| `DarvasBreakoutAnalyzer` | Nền giá phẳng + xác nhận breakout |
| `HitProbabilityPredictor` | P hit %, setup DNA trên list |
| `SwingDecisionEngine` | Khuyến nghị swing (card riêng) |
| `TradeEventDetector` | Nhãn VSA: Gom im, Đẩy giá, Xả… |
| `SessionFlowTracker` | NN phiên, áp lực dòng tiền |
| `CriterionScoringService` | Điểm chỉ báo / reliability |

---

## 5. Luồng người dùng — Input → Output UI

```mermaid
flowchart TB
    subgraph USER_ACTIONS["Hành động user"]
        OPEN["Mở app"]
        RUN["Chạy phân tích\nPOST /opportunities/run-analysis"]
        TAP["Chọn mã / search"]
        WL["Thêm watchlist"]
        JOURNAL["Ghi trade journal"]
    end

    subgraph PAGES_WEB_MOB["Màn hình (web ≈ mobile)"]
        HOME["Trang chủ\n• Cơ hội tốt nhất\n• Tín hiệu mới nhất SessionRadar"]
        ALERTS["Khớp lệnh / Trades\nlive VSA"]
        DETAIL["Chi tiết CP\nchart, nền giá, BuyDecision"]
        WLP["Watchlist"]
        CRIT["Phân tích chỉ báo"]
    end

    subgraph API_CALLS["API đọc"]
        OPP["GET /opportunities"]
        RAD["GET /radar/live"]
        STK["GET /stocks/{sym}"]
        CHART["GET /stocks/{sym}/chart"]
        TRD["GET /market/trades"]
        MKT["GET /market/quotes"]
    end

    OPEN --> HOME
    RUN --> OPP
    HOME --> OPP
    HOME --> RAD
    TAP --> DETAIL
    DETAIL --> STK
    DETAIL --> CHART
    ALERTS --> TRD
    HOME --> MKT

    subgraph LIVE["Realtime"]
        HUB["SignalR QuotesUpdated\nTradeEventPublished\nAlertPublished"]
    end

    HUB --> HOME
    HUB --> ALERTS
    HUB --> DETAIL
```

### Output theo màn hình

| Màn hình | Input chính | Output hiển thị |
|----------|-------------|-----------------|
| **Cơ hội tốt nhất** | `DailyOpportunities` + live quote | Rank, Buy Score, P hit, recommendation*, entry status*, setup DNA, giá |
| **Tín hiệu mới nhất** | `SessionRadarHits` + signals | Mã đột biến ±3%, KL, RS |
| **Khớp lệnh** | `TradeEvent` stream | Gom im / Đẩy giá, KL, NN phiên, áp lực |
| **Chi tiết CP** | `BuyDecisionEngine` full | Buy score breakdown, nền giá, entry checklist, swing, chart |
| **Watchlist** | User + sector edit | Danh sách theo dõi thủ công |
| **Alerts** | `Alerts` table | Darvas breakout, buy alerts |

\* Hai badge recommendation + entry — xem `docs/trade-state-unification-proposal.md`.

---

## 6. Deploy & client

```mermaid
flowchart LR
    DEV["Dev\nlocalhost:5280 API\nnpm run dev FE"]
    PROD["Production\n103.226.248.6"]
    PROD --> APIP["stockradar-api :5281"]
    PROD --> FEP["/var/www/publish/stockradar"]
    PROD --> CRON["cron server-auto-deploy\n5 phút"]
    GIT["git push master"] --> GHA["GitHub Actions\n(billing)"]
    GIT --> CRON
    APK["flutter build apk"] --> MOBILE["JUICE APK"]
    MOBILE --> APIP
    FEP --> APIP
```

| Thành phần | Đường dẫn / URL |
|------------|-----------------|
| API prod | `http://103.226.248.6/api/v1` |
| Web | https://stock.baobiantea.com/ |
| Mobile default API | `http://103.226.248.6/api/v1` |
| Repo server | `/var/www/StockRadar` |

---

## 7. Bảng lưu trữ chính (SQL)

| Bảng / entity | Ghi bởi | Đọc bởi |
|---------------|---------|---------|
| `Stocks` (`HistoryJson`, giá, ngành) | Job 1, 2, KBS sync | Mọi engine, API stock |
| `DailyOpportunities` | Daily analysis | Home, opportunities API |
| `SessionRadarHits` | Intraday scanner | Radar live |
| `Alerts` | Darvas publisher, … | Alerts page |
| `SetupTracks` | Sau phân tích | Performance, calibration |
| Trade events | In-memory store + push | `/market/trades`, SignalR |

---

## 8. Timeline ví dụ (1 chu kỳ)

```text
T-1 (23/06)  Job 1 xong — history đến hết 23/06
T   (24/06)  9h–14h45: sync 60s, trade scan, session radar
             15h00: Job 2 append nến 24/06
             15h02: Phân tích → DailyOpportunities cho 25/06
T+1 (25/06)  User xem list "cơ hội" + monitor intraday
             15h00: Job 2 append nến 25/06 → list cho 26/06
```

---

## 9. File tham chiếu trong repo

| Chủ đề | File |
|--------|------|
| Pipeline job | `docs/pipeline-jobs.md` |
| Backend jobs | `backend/README.md` |
| Nền giá | `docs/base-price-engine.md` |
| Gộp trạng thái mua | `docs/trade-state-unification-proposal.md` |
| Deploy | `docs/DEPLOY-GDATA.md` |
| Job runners | `backend/StockRadar.Infrastructure/MarketData/*Runner.cs` |
| Buy engine | `backend/StockRadar.Domain/Services/BuyDecisionEngine.cs` |
| Web routes | `frontend/src/App.tsx` |
| Mobile tabs | `mobile/lib/widgets/app_bottom_nav.dart` |

---

## 10. Sơ đồ end-to-end (chi tiết)

```mermaid
flowchart TB
    KBS[(KB Buddy)]

    KBS -->|history| J1[Job 1 Backfill]
    J1 -->|HistoryJson| DB[(Database)]

    KBS -->|board 60s| KS[KbsMarketSync]
    KBS -->|board| J2[Job 2 Session]
    J2 -->|merge bar T| DB
    J2 -->|breakout mới| AL[Alerts + SignalR]

    DB --> DA[Daily Analysis]
    DA -->|SmartMoney| DB
    DA -->|DailyOpportunities| DB

    KBS -->|board delta| TM[Trade Monitor]
    TM -->|TradeEvent| SR[SignalR]
    TM -->|quotes| SR

    KBS -->|board| IS[Intraday Scanner]
    IS -->|SessionRadar| DB

    DB --> API[REST API]
    SR --> API
    API --> WEB[Web]
    API --> APP[Mobile]

    USER[User] -->|run-analysis, watchlist| API
    AL --> ZALO[Zalo webhook]
```

*Tài liệu luồng dự án — v1.0*
