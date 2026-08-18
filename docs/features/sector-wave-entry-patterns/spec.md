# SPEC: Sóng ngành thay xếp hạng ngành + kiểu điểm vào là lựa chọn

> Artifact chuẩn bị cho `/speckit-specify` (constitution §"Quy trình Spec Kit & tài liệu").
> Trạng thái: **Đã land trên nhánh `004-indicator-playbooks`** (backend + mobile + web + docs).
> Ngày soạn: 2026-08-18. Nguồn: đọc code trên disk.

---

## 0. Vấn đề

Checklist điểm vào (`CHECKLIST ĐIỂM VÀO` trên mobile) đang có hai khuyết tật do người dùng chỉ ra:

| Mã | Khuyết tật | Bằng chứng trước khi sửa |
|----|-----------|--------------------------|
| P1 | **Hai kiểu vào lệnh xếp cạnh nhau như hai điều kiện phải-đạt.** `breakout` và `shakeout` là hai *con đường* loại trừ nhau: mã phá nền đi thẳng thì không thể đồng thời rũ đáy nền. Xếp chung một checklist → mã breakout đẹp vẫn hiện "8/10 đạt" với dấu ✗ ở shakeout, và `Confidence` (= tỉ lệ pass) bị kéo xuống oan | `BuyDecisionEngine.BuildEntry()` — 2 lần `AddCheck` riêng |
| P2 | **"Ngành top 5" đo sai thứ.** `sectorRank` là composite RS5 + tổng KL + cap proxy + số mã → đo *sức mạnh trung hạn của ngành*, không trả lời được "hôm nay ngành dầu khí gần trần hết → có sóng ngành". Ngành nhỏ nhưng đang có sóng bị đánh trượt; ngành to ì ạch vẫn được cộng 18đ | `BuildSectorSnapshots` composite + `sectorRank <= TopSectorCount` |

## 1. Quyết định

1. **Bỏ hẳn xếp hạng ngành.** Không còn `TopSectorCount`, `SectorRankWeights`, `SectorSnapshot.Rank`, `CompositeScore`, `SmartMoneyMarketContext.SectorRank/SectorCount`.
2. **Thay bằng sóng ngành**, giá trị hiển thị cho người dùng là **số mã tăng / số mã giảm** trong ngành.
3. **Gộp các kiểu điểm vào thành một mục checklist** — đạt 1 trong 3 là đủ.
4. **Thêm phân kỳ dương RSI** làm kiểu điểm vào thứ 3.

## 2. Sóng ngành — định nghĩa

Đo trên **phiên hiện tại**, per ngành, chỉ tính ngành có ≥ `MinStocksPerSector` (mặc định 3) mã đủ lịch sử. Ngành `Khác` / `N/A` / thiếu mã → **Không sóng**.

| Trục | Đo | Ngưỡng mặc định |
|------|-----|-----------------|
| Độ rộng | tỉ lệ mã tăng trong phiên | ≥ 60% (`MinAdvancerRatio`) |
| Lực | trung vị % thay đổi phiên **hoặc** tỉ lệ mã "gần trần" | ≥ +1.5% (`MinMedianChangePercent`) hoặc ≥ 25% mã tăng ≥ +4% (`MinNearCeilingRatio` / `NearCeilingChangePercent`) |
| Tiền vào | tổng KL phiên / tổng KL trung bình của ngành | ≥ 1.3× (`MinVolumeRatio`) |
| Xác nhận | RS ngành 5 phiên so VNINDEX | > 0 (`MinSectorRs5d`) |

| Trạng thái | Điều kiện | Buy Score `sector` | Nhãn |
|-----------|-----------|--------------------|------|
| `Strong` | đủ cả 4 trục | 18/18 | Sóng ngành mạnh |
| `Emerging` | đủ độ rộng **+ ≥1** trục còn lại | 10/18 | Chớm sóng ngành |
| `None` | còn lại (hoặc thiếu độ rộng) | 0/18 | Chưa có sóng ngành |

Độ rộng là điều kiện **bắt buộc**: thiếu nó thì dù KL cao vẫn là None — tránh nhận nhầm một ngành mà chỉ 1–2 mã kéo.

Ngưỡng nằm trong `appsettings.json → SmartMoney:SectorWave`, chỉnh không cần build.

## 3. Kiểu điểm vào — 3 lựa chọn thay thế nhau

Checklist còn **một** mục `entrypattern` — "Kiểu điểm vào (1 trong 3)", đạt nếu bất kỳ pattern nào khớp; chi tiết liệt kê pattern đang khớp (`Breakout Vol×2.1`, `Shakeout đáy nền + hồi`, `Phân kỳ dương RSI`, hoặc nhiều pattern nối bằng ` + `).

| Pattern | Điều kiện | `EntryPointType` |
|---------|-----------|------------------|
| Phá vỡ nền giá thẳng | `Breakout`/`DarvasBreakout` + phiên kích hoạt + Vol× ≥ `BreakoutMinVolumeRatio` | `Breakout` |
| Shakeout đáy nền + hồi | có nền giá + `IsShakeoutFromBase` + phiên kích hoạt | `Shakeout` |
| Phân kỳ dương RSI | `IsBullishRsiDivergence` + phiên kích hoạt | `Divergence` |

Ưu tiên khi nhiều pattern cùng khớp: Breakout → Shakeout → Divergence (chỉ ảnh hưởng nhãn/headline, không ảnh hưởng điểm).

Cổng Top đổi tương ứng: `!breakout && !shakeout && !divergence` mới xét "chưa kích hoạt".

## 4. Phân kỳ dương RSI — thuật toán

`SignalAnalyzer.IsBullishRsiDivergence` (`SignalType.BullishDivergence`):

- Cửa sổ 40 phiên gần nhất; pivot low = đáy thấp nhất trong bán kính 2 phiên.
- Cặp đáy hợp lệ: cách nhau ≥ 5 phiên, đáy sau nằm trong 6 phiên gần nhất.
- Giá: đáy sau ≤ đáy trước × 1.01 (chấp nhận double-bottom sát nhau).
- RSI(14) tại đáy trước ≤ 45 (đã ở vùng bán mạnh) và RSI đáy sau − RSI đáy trước ≥ 3 điểm.
- Xác nhận: phiên gần nhất là nến tăng, đóng cửa trên đóng cửa đáy sau và trên đáy sau.

Chưa có backtest riêng — pattern này mới, nên `Divergence` dùng chung độ tin cậy với `shakeout` (`ReliabilityFactor(profile, "shakeout", 10)`) cho tới khi có đủ mẫu.

## 5. Ảnh hưởng Buy Score

| Component | Trước | Sau |
|-----------|-------|-----|
| `sector` (18đ) | rank ≤3 → 18; ≤5 → 10; còn lại 0 | Strong 18 / Emerging 10 / None 0 |
| `shakeout` (10đ) | chỉ shakeout | shakeout **hoặc** phân kỳ; nhãn "Shakeout / Phân kỳ" |

Giữ nguyên **id** component (`sector`, `shakeout`) để `AdaptiveScoringProfile` / `CriterionWeights` / lịch sử độ tin cậy trong DB không vỡ. Tổng max điểm không đổi → `NormalizeAdaptiveScore` không đổi thang.

## 6. Ảnh hưởng ML

- `SetupDna` phần 3 đổi từ `Ngành #n` sang token sóng. `ParseSetupDna` đọc **cả hai** dạng → dataset lịch sử vẫn parse được.
- Feature đổi tên `sector_inv_rank` → `sector_wave_inv`; giá trị `1/(1+rank)` với rank 1 = sóng mạnh, 2 = chớm sóng, 3 = không sóng (dữ liệu cũ giữ rank 1..N).
- `dna_shakeout` giờ bao cả phân kỳ (`ClassifyPath` map `Divergence` → `Shakeout`) — giữ nguyên 11 chiều vector để model đã train không phải đổi schema.
- **Cần train lại** `OpportunityRanker` sau khi tích đủ dữ liệu mới: phân bố `sector_wave_inv` khác hẳn phân bố rank cũ.

## 7. Rủi ro / theo dõi

| Rủi ro | Ghi chú |
|--------|---------|
| Số mã lọt Top thay đổi | Cổng `ngành chưa có sóng + RS <2%` thay `ngành rank >5 + RS <2%`. Phiên thị trường đỏ diện rộng → gần như không ngành nào có sóng → Top co lại; đây là hành vi *mong muốn* nhưng cần quan sát vài phiên |
| Phân kỳ chưa backtest | Có thể tăng tín hiệu nhiễu ở pha Unfavorable. Cắt nhanh bằng cách siết `MeetsSessionEntryBar` hoặc tắt tạm bằng cách bỏ `SignalType.BullishDivergence` khỏi `DetectSignals` |
| Model ranker lệch | Xem §6 |

## 8. Kiểm thử

- `backend/StockRadar.Tests/SectorWave/SectorWaveTests.cs` — 4 case: ngành gần trần hết → Strong; ngành đỏ/lưỡng lự → None; đủ độ rộng thiếu tiền/RS → Emerging; ngành < 3 mã → None + "Không đủ mã trong ngành".
- `backend/StockRadar.Tests/SectorWave/RsiDivergenceTests.cs` — 4 case: phân kỳ thật → true; giá và RSI cùng thấp hơn → false; uptrend đều → false; thiếu lịch sử → false.

## 9. Tài liệu liên quan

- [`docs/domain/buy-decision.md`](../../domain/buy-decision.md) — mục "Sóng ngành"
- [`docs/domain/base-price-flatbox.md`](../../domain/base-price-flatbox.md) — nền giá / Darvas dùng cho pattern breakout & shakeout
