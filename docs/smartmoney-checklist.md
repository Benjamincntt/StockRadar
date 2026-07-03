# Checklist SmartMoney (Huy Hoàng) trong StockRadar

**Engine thống nhất:** `BuyDecisionEngine` — gates → Buy Score 0–100 → khuyến nghị → điểm vào → Top filter.

| Bước kênh | Rule trong code |
|-----------|-----------------|
| 1. Pha thị trường | `MarketWyckoffPhase`: uptrend/sideway = thuận; downtrend mạnh = chặn CP yếu |
| 2. Ngành mạnh | Xếp hạng ngành theo RS trung bình + Vol ratio; ưu tiên top 5 |
| 3. CP mạnh nhất ngành | RS 5 phiên vs VNINDEX; sort theo score + sector rank |
| 4. Tích lũy | `IsAccumulation` / pha `Accumulation` — cung cạn, đi ngang |
| 5. Breakout + volume | `IsBreakout` và `Vol ratio ≥ 1.5×` TB20 |
| 6–7. Quản trị vốn | Job 4: chỉ theo dõi list đã lọc; cắt lỗ = user (chưa auto) |
| 8. Tránh phân phối | `IsDistribution` → loại ngay |
| Không bắt đáy vì rẻ | Giảm >15% / 20 phiên mà chưa có nền → loại |
| Khỏe hơn VNINDEX | RS ≥ 0 (hoặc breakout mạnh được ngoại lệ nhẹ) |
| Đáy trước thị trường | `Shakeout` → cộng điểm |

**Nền giá (Base Quality Engine):** gate song song **VCP OR Darvas OR Spring** — đọc **`docs/base-price-engine.md` trước** khi sửa `BaseQualityEvaluator.cs`. Tóm tắt: impulse ≥15%, quét 90 phiên, score ≥50; Darvas hộp Close ≤9%; VCP đỉnh thấp/đáy cao; không ép một khuôn AND cũ.

**Điểm pass:** ≥ 60 và **bắt buộc có nền giá** và (**breakout** hoặc **shakeout đáy nền hồi phục**) trong phiên: tăng **>3%**, KL khớp **≥ 800K**, chưa vượt **+10%** so đỉnh nền (FOMO).

**Hai đường vào xu hướng:**
1. **Breakout:** phá đỉnh 20 phiên + Vol×≥1.5 + tăng >3% phiên + KL ≥800K.
2. **Shakeout:** có nền → vài phiên rũ thủng đáy nền (KL thấp) → hồi phục trên đáy + tăng >3% phiên + KL ≥800K.

**Top list:** tối đa 30 mã → `DailyOpportunities` cho ngày giao dịch kế tiếp.

## Buy Score & khuyến nghị

| Khuyến nghị | Điều kiện |
|-------------|-----------|
| **StrongBuy** | Buy Score ≥ 80 + điểm vào **Ready** + pass Top filter |
| **Watch** | Score ≥ 70, hoặc ≥ 60 + điểm vào **Watch** |
| **Avoid** | Fail gate / Late / score thấp |

**Breakdown điểm (tối đa 100):** TT 12 · Ngành 18 · RS 20 · Nền 18 · Breakout 22 · Shakeout 10 · Vol spike 8 · Wyckoff 5 · MA 5.

`SmartMoneyOpportunitySelector` và `SmartMoneyCriterionScorer` đều delegate vào `BuyDecisionEngine` — không tính điểm trùng.

## Điểm vào (trong BuyDecisionEngine.Entry)

| Trạng thái | Ý nghĩa |
|------------|---------|
| **Ready** | Breakout hoặc shakeout + phiên >3% + KL ≥800K — có thể vào |
| **Watch** | Có nền, chưa FOMO — chờ kích hoạt trên đỉnh nền / shakeout đáy |
| **Late** | Vượt +10% so đỉnh nền — tránh FOMO |
| **Invalid** | Không nền / phân phối / thiếu dữ liệu |

**Mức giá:** cắt lỗ dưới đáy nền ×0.98 · kích hoạt đỉnh nền ×1.01 · mục tiêu min(nền+10%, entry+2×biên nền).

**UI:** thẻ **Buy Score** + **Điểm vào** trên trang CP · badge khuyến nghị trên Top Opportunities.

## Master alerts (trong phiên — ưu tiên Lệnh RT)

Chỉ mã **Top cơ hội** · tag `Master` · sắp xếp trước lệnh thường.

| Tín hiệu | Điều kiện |
|----------|-----------|
| **Mua điểm 1** | Phiên +≥4%, KL ≥800K |
| **Mua điểm 2** | Cùng phiên +≥6% (sau điểm 1) |
| **Cắt lỗ điểm 1** | Đỉnh từ mua +≥4% + lệnh bán lớn (treo bán / NN bán) |
| **Cắt hết** | Đỉnh từ mua +≥6.5% + lệnh bán lớn |

Cấu hình: `MasterAlerts` trong `appsettings.json`.

## Review hiệu quả (tự động hàng tuần)

Trang **Hiệu quả Top** (`/performance`) · job Quartz thứ Sáu 15:30 VN.

- Đo **T+2.5** = TB giá đóng T+2 và T+3
- Phân loại: **Tốt** (≥3%), **Ngang** (-1%→3%), **Xịt** (&lt;-1%)
- Nếu tỷ lệ hỏng &gt;45% → đề xuất **Overhaul** bộ lọc Top cơ hội

## Độ tin cậy chỉ báo (tab Chỉ báo)

**Không phải tất cả chỉ báo đều sai** — trước đây cách *đo* tin cậy bị lệch:

| Vấn đề cũ | Hậu quả |
|-----------|---------|
| Tính mọi mã × mọi chỉ báo kể cả điểm yếu (&lt;55) | Phần lớn mẫu bị coi là “dự đoán đi ngang ±0.3%” → hầu hết fail vì CP VN hay biến động &gt;0.3%/phiên |
| Horizon 1 phiên | Không khớp swing (nền / breakout 3+ phiên) |
| Ngưỡng 42% so với ~33% thực tế | Gần như mọi tiêu chí bị đề xuất Remove |

**Cách đo mới (3 phase — trader xu hướng)** — `CriterionAccuracy` trong `appsettings.json`:

**Phase 1 — Setup trend:** chỉ mã có nền + breakout/shakeout (>3%, KL ≥800K) · horizon 5 phiên · điểm ≥60.

**Phase 2 — Outcome swing:** MFE/MAE · RS vs VNINDEX · fail nếu thủng đáy nền.

**Phase 3 — Reliability:** `40% hit + 30% edge + 20% MFE + 10% (1−rũ nền)` · bucket 60–69/70–79/80+ · tách pha TT · Remove khi R&lt;42% và edge&lt;3%.

Cần **chạy lại phân tích** sau phiên để số liệu cập nhật.

## Không có trong data KBS (chưa implement)
- MA20/50/100/200 stack đầy đủ (có thể thêm sau từ `HistoryJson`)
- Xác nhận "quỹ nào mua" — chỉ suy từ giá + volume
