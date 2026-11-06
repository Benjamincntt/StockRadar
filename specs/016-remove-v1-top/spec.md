# Feature Specification: Gỡ V1 (Top cơ hội hằng ngày và toàn bộ đường chấm điểm/xếp hạng phía sau)

**Feature Branch**: `016-remove-v1-top` (làm trên `master` như spec 012–015)

**Created**: 2026-11-05

**Status**: Draft — owner chấp nhận đề xuất của AI cho Q1–Q6 ("đồng ý hết", 05/11/2026); owner thêm danh sách vị thế V3 lên Home và chốt gỡ hẳn radar (06/11/2026, mục "Bổ sung 06/11/2026")

**Input**: Owner (05/11/2026), sau khi đọc kết quả PH-30 và PH-31: "Viết spec gỡ V1". Trước đó owner đặt luật: "Đo trước rồi quyết. Backtest Top 5 của V1 cùng kỳ với V3 (2023–2026). Nếu kém thì gỡ, nếu có ích thì giữ."

## Bối cảnh

- Spec 011 đã gỡ phần bắn tin mua/bán VIP của V1 và giữ phần lọc: phân tích hằng ngày → cổng → xếp hạng ML → hygiene → giới hạn ngành → **Top 5** hiện trên Home. Spec 012 dựng V3 chạy song song và không dùng phần lọc này (V3 không gọi `SignalAnalyzer`, `BuyDecisionEngine`, `SmartMoney*`, `SectorWave*`, `MarketPhaseClassifier`).
- Bằng chứng đo (sổ nghiên cứu `docs/features/v3-scenario-engine/research/02-so-phat-hien.md`):
  - **PH-30**, Top thật trên production 25/06–05/11/2026, mua ATC phiên Top (Top lưu lại là bản làm mới ~14:00 trong phiên): V1 **−1,14%/lệnh** (82 lệnh), chọn ngẫu nhiên −1,60% (−2,51…−0,80), V3 **+3,19%**. Danh mục V1 −13,3%, V3 +2,3%, chỉ số bình quân đều −13,2%.
  - **PH-31**, phát lại V1 hiện tại 943 phiên 2023–2026 bằng harness C# (`StockRadar.Tests/Research/V1TopReplayTests.cs`), trùng 71% với Top thật từ khi model ML hiện tại chạy: V1 có ML **−0,24%/lệnh** (2.698 lệnh), tắt ML −0,20%, chọn ngẫu nhiên **−0,19%** (−0,26…−0,13). Danh mục 3 năm: V1 có ML **−9%**, tắt ML −36%, **V3 +264%**.
  - Kết luận: Top V1 không có lợi thế chọn mã; theo luật owner đặt, gỡ.
- Model ML xếp hạng trên server học từ 45 mẫu (25/09/2026), tự báo độ chính xác 100%. Huấn luyện lại tự động (`TryAutoRetrainAsync`) và HPO không còn nơi gọi.
- Phát hiện kỹ thuật phải giữ khi gỡ (kiểm kê 05/11/2026):
  - **Nến đóng cửa của Job 2 chỉ được ghi trong `DailyAnalysisJob`.** `DailySessionSyncJob` chạy 5 phút/lần và bỏ qua ngoài giờ khớp lệnh (sau 14:45). `KbsMarketSyncJob` cũng dừng 14:45. Lần chạy 15:05 của `DailyAnalysisJob` gọi `session.RunAsync()` trước khi phân tích (`Scheduling/Jobs/DailyAnalysisJob.cs:50-51`), và đó là lần duy nhất ghi nến đã gồm ATC vào `Stocks.HistoryJson`. Nhịp 16:00 của V3 cần nến này.
  - Spec 015 (draft) sẽ cho V3 dùng `BoDieuChinhGiaTheoQuyen`, `FileNguonSuKienQuyen`, `INguonLichChotQuyen`/`FireAntLichChotQuyenClient`. Hiện chỉ V1 dùng, nhưng phải giữ.
  - V1 còn nuôi các bề mặt ngoài thẻ Top: chi tiết mã (`GET stocks/{symbol}` chạy `BuyDecisionEngine`, đọc `DailyOpportunities`, hiện tỉ lệ khối lượng, RS, flatBox), điểm trong Watchlist, thẻ VNINDEX (pha thị trường qua `MarketPhaseClassifier`), thẻ "Tín hiệu mới nhất" (`IntradayScannerJob` → `SessionRadarHits`).

## User Scenarios & Testing

### User Story 1 — Home bỏ Top V1, thêm danh sách vị thế V3 đang giữ (Priority: P1)

Nhà đầu tư mở Home (mobile và web) không còn thấy khối "Top cổ phiếu xác suất cao" / "Cơ hội tốt nhất", nhãn trạng thái giao dịch (Watch/Actionable…), điểm Buy Score, nút "chạy phân tích". Thẻ radar "Tín hiệu mới nhất" cũng gỡ. Home vẫn còn, gồm theo thứ tự: ô tìm mã, thẻ VNINDEX (mobile, không nhãn pha), **danh sách mã V3 đang giữ** (mới).

**Why this priority**: Top là bề mặt người dùng thấy và đã đo là không có ích; vị thế đang giữ là thứ owner cần theo dõi hằng ngày.

**Independent Test**: mở Home trên mobile và web sau khi gỡ: không còn khối Top và thẻ radar; danh sách đang giữ khớp màn Hiệu quả; không lỗi gọi API.

**Acceptance Scenarios**:

1. **Given** app sau khi gỡ, **When** mở Home, **Then** không có khối Top V1 và không còn request tới `opportunities`, `opportunities/run-analysis`.
2. **Given** V3 có vị thế `DangGiu`, **When** mở Home, **Then** mỗi vị thế một dòng: mã, kịch bản, **giá mua**, **giá hiện tại** (cập nhật trực tiếp), **% lãi/lỗ so với giá mua**, **giá chốt lời**, **số phiên còn lại tới hạn bán**; bấm vào mở chi tiết mã. Khi không có vị thế thì hiện một dòng "Chưa giữ mã nào".
   - Giá chốt lời: lệnh thường = max(ngưỡng chạm MA đã lưu, giá mua × 1,004); lệnh hoảng loạn sâu = giá mua × 1,15; N3 không có giá chốt lời (bán ATO theo lịch) → hiện "Bán ATO theo lịch".
   - Số phiên còn lại = giữ tối đa của vị thế (KB1/KB2/N1 15, KB4 10, N3 12, hoảng loạn sâu 40 — cấu hình `V3`) − số phiên đã giữ; hết hạn thì bán ATC.
   - **Giá cắt lỗ: chưa hiện.** V3 hiện không có luật cắt lỗ; owner yêu cầu backtest các phương án cắt lỗ (việc nghiên cứu riêng). Chỉ thêm cột khi có luật được owner chốt, qua spec riêng.
3. **Given** app bản cũ chưa cập nhật gọi `GET opportunities`, **When** server đã gỡ, **Then** app cũ hiện lỗi/khối rỗng có kiểm soát, không treo màn Home (xử lý theo Q5).

---

### User Story 2 — Không còn tiến trình V1 chạy nền, V3 không suy chuyển (Priority: P1)

Server không còn chạy phân tích Top (11:30, 15:05, làm mới 15 phút trong phiên), không còn xếp hạng ML, không còn ghi các bảng của V1. Toàn bộ nhịp V3 (08:30, mỗi phút, 14:25, 16:00) chạy y như trước, với nến đóng cửa đầy đủ trong `Stocks.HistoryJson`.

**Why this priority**: gỡ nhầm phần dùng chung là hỏng V3, hệ duy nhất đang có lãi.

**Independent Test**: chạy một ngày giao dịch trên dev/prod sau khi gỡ: nến ngày trong `HistoryJson` của một mã bất kỳ khớp giá đóng cửa ATC; nhịp 16:00 V3 log đủ bước như ngày trước khi gỡ; `TinHieuV3`/`ViTheV3`/`ChiSoNgayV3` có dòng mới như thường.

**Acceptance Scenarios**:

1. **Given** phiên kết thúc 14:45, **When** tới 15:05, **Then** Job 2 vẫn chạy một lần sau đóng cửa (trigger riêng, không còn nằm trong `DailyAnalysisJob`) và ghi nến có ATC.
2. **Given** danh sách job Quartz sau khi gỡ, **When** liệt kê, **Then** không còn `daily-analysis` và các job V1 owner chốt bỏ ở Q2; còn `history-backfill`, `daily-session-sync` (+ trigger sau đóng cửa), `kbs-market-sync`, bốn job V3.
3. **Given** cùng dữ liệu nến, **When** chạy nhịp 08:30/14:25/16:00 V3 trước và sau khi gỡ, **Then** tín hiệu và vị thế V3 giống hệt nhau.

---

### User Story 3 — Chi tiết mã và Watchlist không còn điểm V1 (Priority: P2)

Màn chi tiết mã và Watchlist không còn hiện Buy Score / quyết định mua / trạng thái giao dịch của V1. Phần V3 trên chi tiết mã (huy hiệu, thẻ V3, sự kiện quyền) giữ nguyên. Các số đo thô (tỉ lệ khối lượng, RS, flatBox) giữ hay bỏ theo Q3.

**Why this priority**: điểm V1 đã đo là không dự báo; để lại là tiếp tục dẫn người dùng theo một con số vô nghĩa.

**Independent Test**: mở chi tiết một mã có tín hiệu V3 và một mã không có: không còn khối điểm V1, phần V3 hiển thị như trước, không lỗi.

**Acceptance Scenarios**:

1. **Given** mã bất kỳ, **When** mở chi tiết, **Then** không còn Buy Score/quyết định V1; thẻ V3 và biểu đồ hiện bình thường.
2. **Given** Watchlist có mã, **When** mở, **Then** không còn cột/nhãn điểm V1 (thay bằng gì theo Q4).

---

### User Story 4 — Dữ liệu, cấu hình, tài liệu V1 được dọn sạch (Priority: P3)

Không còn bảng, cấu hình, endpoint, script, tài liệu sống mô tả V1 như đang chạy. Dữ liệu cũ được xuất ra file trước khi xoá bảng (theo tiền lệ spec 011 `dotb-drop-vip-tables.sql`).

**Why this priority**: xoá tính năng là xoá sạch, không giữ shim; nhưng phải sau khi phần chạy đã gỡ an toàn.

**Independent Test**: grep backend/mobile/frontend không còn tham chiếu tới các bảng, option, endpoint đã gỡ; `docs/domain/*` và `CLAUDE.md` không còn mô tả V1 như đang chạy.

**Acceptance Scenarios**:

1. **Given** DB production, **When** chạy migration gỡ, **Then** các bảng V1 (danh sách ở Phụ lục A) đã được xuất ra file và bị xoá; bảng dùng chung và bảng V3 còn nguyên.
2. **Given** `appsettings*.json`, **When** rà, **Then** không còn mục cấu hình chỉ V1 dùng (Phụ lục B).

### Edge Cases

- Gỡ `DailyAnalysisJob` mà quên trigger Job 2 sau đóng cửa → `HistoryJson` thiếu nến ATC, V3 16:00 tính trên giá 14:45 hoặc thiếu phiên. Phải có trigger riêng **trước** khi xoá job.
- Thứ tự deploy: server gỡ API trước khi app mới lên store → app cũ gọi `opportunities` nhận 404 (Q5).
- `NhatKyService` (nhật ký giao dịch, V3) dùng `RealizedPnlOptions`, `RealizedPnlMath`, hằng số trong `Domain/MasterAlerts`. Không xoá các thứ này dù nằm cạnh code V1.
- `StockUniverseFilter` do Job 1 dùng để rescreen universe, phải giữ.
- `SignalAnalyzer` đang được chi tiết mã, `IntradayScannerRunner`, `RadarService`, `MarketService` dùng; cả bốn nơi đều gỡ, nên xoá được.
- Radar và bộ theo dõi lô lớn đang ghi giá vào bộ nhớ đệm và đẩy `QuotesUpdated`. Sau khi gỡ, `KbsMarketSyncJob` (`MarketSyncService.ApplyAsync`, 60 giây, toàn bộ mã) là nơi duy nhất làm việc này; đã đủ cho giá trực tiếp trên app. V3 trong phiên đọc giá thẳng từ `KbsPriceBoardClient`, không qua bộ nhớ đệm.
- App mobile bản cũ tải Top, radar, VNINDEX chung một `Future.wait` (`home_screen.dart:132-136`): `opportunities` trả 404 làm hỏng cả Home, kể cả thẻ VNINDEX. Chỉ gỡ endpoint sau khi mọi máy đã cài app mới (Q5).
- Test hồi quy rights adjustment (`StockRadar.Tests/DieuChinhGia/*`) đang test qua `SignalAnalyzer`; spec 015 cần bộ điều chỉnh, nên giữ test hoặc chuyển sang test thẳng `BoDieuChinhGiaTheoQuyen`.
- Harness nghiên cứu `V1TopReplayTests.cs` phụ thuộc toàn bộ V1: xoá cùng V1, kết quả đã nằm trong PH-31.

## Requirements

### Functional Requirements

- **FR-001**: Hệ thống MUST ngừng chạy phân tích Top V1 (cả lần 11:30, 15:05 và làm mới trong phiên) và xoá `DailyAnalysisJob`, `DailyAnalysisRunner`, endpoint `opportunities*`, `market/jobs/analysis`.
- **FR-002**: Hệ thống MUST chạy Job 2 (`DailySessionSyncRunner`) một lần sau đóng cửa (mặc định 15:05 ngày giao dịch) bằng trigger riêng, để `Stocks.HistoryJson` có nến đã gồm ATC trước nhịp V3 16:00. Trigger này MUST có trước hoặc cùng change set với FR-001.
- **FR-003**: Hệ thống MUST xoá đường xếp hạng ML và hiệu chỉnh của V1: `OpportunityRanker*`, huấn luyện, HPO, `TuneEvaluate`, `ml/*`, `HitCalibration*`, `AdaptiveScoringProfile*`, `CriterionAccuracy*`/`CriterionScoring*`, `SetupTrack*`, `EarlyRecovery*`, `ShadowAnalysis*`, `EntryTiming*`, `EngineTrust*`, `OpportunityNorthStar*`, `SmartMoneyBacktestRunner` + `backtest/smartmoney`, `SectorWaveRegime*` + backfill, hosted service `SmartMoneyContextWarmer`, `OpportunityRankerBootstrap`.
- **FR-004**: Hệ thống MUST xoá cảnh báo Darvas trong Job 2 (`DarvasBreakoutAlertPublisher`, ghi bảng `Alerts`, SignalR `AlertCreated` không có client nghe) mà không đổi phần append nến / VNINDEX / rescreen của Job 2.
- **FR-005**: Mobile và web MUST bỏ khối Top V1 trên Home, `TradeStateBadge`/nhãn trạng thái giao dịch, model/type và hàm API của V1, cùng các widget/hook mồ côi đã kiểm kê (Phụ lục C).
- **FR-006**: Chi tiết mã và Watchlist MUST bỏ Buy Score/quyết định/trạng thái V1 và không còn đọc `DailyOpportunities`. Phần giữ lại theo Q3–Q4.
- **FR-007**: Radar "Tín hiệu mới nhất" và theo dõi lô lớn/dòng tiền MUST gỡ cả job (`IntradayScannerJob`, `OpportunityIntradayMonitorJob`), bảng `SessionRadarHits`, endpoint (`radar-items*`, `market/jobs/intraday-scan`, `market/intraday-monitor`, `market/trades`, `market/flow/*`), SignalR (`RadarUpdated`, `TradeEventCreated`), cấu hình (`IntradayScanner`, `OpportunityMonitor`), UI.
- **FR-018**: Home mobile và web MUST có danh sách vị thế V3 `DangGiu` như US1 kịch bản 2, lấy từ endpoint V3 hiện có `v3/hieu-qua/vi-the-dang-giu` (lọc `DangGiu`). Giá chốt lời và số phiên còn lại MUST tính ở server bằng đúng hàm `V3LuatBanThucTe` (`GiuToiDaTheoViThe`, ngưỡng đã lưu `NguongChamMa`) và trả trong `ViTheV3Dto`, client không chép công thức. Không thêm logic chọn mã mới.
- **FR-019**: Sau khi gỡ, giá trực tiếp (`QuotesUpdated`, `GET market/quotes`, `market/sparklines`) MUST vẫn có từ `KbsMarketSyncJob`. Sparkline trên Home chỉ còn cho mã đang giữ.
- **FR-008**: Hệ thống MUST giữ nguyên mọi thứ V3 và các nhịp dữ liệu dùng chung (Phụ lục D), kể cả các thành phần spec 015 sẽ dùng.
- **FR-009**: Dữ liệu các bảng V1 MUST được xuất ra file (CSV/JSON, lưu ngoài DB) trước khi migration xoá bảng; migration MUST đọc lại theo luật repo trước khi apply (`dotnet ef migrations add` trong repo này sinh body sai).
- **FR-010**: Mục cấu hình chỉ V1 dùng MUST được xoá khỏi `appsettings*.json`, `appsettings.Production.json.example` và **cả file trên server** (khác repo), trong cùng đợt deploy.
- **FR-011**: Tài liệu MUST cập nhật cùng change set: `CLAUDE.md` (pipeline, bảng luật sản phẩm, entry thường dùng), `docs/domain/pipeline-jobs.md`, `buy-decision.md`, `ma-stack-and-market-phase.md`, `base-price-flatbox.md` (thành tombstone như `realized-pnl.md` nếu không còn code), `docs/architecture.md`, `docs/entity_model.md`, use case UC-003/005/006/008, skill `.claude/skills/*` nhắc tới `DailyAnalysisRunner`/`BuyDecisionEngine`, script V1 trong `scripts/`.
- **FR-012**: Không giữ shim, enum hay nhãn "deprecated" cho phần đã gỡ.
- **FR-017**: Trước commit xoá code V1, MUST có tài liệu lưu trữ `docs/features/v1-archive/` mô tả cách V1 làm, đủ để dựng lại không cần nghiên cứu lại:
  - chuỗi cổng theo đúng thứ tự trong `DailyAnalysisRunner` và `BuyDecisionEngine` (điều kiện, ngưỡng, giá trị cấu hình tại thời điểm gỡ, nhãn lý do);
  - cách tính Buy Score (từng thành phần, trọng số), PredictedHitPercent, trạng thái giao dịch (`TradeStateResolver`), nhãn khuyến nghị cũ;
  - pha thị trường (`MarketPhaseClassifier`: MA20, FTD, đáy cao dần), sóng ngành và regime nhiều phiên, RS percentile, RS Leader;
  - nền giá / flatBox / Darvas (`BaseQualityEvaluator`, `DarvasBreakoutAnalyzer`);
  - xếp hạng: 11 đặc trưng của model ML, trọng số model đang chạy (bản 25/09/2026), cộng điểm ngành, hygiene, giới hạn ngành; cách huấn luyện và nhãn;
  - radar trong phiên và theo dõi lô lớn (ngưỡng phát hiện);
  - kết quả đo PH-30, PH-31 và kết luận vì sao gỡ;
  - commit cuối còn code V1 (để `git checkout` lại khi cần) và cách chạy harness `V1TopReplayTests` ở commit đó.
  Các file `docs/domain/buy-decision.md`, `ma-stack-and-market-phase.md`, `base-price-flatbox.md` thành tombstone trỏ sang tài liệu lưu trữ.

### Key Entities

- **Bảng V1 bị xoá**: danh sách đầy đủ ở Phụ lục A.
- **Nến ngày dùng chung** (`Stocks.HistoryJson`): giữ, nguồn của V3; Job 1, Job 2 (kể cả trigger sau đóng cửa mới), `KbsMarketSync`, rescreen universe tiếp tục ghi.

## Success Criteria

- **SC-001**: Sau deploy, 5 phiên liên tiếp: nhịp V3 16:00 chạy đủ, nến ngày trong `HistoryJson` của 10 mã kiểm ngẫu nhiên khớp giá đóng cửa ATC trên bảng giá.
- **SC-002**: Grep backend/mobile/frontend không còn tham chiếu tới tên class, bảng, endpoint, option ở Phụ lục A–C (trừ migration lịch sử và tombstone docs).
- **SC-003**: Build backend, `dotnet test`, `flutter analyze`, `npm run build` đều sạch; số test giảm đúng bằng test V1 đã gỡ, không test V3 nào hỏng.
- **SC-004**: Số job Quartz trong phiên giảm ít nhất 1 (`daily-analysis`) cộng các job Q2 owner chốt bỏ; thời gian CPU/ghi DB của server trong phiên giảm (đo bằng log nhịp trước/sau, không đặt ngưỡng cứng).
- **SC-005**: Dữ liệu xuất của các bảng V1 có số dòng khớp `COUNT(*)` trước khi xoá.

## Đã làm rõ (owner: "đồng ý hết", 05/11/2026)

Owner chấp nhận đề xuất của AI cho cả 6 câu dưới đây:

- **Q1**: gỡ sạch Top và toàn bộ đường chấm điểm/ML/hiệu chỉnh phía sau.
- **Q2** (owner xác nhận lại 06/11/2026, xem "Bổ sung 06/11/2026"): gỡ cả radar "Tín hiệu mới nhất" (`IntradayScannerJob`, `IntradayScannerRunner`, `RadarService`, `radar-items*`, `SessionRadarHits`, SignalR `RadarUpdated`, cấu hình `IntradayScanner`, thẻ trên Home mobile và web) lẫn theo dõi lô lớn/dòng tiền (`OpportunityIntradayMonitorJob` và stack `TradeEvent*`/`SessionFlow*`/`OrderBook*`/`IntradayMonitorStatus*`, endpoint `market/intraday-monitor`, `market/trades`, `market/flow/*`, SignalR `TradeEventCreated`, cấu hình `OpportunityMonitor`). Trước khi gỡ monitor phải xác nhận việc nó ghi giá vào DB (`sync.ApplyAsync`) đã được `KbsMarketSyncJob` làm đủ.
- **Q3**: chi tiết mã bỏ tỉ lệ khối lượng, RS, flatBox; chỉ còn giá, biểu đồ, V3, sự kiện quyền. Xoá `SignalAnalyzer` (trừ phần rights adjustment spec 015 cần, chuyển test `DieuChinhGia/*` sang `BoDieuChinhGiaTheoQuyen`), `BaseQualityEvaluator`, cấu hình `PriceRunupFilter`.
- **Q4**: Watchlist bỏ điểm V1; mã có tín hiệu hoặc vị thế V3 thì hiện huy hiệu V3.
- **Q5**: đưa app mobile/web mới (không gọi API V1) lên trước; sau đó mới gỡ endpoint và job trên server.
- **Q6**: thẻ VNINDEX bỏ nhãn pha thị trường, chỉ giữ giá và biểu đồ. Xoá `MarketPhaseClassifier`, `VnIndexPriorPeakAnalyzer`, cấu hình `SmartMoney` (cả `MarketPhase`) và `MasterAlerts`, trừ hằng số `Domain/MasterAlerts` mà `NhatKyService` dùng.

### Bổ sung (owner, 05/11/2026)

Sau khi xem các cổng lọc của V1 và V3, owner chốt: "Cổng sắp chốt quyền giữ hoặc chuyển sang cho v3. còn lại bỏ hết. chỉ ghi tài liệu cách làm để sau có dùng lại thì không cần nghiên cứu lại".

- **Cổng sắp chốt quyền**: chuyển sang V3, đặc tả ở spec 015 US6 (đo trước rồi mới bật). Spec 016 gỡ nơi gọi của V1 nhưng **giữ** `INguonLichChotQuyen`, `FireAntLichChotQuyenClient`, `ThongTinChotQuyen`, `FireAntOptions`. Nếu spec 015 US6 chưa xong khi gỡ V1 thì trong khoảng đó không bên nào dùng cổng; chấp nhận vì cổng chỉ từng chặn Top V1.
- **Các cổng và phần chấm điểm còn lại của V1**: gỡ hết như FR-001…FR-004.
- **Tài liệu lưu trữ** (FR-017): trước khi xoá code, ghi lại cách V1 làm đủ chi tiết để dựng lại mà không phải nghiên cứu lại.

### Bổ sung (owner, 06/11/2026)

Sau khi AI rà các màn bị ảnh hưởng, owner viết: "lo gic cần nhất quán và tối ưu chỉ 1 luồng nhưng không bỏ màn home được. top cổ phiếu có thể bỏ nhưng nên để list danh sách đã mua vào home cho dễ theo dõi . tín hiệu mua bán lớn cũng phải giữ lại." Sau đó owner trả lời các câu AI hỏi:

- **"Tín hiệu mua bán lớn"** là thẻ radar "Tín hiệu mới nhất" trên Home → ban đầu owner chọn giữ. Sau đó owner hỏi "Radar bỏ cả logic có được không => điều tra xem có ảnh hưởng các phần nào của v3 không"; AI kiểm: radar không dùng cho V3, giá trực tiếp vẫn có từ `KbsMarketSyncJob` (FR-019). Owner chốt **gỡ hẳn radar** → Q2 giữ nguyên như ban đầu (gỡ cả radar lẫn lô lớn).
- **"Danh sách đã mua"** lấy từ **vị thế V3 đang giữ** (`ViTheV3`, `DangGiu`) → FR-018. Lưu ý: theo spec 014, `ViTheV3` chạy theo lệnh thật khi đặt `V3:TaiKhoanDanhMuc`; khoá này trên server **chưa đặt**, nên danh sách hiện là số hệ thống.
- **Vùng tích luỹ trên biểu đồ web** (vẽ từ `flatBox.periods`): owner chọn **bỏ luôn**, đúng Q3.
- **Cột của danh sách đang giữ**: owner yêu cầu giá mua, giá hiện tại, % lãi lỗ so với giá mua, giá chốt lời, giá cắt lỗ, số phiên đến điểm bán nếu cố định. AI báo V3 không có luật cắt lỗ; owner: "Cần backtest tất cả các phương án cắt lỗ để xem cái nào hiệu quả. nếu cần thiết hãy sáng tạo ra các cách cắt lỗ chưa từng có và thử nghiệm" → việc nghiên cứu riêng, ngoài spec 016; cột cắt lỗ chưa hiện.

## Câu hỏi đã hỏi owner

Đề xuất của AI đi kèm mỗi câu; owner đã chấp nhận cả 6 (xem mục trên).

- **Q1 — Phạm vi gỡ.** Đề xuất: gỡ cả Top lẫn toàn bộ đường chấm điểm/ML/hiệu chỉnh phía sau (FR-001…FR-004), vì không phần nào còn nơi dùng ngoài Top và chi tiết mã. Phương án hẹp: chỉ ẩn Top trên Home, giữ backend — trái luật "xoá là xoá sạch".
- **Q2 — Thẻ "Tín hiệu mới nhất" (radar trong phiên, `IntradayScannerJob`, `SessionRadarHits`) và theo dõi lô lớn/dòng tiền (`OpportunityIntradayMonitorJob`, không UI nào hiện).** Chưa được đo. Đề xuất: gỡ theo dõi lô lớn (không ai xem); radar để owner chọn giữ hoặc gỡ.
- **Q3 — Chi tiết mã còn giữ tỉ lệ khối lượng, RS, flatBox không?** Các số này không phải điểm V1 nhưng tính bằng `SignalAnalyzer`/`BaseQualityEvaluator` của V1. Đề xuất: bỏ cả, chi tiết mã chỉ còn giá, biểu đồ, V3, sự kiện quyền; nếu owner muốn giữ thì giữ hai class này.
- **Q4 — Watchlist thay điểm V1 bằng gì?** Đề xuất: bỏ cột điểm; nếu mã đang có tín hiệu/vị thế V3 thì hiện huy hiệu V3 (đã có sẵn trên chi tiết mã).
- **Q5 — Thứ tự deploy với app mobile.** Đề xuất: (1) build app mới không gọi API V1 lên trước; (2) sau đó mới gỡ endpoint trên server. App cũ trong thời gian chờ vẫn gọi được vì server chưa gỡ.
- **Q6 — Thẻ VNINDEX trên mobile còn hiện "pha thị trường" (Favorable/Neutral/Unfavorable) không?** Pha này tính bằng `MarketPhaseClassifier` của V1 và trên production **không bao giờ ra Favorable** vì bảng `MarketIndices` lưu O = H = L = C (PH-31). Đề xuất: bỏ nhãn pha, chỉ giữ giá và biểu đồ; nếu giữ thì phải sửa dữ liệu VNINDEX trước (việc riêng).

## Phụ lục A — Bảng V1 (theo kiểm kê 05/11/2026; xác nhận lại lúc lập plan)

`DailyOpportunities`, `DailyAnalysisRuns`, `EarlyRecoveryRadar`, `CriterionWeights`, `DailyCriterionAccuracies`, `StockCriterionScores`, `StockCriterionDetails`, `CriterionGroupDailyAccuracies`, `WeeklyCriterionReviews`, `CriterionGroupWeeklyReviews`, `SetupTracks`, `WeeklyOpportunityReviews`, `ShadowWeightPicks`, `ShadowWeightSummaries`, `EntryTimingStates`, `HitCalibrationBuckets`, `HitCalibrationStates`, `SectorWaveRegimes`, `Alerts` (chỉ Darvas ghi). Không code nào đọc/ghi: `VipAlertFires`, `FalsePositiveMiningStates`. Chỉ qua repository không ai gọi (`EfTradeJournalRepository`): `TradeJournalEntries`, `PersonalCalibrationStates`. Có repository trong `EfPerformanceRepositories.cs`, cần xác nhận nơi gọi lúc lập plan: `MasterAlertPositions`, `PositionSellLegs`. Theo Q2: `SessionRadarHits`.

## Phụ lục B — Cấu hình chỉ V1 dùng

`MarketJobs.DailyAnalysis`, `CriterionAccuracy`, `SmartMoney` (trừ `MarketPhase` nếu Q6 giữ nhãn pha), `PriceRunupFilter` (nếu Q3 bỏ flatBox), `OpportunityRanker`, `HyperparameterTuning`, `TuneEvaluate`, `OpportunityPerformance`, `ShadowAnalysis`, `MasterAlerts` (nếu Q6 bỏ đỉnh VNINDEX), `Cache.SmartMoneyContextSeconds`, `Cache.EngineTrustSeconds`; theo Q2: `IntradayScanner`, `OpportunityMonitor`. Không code nào dùng: `SwingTrading`.

## Phụ lục C — Bề mặt client

- Mobile: khối Top trong `screens/home_screen.dart`, `widgets/trade_state_badge.dart`, `core/labels/trade_state_labels.dart`, lớp V1 trong `models.dart` (`Opportunity`, `EngineTrust`, `OpportunitiesList`, `DailyAnalysisResult`, `BuyDecision`, `EntryPoint*`, `Criterion*`, `SmartMoneyBacktest*`, `TradeEvent`, `IntradayMonitorStatus`…), hàm V1 trong `core/api/api_client.dart`, widget mồ côi (`smart_money_backtest_card.dart`, `price_levels_card.dart`, `stock_detail_widgets.dart`, `core/labels/job_state_labels.dart`), route chết `/criteria/summary`, `/performance/summary`, `/performance/alert-history`, listener `TradeEventCreated` + `recentTrades` trong `market_hub_service.dart`, thẻ radar (`_radarTile`, `_SessionRadarStatusLine`, `_signalLabelVi`, model `RadarLive*`, `getRadarLive`), dòng "vùng tích lũy" ở tiêu đề biểu đồ chi tiết mã. Giữ `score_pill.dart` (V3 dùng), `getSparklines` (sparkline cho mã đang giữ).
- Web: khối "Cơ hội tốt nhất" trong `pages/HomePage.tsx`, `components/entry/TradeStateBadge.tsx`, `lib/tradeState.ts`, type V1 trong `types/index.ts`, hàm V1 trong `lib/api.ts`, mồ côi (`SmartMoneyBacktestPanel.tsx`, `TradePrintList.tsx`, `IntradayMonitorStatusLine.tsx`, `hooks/useLiveTrades.ts`, `hooks/useIntradayMonitorStatus.ts`, `lib/tradeLabels.ts`, `HeatmapGrid.tsx`, `Charts.tsx`), vùng tích luỹ trên biểu đồ (`lib/chartAccumulation.ts`, `components/chart/AccumulationLegend.tsx`, prop `accumulationPeriods`/đường hộp của `PriceVolumeChart`), `components/radar/SessionRadarList.tsx`, type/hàm radar. Giữ `useSparklines`, `ScorePill` (`LiveChangePill` dùng).
- Không client nào gọi, gỡ cùng: `getSignals`, `getRadar`, `getSectors`, `getOpportunitySymbols`, `getJob1Status`/`runJob1*` (web); `getCriteriaSummary`, `getJobs`/`runJob`, `getJob1Status`/`runJob1*`, `getPerformanceSummary`, `getAlertHistory*` (mobile) — hàm Job 1 chỉ gỡ ở client, endpoint server giữ.

## Phụ lục D — Phải giữ (V3 và dữ liệu dùng chung)

- Job 1 `HistoryBackfillJob` + rescreen (`StockUniverseFilter`), Job 2 `DailySessionSyncJob` (kể cả giữ ấm mã inactive) + trigger sau đóng cửa mới, `KbsMarketSyncJob` (giá realtime, SignalR `QuotesUpdated`/`IndexUpdated`), bốn job V3, `IQuoteTickCache`, `market/quotes`, `market/sparklines`.
- Chi tiết mã giữ các trường `history` (biểu đồ ngày) và `isUniverseActive`/`universeStatusReason`/`universeUpdatedAt` (dòng "mã ngoài universe").
- `IJobStockRepository` (`GetAllForUniverseScreeningAsync`, `GetBySymbolsAsync`), `KbsPriceBoardClient`, `Stock`/`OhlcvBar`, `ExchangePriceBand`, `TradingSessionMath`, `VietnamMarketCalendar`, `IndicatorMath`, `IGuiThongBao` (Telegram, FCM), `INhatKyRepository`, `NhatKyService` cùng `RealizedPnlOptions`/`RealizedPnlMath`/hằng số `Domain/MasterAlerts` nó dùng.
- Cho spec 015: `BoDieuChinhGiaTheoQuyen`, `FileNguonSuKienQuyen`, `INguonLichChotQuyen`, `FireAntLichChotQuyenClient`, `FireAntOptions`, `SuKienQuyenOptions` (gỡ cổng "sắp chốt quyền" của V1 nhưng giữ client).
- Bảng dùng chung: `Stocks`, `MarketIndices`, `SectorDefinitions`, `Users`, `Watchlists`, `WatchlistItems`, `JobRunStatuses`, `NhatKyGiaoDich`, `NhatKyTinHieuBoQua`, `ThietBiNhanThongBao`; bảng V3: `TinHieuV3`, `ViTheV3`, `ChiSoNgayV3`.

## Assumptions

- "Gỡ V1" theo nghĩa owner đã dùng ở spec 011: xoá sạch đường chạy, không ẩn tạm.
- Kết luận đo dựa trên luật bán mượn của V3 (KB1/KB2); V1 chưa từng có luật bán riêng sau spec 011, nên không có cách đo nào công bằng hơn.
- Không đổi luật, cổng hay dữ liệu của V3.
- Owner tự chạy `ship-all.ps1` và tự build/cài app mobile.
