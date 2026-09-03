# SPEC (BA prep): Quy hoạch lại chỉ báo theo Playbook

> Artifact chuẩn bị **trước** `/speckit-specify` (constitution §"Quy trình Spec Kit & tài liệu").
> Trạng thái: **Shipped — branch `004-indicator-playbooks`**. Tất cả Q1–Q6 đã chốt.
> Ngày soạn: 2026-08-17. Nguồn: đọc code trên disk (constitution §I).

---

## 0. Bối cảnh — dây bị đứt ở đâu (đã verify trên code)

Màn hình **Phân tích chỉ báo** (`mobile/lib/screens/criteria_screen.dart`) hiển thị 25 dòng: 10 chỉ báo đơn + 6 bundle + 9 tiêu chí SmartMoney. Điểm của **16 dòng kỹ thuật** không tham gia chọn Top hay bắn Telegram VIP.

| # | Sự thật | Entry code |
|---|---------|-----------|
| 1 | `BuyDecisionEngine` chỉ nhận `ISignalAnalyzer`; không đọc RSI/MACD/Ichimoku/Stoch/ADX/Bollinger/VWAP từ bộ chấm chỉ báo | `BuyDecisionEngine.cs:39` |
| 2 | `ScoreIndicators()` chỉ dùng cho hiển thị (`StockService.cs:55`, API live) và hậu kiểm | `DailyCriterionScoringRunner.cs:251` |
| 3 | Hậu kiểm chạy **sau** chọn Top (`RunAfterAnalysisAsync`) → là thước đo, không phải đầu vào | `DailyAnalysisRunner.cs:393` |
| 4 | Vòng trọng số `CriterionWeights → AdaptiveScoringProfile` chỉ map **9 tiêu chí SmartMoney**; 16 dòng kỹ thuật `TryGetValue` fail → `continue` | `AdaptiveScoringProfile.cs:35,66` |
| 5 | ML ranker: 11 feature không có điểm chỉ báo nào; `atr_norm` / `dist_ma20_norm` tính độc lập | `OpportunityRankFeatures.cs:8` |
| 6 | Telegram VIP lấy từ `DailyOpportunity` (Buy Score / Top), không từ criterion score | — |

### Ba điều KHÔNG được suy diễn quá (đã sửa từ bản review đầu)

1. **Volume / MA vẫn ảnh hưởng Top** — qua `ISignalAnalyzer` trong `BuyDecisionEngine` (volume ratio, MA stack, breakout volume, flatBox). Màn hình đang đo một **bản sao song song** (RSI14, EMA, bundle trung bình cộng), không phải cùng tín hiệu. Mệnh đề đúng: *"chỉ báo trên màn hình không chọn cổ phiếu"*. Mệnh đề sai: *"volume/trend không tham gia chọn mã"*.
2. **9 tiêu chí SmartMoney không phải dead code** — chúng nối lại Buy Score qua `WeightMultiplier` / `IsActive`, badge Giữ/Loại có thể đổi điểm ngày sau. Khuyết tật là **vòng lặp thiên lệch** (đo chính thành phần đã chọn mã), không phải vô tác dụng. Dead code là 16 dòng kỹ thuật.
3. **6 bundle không cùng một tội** — xem §5.

---

## 1. Bốn khuyết tật của thước đo hiện tại

| Mã | Khuyết tật | Bằng chứng |
|----|-----------|-----------|
| D1 | **Đo sai quần thể.** `RequireTrendSetup: false` → chấm accuracy trên toàn universe, fallback đáy 20 phiên khi không có nền chuẩn. Quần thể đo khác hẳn quần thể Top chọn ra | `appsettings.json:107`, `DailyCriterionScoringRunner.cs:245` |
| D2 | **Một thước cho mọi chỉ báo.** Tất cả bị chấm bằng cùng `MeasureOutcome` (setup trend T+2.5, MFE ≥3%, RS vs VN). ATR dự báo *biến động*, ADX dự báo *có trend hay không* — ép thành Bullish/Bearish rồi đo bằng thước hướng giá → hội tụ về baseline | `TrendSetupEvaluator.cs:62` |
| D3 | **Bundle trung bình cộng + ngưỡng 60 = mù.** Bundle = average thành phần, +10 khi đồng thuận → điểm dạt 60–75 → gần như luôn vượt `MinScoreForEvaluation: 60` → sample cao, sức phân biệt ~0 | `IndicatorBundleScorer.cs:47`, `appsettings.json:104` |
| D4 | **Score trộn hai nghĩa.** `88` = "RSI quá bán + hồi" (Bullish), `85` = "RSI quá mua + yếu" (Bearish). Số cao = *tín hiệu rõ*, không phải *nên mua*. UI sort giảm dần + tô xanh/đỏ theo ngưỡng 55/45 → đọc như "cao = tốt" | `TechnicalIndicatorAnalyzer.cs:52-55`, `criteria_screen.dart:481` |

**D5 (phân loại):** taxonomy Mới → Trung cấp → Nâng cao → Chuyên nghiệp → Tổ chức là taxonomy trình độ, không trả lời được câu hỏi giao dịch *"khi tôi đánh breakout nền chặt thì chỉ báo nào đáng tin?"*.

---

## 2. Phạm vi đã chốt

### Trong phạm vi

- Thêm chiều **Playbook** vào hậu kiểm chỉ báo: accuracy đo theo `(criterion × playbook × marketPhase)` thay vì global.
- **Outcome + baseline riêng theo playbook** (horizon, target, mức vô hiệu hóa).
- **3 playbook** — mỗi cái đã có cổng tồn tại trong code.
- Gỡ 3 bundle "trình độ"; giữ VSA / POC+Delta / SMC làm **thành phần trong playbook**, không phải hàng xếp hạng độc lập.
- UI: tab theo playbook, tách trục hướng và trục độ rõ, sửa typo.

### Ngoài phạm vi (quyết định có chủ đích)

| Hạng mục | Lý do loại |
|---------|-----------|
| Playbook **Momentum tiếp diễn T+10, MFE ≥6%** | Top/VIP đang horizon T+2.5. Đây là **sản phẩm mới**, không phải quy hoạch lại màn hình. Để phase 2 sau khi 3 đường trên có số |
| **Cộng điểm chỉ báo thẳng vào Buy Score** | Vi phạm rủi ro hồi quy điểm; constitution §IV. Chỉ nối khi có edge chứng minh, và nối qua ML feature / veto có trần |
| **Bật `RequireTrendSetup: true` toàn cục** | Xem §3.4 — mâu thuẫn với chính luận playbook |

---

## 3. Ngữ nghĩa điểm mới

### 3.1 Tách hai trục (sửa D4)

Thay `(Score 0–100, Bias)` trộn nghĩa bằng hai trục độc lập:

- **Direction** — `Bullish` / `Bearish` / `Neutral` (giữ nguyên `PatternBias`).
- **Clarity** (độ rõ tín hiệu) 0–100 — "chỉ báo đang nói to đến mức nào", **không** hàm ý nên mua.

UI không được sort trộn hai chiều này thành một cột "cao = tốt".

### 3.2 Chiều Playbook (sửa D1 + D5)

Nhân đúng pattern `Phases` đã có trong `CriterionAccuracySnapshot` / `StockCriterionDetail`:

- Thêm `PlaybookId` vào bản ghi chi tiết và snapshot.
- Accuracy / edge / reliability tính theo `(criterion × playbook × marketPhase)`.
- **Baseline riêng cho từng playbook**: hit rate của quần thể playbook đó *khi chưa lọc bằng chỉ báo*. Không có baseline riêng thì `edge` vô nghĩa.

Đây là thay đổi đem lại giá trị lớn nhất trên mỗi dòng code.

### 3.3 Outcome riêng theo playbook (sửa D2)

`MeasureOutcome` nhận tham số playbook: horizon + target MFE + mức vô hiệu hóa theo đúng đường đánh. Bỏ "một thước cho tất cả".

### 3.4 KHÔNG bật `RequireTrendSetup` như công tắc toàn cục

`HasValidTrendSetup` (`TrendSetupEvaluator.cs:37`) = flatBox hợp lệ + `GainFromBoxTop ≤ MaxGainFromBase` (chống FOMO) + `MeetsSessionEntryBar` + (`IsBreakoutConfirmed` | `DarvasBreakout` | `IsShakeoutFromBase`).

Bật global → **mọi** chỉ báo bị đo trên sân breakout/shakeout. RSI hồi MA20 và RSI < 30 bắt đáy sẽ trông tệ hơn — không vì vô dụng mà vì **đo nhầm sân**. Đúng là biểu hiện của D2, không phải cách chữa D1.

Làm thẳng chiều playbook thì đo đúng sân ngay, không cần bước trung gian gây lệch.

> **Ghi nhận latent inconsistency:** `CriterionAccuracySettings` default trong code là `RequireTrendSetup = true` (`AnalysisResults.cs:8`) nhưng `appsettings.json:107` đặt `false`. Cần một quyết định ghi rõ, không để lệch âm thầm.

---

## 4. Ba playbook

| ID | Cổng đã có trong code | Chỉ báo xác nhận (ứng viên) | Horizon | Target |
|----|----------------------|----------------------------|---------|--------|
| `breakout-darvas` | `hasFlatBoxBreakout` / `DarvasBreakout` (`BuyDecisionEngine.cs:78`) | Volume ratio, ATR expansion, dist-MA20, VSA spread/vol | T+2.5 | MFE ≥3%, đáy hộp còn nguyên |
| `pullback-ma20` | `hasMaStack` + **không** breakout (`BuyDecisionEngine.cs:69`) | RSI 40–55 hồi, Stoch %K cross %D, %B ≤0.3, VWAP | T+5 | MFE ≥4% |

> Playbook `reversal-bounce` **đã gỡ bỏ** cùng toàn bộ hệ sóng hồi — spec [`008-remove-reversal-bounce`](../../../specs/008-remove-reversal-bounce/spec.md).

---

## 5. Xử lý 6 bundle — không dàn hàng

| Bundle | Nội dung | Quyết định |
|--------|---------|-----------|
| `BundleBeginner` (EMA+RSI+Vol) | Bản sao chồng lấn singles; `MovingAverage`+`Volume` lặp 3–4 lần | **Gỡ** |
| `BundleIntermediate` (EMA+Vol+ATR) | như trên | **Gỡ** |
| `BundleAdvanced` (VWAP+EMA+Vol+ATR) | như trên | **Gỡ** |
| `BundleProfessional` (Wyckoff + VSA) | Tín hiệu **mới**: spread/volume VSA (`IndicatorBundleScorer.cs:175`) | **Giữ nội dung**, chuyển thành thành phần playbook |
| `BundleInstitutional` (Vol Profile + VWAP + Delta) | Tín hiệu **mới**: POC, Delta (`:201`, `:238`) | **Giữ nội dung**, chuyển thành thành phần playbook |
| `BundleSmartMoneyConcept` (SMC + Vol + VWAP) | Tín hiệu **mới**: BOS / liquidity sweep (`:265`) | **Giữ nội dung**, chuyển thành thành phần playbook |

Bundle còn lại **không dùng trung bình cộng** (sửa D3): dùng gate/veto — thiếu một thành phần thì trả `Neutral`, không kéo điểm về 65.

---

## 6. UI (`criteria_screen.dart`)

- Tab theo playbook; mỗi tab hiện đúng bộ chỉ báo của nó + edge so với **baseline của chính playbook đó**.
- Bỏ cách đọc "cao = nên mua": tách cột hướng và cột độ rõ (sửa D4 ở tầng hiển thị).
- Sửa typo `Rũi ro` → `Rủi ro` (`criteria_screen.dart:408`).
- Gỡ 3 bundle trình độ khỏi bảng xếp hạng.

---

## 7. Quyết định đã chốt (Q1–Q6)

> Xem bảng Decisions đầy đủ trong [`plan.md`](./plan.md). Giữ lại đây làm dấu vết lý do.

- **Q1 — Gán playbook độc quyền hay đa nhãn?** Một mã có thể vừa `hasMaStack` vừa breakout. Đa nhãn thì mẫu bị đếm trùng; độc quyền thì cần thứ tự ưu tiên. → **CHỐT: độc quyền, ưu tiên `breakout-darvas` > `pullback-ma20`, không khớp → `unclassified` (`reversal-bounce` đã gỡ, spec 008).** Hệ quả: `PlaybookId` chỉ là cột trên `StockCriterionDetails` (phụ thuộc hàm vào khóa), nhưng phải vào composite key của 2 bảng accuracy.
- **Q2 — Playbook classifier nằm ở đâu?** `hasFlatBoxBreakout` / `hasMaStack` hiện là biến cục bộ trong `BuyDecisionEngine.Evaluate`. Expose qua `BuyDecisionEvaluation`, hay tính lại trong `DailyCriterionScoringRunner`? Constitution §V cấm bịa abstraction song song khi Domain service đã sở hữu concern.
- **Q3 — Quần thể `pullback-ma20`** chưa có cổng đặt tên. Định nghĩa chính xác "uptrend + không breakout" là gì (MA stack strictness nào, loại trừ setup zone Darvas không)?
- **Q4 — Migration/backfill:** backfill `PlaybookId` cho snapshot lịch sử hay chỉ tính tiến về trước? Ảnh hưởng thời điểm có đủ mẫu để kết luận.
- **Q5 — Ngưỡng `MinScoreForEvaluation`** có nên theo playbook không, sau khi bundle hết trung bình cộng?
- **Q6 — `RequireTrendSetup`** chốt giá trị nào sau khi có chiều playbook (giữ `false` toàn cục vì playbook đã lọc, hay bỏ hẳn cờ)?

---

## 8. Success criteria

- **SC-001**: Mỗi playbook có baseline riêng và ≥30 mẫu/tiêu chí trong cửa sổ rolling trước khi bất kỳ badge Giữ/Loại nào được hiển thị.
- **SC-002**: Ít nhất 1 chỉ báo trong mỗi playbook cho `edge > 3%` so với baseline **của chính playbook đó**, ổn định ≥2 tuần — điều kiện cần trước khi bàn tới nối ML/veto.
- **SC-003**: Bảng xếp hạng không còn hai dòng chồng lấn >50% thành phần.
- **SC-004**: Buy Score, Top và Telegram VIP **không đổi hành vi** sau khi land spec này (thay đổi thuần đo lường + hiển thị). Regression: so số lượng và thành phần Top trước/sau trên cùng ngày dữ liệu.

---

## 9. Guardrails

- Constitution §II: đây là đổi **ngữ nghĩa điểm** → phải qua `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` trước `/speckit-implement`.
- Constitution §IV: cập nhật `docs/domain/buy-decision.md` trong **cùng change set**.
- Constitution §V: thay đổi tối thiểu xâm lấn — không refactor lân cận.
- Mọi thay đổi hành vi phải có cờ config để rollback.
- Backend xong → `backend/restart-api.ps1`. Ship → `.\scripts\ship-all.ps1 -Message "..."`.

---

## 10. Thứ tự thực thi đề xuất

1. `/speckit-specify` + `/speckit-clarify` (Q1–Q6) → cập nhật `docs/domain/buy-decision.md`.
2. Chiều `PlaybookId` + outcome/baseline riêng theo playbook (backend + migration).
3. Gate/veto thay trung bình cộng; gỡ 3 bundle trình độ.
4. UI tab playbook + tách hai trục + typo.
5. **Chỉ khi SC-002 đạt**: nối ML feature vào ranker Top, hoặc veto có trần theo khuôn `vip-deepseek-veto`.
