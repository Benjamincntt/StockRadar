# Feature Specification: Sector Wave State Engine (sóng ngành giữ trạng thái nhiều phiên)

**Feature Branch**: `007-sector-wave-state-engine`

**Created**: 2026-08-24

**Status**: Draft

**Input**: User description: "Hệ thống coi ngành bùng nổ thứ Sáu thành 'không có sóng' vào thứ Hai chỉ vì 1 phiên hạ nhiệt ngắn hạn — point-in-time blindness. Cần tách 'Xung lực phiên' (Daily Momentum, đo per phiên) khỏi 'Sóng ngành' (Sector Wave Regime, giữ trạng thái Active 3-5 phiên cho tới khi có tín hiệu gãy sóng), theo Hướng 2 (Sector Wave State Cache)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mã cá nhân breakout theo sau sóng ngành không bị chặn oan (Priority: P1)

Ngành Chứng khoán bùng nổ độ rộng (breadth) trong một phiên (ví dụ trần tím diện rộng, khối lượng lớn) — đây là Catalyst. 1–3 phiên sau, một phần mã trong ngành đi ngang/hạ nhiệt nhẹ (Pause bình thường), trong khi một mã khác trong ngành (ví dụ HCM) mới phá nền/breakout riêng. Hiện tại `ClassifyWave` chỉ đo breadth của **đúng phiên đang xét**, nên phiên Pause khiến `advancerRatio` tụt dưới ngưỡng và ngành bị chấm `None` — mã breakout theo sau bị chặn ở gate `!sectorWave.HasWave && rs5 < 2m` ([`BuyDecisionEngine.cs:581`](../../backend/StockRadar.Domain/Services/BuyDecisionEngine.cs)) dù ngành thực chất vẫn trong sóng.

Sau thay đổi, ngành giữ trạng thái **Active** trong một số phiên sau khi Catalyst kích hoạt, cho tới khi có tín hiệu gãy sóng rõ ràng hoặc hết hạn an toàn — gate Top dùng trạng thái này thay vì chỉ nhìn đúng phiên hiện tại.

**Why this priority**: Đây là điểm mù cụ thể vừa phát hiện qua HCM ngày 24/08/2026 (ngành Chứng khoán) — không sửa thì mọi mã breakout theo sau một phiên bùng nổ ngành đều có rủi ro bị gate loại oan trong các phiên "nghỉ" tiếp theo.

**Independent Test**: Dựng lại 2 phiên lịch sử: phiên T-2 ngành X đạt `ClassifyWave` = Strong hoặc Emerging; phiên T-1 và T0 ngành X breadth tụt dưới ngưỡng nhưng không có tín hiệu gãy sóng. Mã Y trong ngành X breakout hợp lệ ở T0. Kỳ vọng: `sectorWave` tại T0 báo `IsActive = true` (kế thừa từ T-2), gate dòng 581 không chặn Y vì lý do ngành.

**Acceptance Scenarios**:

1. **Given** ngành Chứng khoán đạt `Strong` tại phiên T-2 (breadth ≥ ngưỡng, lực + tiền + RS đủ), **When** phiên T-1 và T0 breadth tụt dưới `MinAdvancerRatio` nhưng không phiên nào chạm điều kiện gãy sóng, **Then** `SectorWaveState` tại T0 = **Active** (kế thừa từ T-2, còn hiệu lực).
2. **Given** `SectorWaveState` đang Active nhờ T-2, **When** mã Y (ngành Chứng khoán) breakout hợp lệ tại T0 với RS 5 phiên = +1.91% (< 2%), **Then** gate `BuyDecisionEngine.cs:581` **không** trả `"Ngành chưa có sóng + RS không đủ"` — Y được xét tiếp các gate còn lại như bình thường.
3. **Given** `SectorWaveState` Active từ T-2, **When** không phiên nào giữa T-2 và T0 xảy ra tín hiệu gãy sóng, và số phiên kể từ T-2 vẫn trong hạn TTL an toàn, **Then** trạng thái Active được giữ nguyên, không tự tắt.

---

### User Story 2 - Sóng ngành phải tự tắt khi có tín hiệu gãy sóng thật, không "kẹt Active" vô hạn (Priority: P1)

Nếu ngành đã Active mà sau đó có một phiên **rũ bỏ mạnh** (breadth giảm sâu + khối lượng lớn) — đây là tín hiệu ngành đã đổi pha, không còn là "nghỉ" mà là kết thúc sóng. Trạng thái Active phải tắt ngay từ phiên đó, không chờ hết TTL.

**Why this priority**: Nếu không có cơ chế tắt rõ ràng, US1 sẽ tạo rủi ro ngược — giữ Active mãi cho một ngành đã thực sự đảo chiều, khiến gate Top mất tác dụng bảo vệ (mua mã yếu trong ngành đang sập).

**Independent Test**: Dựng ngành X Active. 3 phiên liên tiếp T-2, T-1, T0 đều có `SectorSnapshot.VolumeRatio < 0.5` (khối lượng ngành dưới 50% trung bình 20 phiên). Kỳ vọng: trạng thái tại T0 = **Inactive**, ghi nhận `FailedOn = T0`, không cần chờ hết TTL.

**Acceptance Scenarios**:

1. **Given** ngành X đang Active, **When** 3 phiên liên tiếp (T-2, T-1, T0) đều có `VolumeRatio < FailureMaxVolumeRatio (0.5)` — tức khối lượng giao dịch ngành dưới 50% trung bình 20 phiên gần nhất, 3 phiên liên tục, **Then** `SectorWaveState` tại T0 = Inactive, `FailedOn = T0`.
2. **Given** chuỗi phiên volume thấp mới chỉ 2/3 (T-1, T0 dưới 50%, T-2 không dưới), **When** tính trạng thái tại T0, **Then** vẫn Active — chưa đủ 3 phiên liên tiếp nên `ConsecutiveLowVolumeSessions = 2`, không tắt.
3. **Given** ngành X Inactive vì đủ 3 phiên volume thấp liên tiếp, **When** phiên T0 ngành X lại đạt `ClassifyWave` = Strong/Emerging (breadth + volume phục hồi), **Then** trạng thái được kích hoạt lại như một chu kỳ Active mới (`ActivatedOn = T0`, `ConsecutiveLowVolumeSessions` reset về 0), không kế thừa lịch sử trước đó.
4. **Given** ngành X Active từ phiên T-N, **When** N vượt `MaxActiveSessions = 20` phiên mà không phiên nào tái xác nhận (`ClassifyWave` ≥ Emerging) và cũng chưa đủ 3 phiên volume thấp liên tiếp để tắt sớm, **Then** trạng thái tự chuyển Inactive tại phiên T-N+20 (hết hạn an toàn ~1 tháng giao dịch, tránh Active vô thời hạn).

---

### User Story 3 - Vận hành nhìn thấy lý do ngành đang Active/Inactive (Priority: P2)

Khi xem chi tiết mã hoặc lý do Top, người vận hành cần biết ngành đang ở trạng thái sóng nào và vì sao (ví dụ "Active — kích hoạt 21/08, còn 2/5 phiên hiệu lực" hoặc "Inactive — gãy sóng 22/08").

**Why this priority**: Không có nhãn giải thích, US1/US2 đúng về logic nhưng khó audit/tin tưởng khi vận hành thực tế nghi ngờ kết quả (như case HCM vừa xảy ra).

**Independent Test**: Gọi API chi tiết mã có `sectorWave`, kiểm tra có trường mô tả trạng thái Active/Inactive kèm ngày kích hoạt hoặc ngày gãy sóng.

**Acceptance Scenarios**:

1. **Given** ngành đang Active nhờ phiên trước, **When** xem `scoreReasons`/chi tiết ngành của một mã thuộc ngành đó, **Then** có dòng giải thích dạng "Sóng ngành — kích hoạt {ngày}, còn hiệu lực" khác với dòng hiện tại (chỉ có breadth phiên hiện tại).

---

### Edge Cases

- Ngành chưa từng Active (mới có breadth đủ lần đầu hôm nay): hoạt động như hiện tại — `ActivatedOn` = hôm nay, không cần lịch sử.
- Ngày phân tích bị chạy lại (re-run) cho một phiên đã qua: `SectorWaveState` phải tính lại đúng theo thứ tự thời gian thực (không dùng giờ hệ thống hiện tại), tránh lệch khi backfill.
- Ngành đủ điều kiện Active giữa kỳ TTL (ví dụ phiên T-1 lại đạt Emerging trong khi đang Active từ T-3): coi là tái xác nhận, gia hạn TTL tính từ phiên tái xác nhận gần nhất, không cộng dồn hai chu kỳ.
- Phiên thiếu dữ liệu (ngành < `MinStocksPerSector` mã đủ lịch sử): giữ nguyên trạng thái Active/Inactive của phiên trước đó (không tính là gãy sóng, không tính là tái xác nhận) — trung lập.
- Đổi tên/gộp ngành giữa các phiên: ngoài phạm vi — giả định `Sector` string ổn định như hiện tại.
- Chuỗi 3 phiên volume thấp bị "gãy" bởi 1 phiên thiếu dữ liệu ở giữa (ví dụ nghỉ giao dịch, hoặc ngành tụt dưới `MinStocksPerSector`): không tính phiên đó vào chuỗi, nhưng cũng không reset — chuỗi tiếp tục đếm bằng các phiên có dữ liệu kế tiếp (coi phiên thiếu dữ liệu là "bỏ qua", không phải "phá vỡ chuỗi").
- Phiên có `VolumeRatio < 0.5` nhưng đồng thời `ClassifyWave` = Strong/Emerging (breadth+lực+RS vẫn tốt dù tiền vào giảm): ưu tiên tái xác nhận — reset `ConsecutiveLowVolumeSessions` về 0 (không cộng vào chuỗi gãy sóng), theo đúng thứ tự ưu tiên FR-002 trước FR-004.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI tính `SectorWaveState` (Active/Inactive) cho mỗi ngành mỗi phiên phân tích, độc lập với `SectorSnapshot.State` (breadth đúng-phiên hiện có, giữ nguyên không đổi).
- **FR-002**: Một ngành CHUYỂN sang Active khi `ClassifyWave` phiên đó trả `Strong` hoặc `Emerging` (dùng đúng logic/ngưỡng hiện tại — không đổi `SectorWaveSettings` hiện hữu).
- **FR-003**: Một ngành đang Active PHẢI giữ Active tối đa `MaxActiveSessions` phiên kể từ lần kích hoạt/tái xác nhận gần nhất, trừ khi bị tắt sớm bởi FR-004.
- **FR-004**: Một ngành đang Active PHẢI chuyển Inactive ngay khi ghi nhận đủ `FailureConsecutiveSessions` (= 3) phiên **liên tiếp** có `SectorSnapshot.VolumeRatio < FailureMaxVolumeRatio` (= 0.5) — tức khối lượng giao dịch ngành dưới 50% trung bình 20 phiên gần nhất (`VolumeRatio` đã dùng đúng nền `signals.GetAverageVolume`, `Lookback = 20`, không tính lại công thức mới).
- **FR-005**: Gate Top tại [`BuyDecisionEngine.ResolveTopGateFailure`](../../backend/StockRadar.Domain/Services/BuyDecisionEngine.cs) dòng 581 PHẢI đổi điều kiện từ `!sectorWave.HasWave` thành `!(sectorWave.HasWave || sectorWaveState.IsActive)` — mã cá nhân vẫn cần RS ≥ 2% nếu cả breadth phiên hiện tại VÀ trạng thái Active đều không có.
- **FR-006**: Ngưỡng `FailureMaxVolumeRatio`, `FailureConsecutiveSessions`, `MaxActiveSessions` PHẢI là config (`appsettings.json`, khối `SmartMoney:SectorWave:Failure` và `SmartMoney:SectorWave:MaxActiveSessions`), không hard-code.
- **FR-007**: `SectorWaveState` PHẢI tính đúng theo trình tự ngày giao dịch thực (dựa vào lịch sử OHLCV theo ngày), để chạy lại/backfill một phiên cũ ra kết quả giống lần chạy gốc — kể cả bộ đếm `ConsecutiveLowVolumeSessions`.
- **FR-008**: Response API mô tả sóng ngành (ví dụ `scoreReasons`, chi tiết mã) PHẢI phân biệt được lý do "có sóng vì breadth phiên hiện tại" và "có sóng vì đang trong chu kỳ Active kế thừa" (US3).
- **FR-009**: `SectorWaveState` PHẢI được **persist** — một bản ghi per (Sector, TradingDate) ghi vào DB ngay trong `DailyAnalysisRunner`, sau bước `BuildSectorSnapshots` mỗi phiên (cần EF Core migration ở giai đoạn implement). Không dùng recompute-on-the-fly.
- **FR-010**: Ngưỡng chốt: `FailureMaxVolumeRatio = 0.5`, `FailureConsecutiveSessions = 3` (phiên liên tiếp), `MaxActiveSessions = 20` (phiên giao dịch, ~1 tháng lịch). Vẫn nên chạy `criteria/reliability-backtest` một lần trước khi bật production để có baseline win-rate tham chiếu (không phải điều kiện chặn ra spec).

### Key Entities

- **SectorWaveState** *(persisted, per Sector + TradingDate)*: `Sector`, `TradingDate`, `IsActive`, `ActivatedOn` (phiên kích hoạt/tái xác nhận gần nhất), `ConsecutiveLowVolumeSessions` (int, đếm chuỗi phiên `VolumeRatio < 0.5` liên tiếp hiện tại), `FailedOn` (nullable, phiên đủ 3 chuỗi gãy sóng gần nhất nếu có). Khác với `SectorSnapshot.State` (chỉ phản ánh đúng phiên hiện tại, giữ nguyên không đổi).
- **SectorWaveFailureSettings**: `FailureMaxVolumeRatio` (0.5), `FailureConsecutiveSessions` (3) — nằm trong khối con mới `SmartMoney:SectorWave:Failure`; `MaxActiveSessions` (20) ở cấp `SmartMoney:SectorWave`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Với kịch bản ngành Active từ phiên Catalyst, 1–3 phiên "nghỉ" liền sau (breadth dưới ngưỡng, không gãy sóng) không làm gate dòng 581 chặn mã breakout hợp lệ trong ngành đó — 100% trường hợp test hồi quy US1.
- **SC-002**: Với kịch bản có đủ 3 phiên liên tiếp `VolumeRatio < 0.5`, trạng thái chuyển Inactive **đúng ngay phiên thứ 3**, không trễ, không sớm — 100% test US2.
- **SC-003**: Backtest `criteria/reliability-backtest` trước/sau thay đổi: số lượng mã pass Top tăng lên (do bớt chặn oan) nhưng win-rate 7 ngày không giảm quá [ngưỡng cụ thể cần thống nhất, ví dụ không giảm hơn 3 điểm % so baseline] — tránh nới gate làm giảm chất lượng Top.
- **SC-004**: Case neo HCM 24/08/2026 (ngành Chứng khoán có phiên breadth mạnh 21/08, RS HCM +1.91%): sau thay đổi, nếu ngành thực tế đủ điều kiện Active kế thừa từ 21/08, HCM không còn bị chặn bởi lý do "Ngành chưa có sóng" (vẫn có thể bị chặn bởi gate khác nếu không đạt).

## Assumptions

- Không đổi `SectorWaveSettings` hiện có (`MinAdvancerRatio`, `MinMedianChangePercent`, `NearCeilingChangePercent`, `MinNearCeilingRatio`, `MinVolumeRatio`, `MinSectorRs5d`) — chỉ thêm lớp trạng thái xuyên phiên bên trên.
- `MaxActiveSessions = 20` phiên là lưới an toàn bắt buộc (đã chốt) — tránh trạng thái Active không có điểm kết thúc nếu không phiên nào từng đủ 3 phiên volume thấp liên tiếp.
- Điều kiện gãy sóng dùng thuần **khối lượng cạn dần** (`VolumeRatio < 0.5` × 3 phiên liên tiếp), không dùng breadth giảm/median âm — đây là lựa chọn có chủ đích: sóng "chết" vì hết tiền vào, không cần đợi một phiên bán mạnh mới coi là kết thúc.
- Việc lưu `SectorWaveState` (persist theo FR-009) là một thay đổi schema nhỏ (bảng mới) — cần qua `efcore-migration-review` ở giai đoạn implement, không thuộc phạm vi spec này.
- Không đổi ngưỡng Buy Score / MinPassScore / gate RS ở dòng 581 (`rs5 < 2m`) — chỉ đổi nguồn xác định `sectorWave.HasWave` hiệu lực.
- Phạm vi chỉ áp dụng cho gate Top cơ hội (`BuyDecisionEngine`); không đổi cách `docs/domain/ma-stack-and-market-phase.md` hay ReversalBounce dùng breadth thị trường chung (khác khái niệm với breadth-per-ngành ở đây).
