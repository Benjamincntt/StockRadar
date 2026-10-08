# StockRadar V2 — Máy nhận kịch bản (Scenario Engine)

> **Ngày tạo:** 29/09/2026
> **Trạng thái:** APPROVED (đã được chủ dự án duyệt)
> **Phạm vi:** Tài liệu này thay thế `docs/features/indicator-scenario-engine/spec.md` và `docs/features/indicator-playbooks/spec.md`
>
> **Đã land 2026-09 (đọc trước khi dùng):** V2 chạy **song song với V1** — V1 (`DailyAnalysisRunner` → `DailyOpportunities`) **KHÔNG bị xóa**, vẫn cấp Home Top + VIP alerts. Trọng số ranker thực tế (`appsettings:XepHang`): RS .30 · Sector .20 · Trigger .20 · Regime .10 · R:R .10 · Confluence .10. Pha 3 chạy **16:00** (`Pha3DoLuongJob`). API thực tế: `GET /kich-ban/xep-hang` + `GET /stocks/{symbol}/kich-ban` + `GET /hieu-qua/*`.
>
> **⚠️ Sự thật chuẩn luồng V2 as-is nằm ở [`domain/pipeline-jobs.md`](../../domain/pipeline-jobs.md#luồng-v2-scenario-engine--sự-thật-chuẩn-duy-nhất) — canon duy nhất.** File này là **lịch sử thiết kế** (kế hoạch gốc đã duyệt); khi hai bên lệch nhau → tin canon pipeline-jobs (nó đối chiếu code trực tiếp). Đổi hành vi V2 → sửa canon trước, chỉ cập nhật spec này nếu thay đổi cả tầm nhìn thiết kế.

---

## §0. Tổng quan phiên bản

- V2 là bản cập nhật lớn: chuyển từ "hệ thống tìm mã thỏa điều kiện" sang "hệ thống theo dõi câu chuyện từng mã"
- BuyDecisionEngine hiện tại (7 cổng veto liên tiếp) bị THAY THẾ — không còn quyền chặn mã *(kế hoạch gốc; thực tế land 2026-09: V1 vẫn chạy song song với 3 cổng Top — FOMO · Unfavorable · sóng ngành; 10/2026 thêm cổng chia chác FireAnt thành 4 cổng — xem `domain/buy-decision.md`)*
- 13 chỉ báo kỹ thuật được đưa trở lại với vai trò QUYẾT ĐỊNH (không chỉ hiển thị)
- Thay đổi này giải quyết vấn đề: cả tháng không có mã nào lọt Top do cổng quá chặt

## §1. Kiến trúc Pipeline

```
~1500 mã toàn sàn
      │
      ▼ SƠ TUYỂN (1 lần/ngày, trước phiên 08:30)
      │ • Avg20Value ≥ 10 tỷ VND (có thể điều chỉnh 5-20 tỷ)
      │ • MarketCap ≥ 500 tỷ
      │ • Lịch sử ≥ 250 phiên
      │ • Không thuộc diện hạn chế/kiểm soát giao dịch
      │
    ~70 mã
      │
      ▼ SCENARIO ENGINE
      │ • Pha 1 (trước phiên): đánh giá Bối cảnh + Hình thái → xác định FORMING
      │ • Pha 2 (trong phiên, mỗi 1 phút): kiểm tra Cò kích hoạt → TRIGGERED
      │ • Pha 3 (sau phiên): cập nhật state, lưu snapshot, đo outcome
      │
    Scenario Candidates (TRIGGERED)
      │
      ▼ OPPORTUNITY RANKER
      │ • RS (30%) + Sector (20%) + Trigger quality (20%) + Regime (10%) + R:R (10%) + Confluence (10%)  ← thực tế appsettings:XepHang
      │ • KHÔNG có quyền veto — chỉ xếp hạng
      │
    Top 5
      │
      ▼ ALERT (kiểm tra giá realtime còn trong vùng Entry → bắn Telegram)
```

## §2. Ba pha hoạt động

### Pha 1 — Trước phiên (08:30, chạy 1 lần/ngày)
- Sơ tuyển: ~1500 → ~70 mã
- Đánh giá Bối cảnh + Hình thái cho 70 mã (dùng nến ngày)
- Phân loại trạng thái: WATCHING / FORMING
- Với mã FORMING: xác định điều kiện trigger cụ thể (mức giá, ngưỡng volume, RSI level...)
- Với mã đang HOLDING: xác định điều kiện sell trigger

### Pha 2 — Trong phiên (09:00–14:45, mỗi 1 phút)
- Chỉ theo dõi: ~10-15 mã FORMING + ~3-5 mã HOLDING ≈ 20 mã
- Mỗi phút: lấy giá + volume realtime → so sánh điều kiện trigger đã tính sẵn
- Khi TRIGGERED:
  - Tính Entry/SL/TP1/TP2 (dùng ATR + structure tại thời điểm đó)
  - Tính Position size
  - Bắn Telegram NGAY
  - Lưu snapshot toàn bộ chỉ báo tại thời điểm trigger
  - Chuyển trạng thái

### Pha 3 — Sau phiên (16:00, chạy 1 lần — `Pha3DoLuongJob`)
- Cập nhật nến ngày vào history
- Lưu trạng thái cuối ngày
- Đo outcome T+1/T+2/T+3 cho các trigger đã bắn trước đó
- Chuẩn bị data cho Pha 1 ngày mai

## §3. Năm kịch bản

### 3.1. Nổ hướng lên (Breakout) — BUY
**Câu chuyện:** Cổ phiếu tích lũy trong hộp/nền giá, volume teo dần, rồi phá đỉnh với volume nổ.

| Vai trò | Chỉ báo | Điều kiện |
|---|---|---|
| Bối cảnh | EMA20, EMA50, ADX | EMA20 > EMA50 VÀ ADX > 25 |
| Hình thái | Bollinger, Volume, Darvas/Base | Bollinger co hẹp + Volume teo < 0.7× TB20 |
| Cò kích hoạt | Giá, Volume, MACD | Giá vượt đỉnh hộp + Volume > 1.5× TB20 + MACD histogram mở rộng |
| Rủi ro/Thoát | ATR, Đáy hộp | SL = đáy hộp − 0.5×ATR, TP1 = chiều cao hộp × 1 |

### 3.2. Hồi về hỗ trợ (Pullback) — BUY
**Câu chuyện:** Xu hướng tăng mạnh, giá tạm lùi về EMA20/VWAP rồi bật lên.

| Vai trò | Chỉ báo | Điều kiện |
|---|---|---|
| Bối cảnh | EMA20, EMA50, EMA200 | EMA20 > EMA50 > EMA200 |
| Hình thái | Giá, RSI, Volume | Giá chạm/gần EMA20 + RSI 40–50 + Volume bán thấp |
| Cò kích hoạt | RSI, MACD, Giá | RSI bật lên từ 40–50 + MACD histogram giảm rồi tăng + Close trên EMA20 |
| Rủi ro/Thoát | ATR, EMA50 | SL = EMA50 − 0.5×ATR, TP1 = đỉnh cũ |

### 3.3. Quét thanh khoản (Liquidity Sweep) — BUY
**Câu chuyện:** Giá quét xuống dưới đáy nền (ăn stop-loss) rồi giành lại. Smart Money gom hàng.

| Vai trò | Chỉ báo | Điều kiện |
|---|---|---|
| Bối cảnh | EMA, Xu hướng | Đang trong xu hướng tăng hoặc đi ngang tích lũy |
| Hình thái | Giá, Đáy nền | Giá xuyên qua đáy nền/hộp trong phiên |
| Cò kích hoạt | Giá đóng cửa, Volume, VWAP | Close GIÀNH LẠI trên đáy nền + Volume spike + Giá trên VWAP |
| Rủi ro/Thoát | ATR, Đáy quét | SL = đáy quét − 0.3×ATR |

### 3.4. Kiệt sức (Exhaustion) — SELL (Bán 50%)
**Câu chuyện:** Đã tăng mạnh, hết đà: volume climax, RSI phân kỳ âm, giá quá xa EMA20.

| Vai trò | Chỉ báo | Điều kiện |
|---|---|---|
| Bối cảnh | Giá, EMA20 | Đã tăng > 8% từ entry VÀ cách EMA20 > 2×ATR |
| Hình thái | RSI, Volume, MACD | RSI > 75 + Volume climax (> 2.5× TB20) + MACD histogram giảm |
| Cò kích hoạt | RSI phân kỳ, Bollinger | RSI phân kỳ âm HOẶC close dưới Bollinger trên |
| Hành động | — | Bán 50% vị thế |

### 3.5. Gãy nền (Breakdown) — EXIT (Bán 100%)
**Câu chuyện:** Phá vỡ đáy nền, volume bán mạnh, mất EMA20/VWAP. Không còn lý do giữ.

| Vai trò | Chỉ báo | Điều kiện |
|---|---|---|
| Bối cảnh | Giá, Đáy nền | Đang giữ vị thế |
| Hình thái | Giá, EMA20, VWAP | Xuyên đáy nền HOẶC mất EMA20 HOẶC mất VWAP |
| Cò kích hoạt | Volume, MACD | Volume bán > 1.5× TB20 + MACD histogram âm + Close dưới đáy nền |
| Hành động | — | Bán 100% vị thế |

## §4. Bốn vai trò chỉ báo

| Vai trò | Trả lời câu hỏi | Tần suất | Chỉ báo thuộc vai trò |
|---|---|---|---|
| Bối cảnh | "Mã này có đáng để ý không?" | 1 lần/ngày | EMA, ADX, Ichimoku, VNINDEX, Sector |
| Hình thái | "Đang có setup đẹp không?" | 1 lần/ngày | Bollinger, Volume contraction, Darvas/Base, VSA, POC |
| Cò kích hoạt | "BÂY GIỜ có phải lúc hành động?" | Mỗi 1 phút | Breakout price, Volume expansion, MACD histogram, RSI cross, VWAP reclaim |
| Rủi ro/Thoát | "SL/TP ở đâu, khi nào chạy?" | Mỗi 1 phút (sell side) | ATR, Resistance, RSI exhaustion, Volume climax, MACD deterioration |

**Luật chống đếm trùng:** Mỗi chỉ báo chỉ đóng 1 vai trò trong 1 kịch bản. Không dùng cùng 1 chỉ báo để vừa tính Bối cảnh vừa tính Trigger.

## §5. ScenarioResult — đầu ra của Scenario Engine

Mỗi mã × mỗi kịch bản → 1 ScenarioResult:

```
ScenarioResult {
    Ma: string              // "HPG"
    KichBan: enum           // NoHuongLen, HoiHoTro, QuetThanhKhoan, KietSuc, GayNen
    TrangThai: enum         // WATCHING, FORMING, TRIGGERED, HOLDING, TAKE_PROFIT, INVALIDATED, EXIT
    BoiCanh: bool           // ✓ hay ✗
    HinhThai: bool          // ✓ hay ✗
    CoKichHoat: bool        // ✓ hay ✗
    MucHoanThien: decimal   // 0-100%
    Entry: (decimal, decimal) // vùng vào lệnh
    SL: decimal             // dừng lỗ
    TP1: decimal            // chốt lời 1
    TP2: decimal            // chốt lời 2
    Invalidation: string    // điều kiện hủy
    Evidence: list[string]  // bằng chứng từng điều kiện
    ThoiGianTrigger: DateTime? // thời điểm kích hoạt (nếu có)
    Snapshot: dict          // giá trị mọi chỉ báo tại thời điểm trigger
}
```

## §6. State Machine (Vòng đời kịch bản)

```
WATCHING ──(đạt bối cảnh + hình thái)──→ FORMING
FORMING  ──(đạt cò kích hoạt)──────────→ TRIGGERED
TRIGGERED ──(vào lệnh)─────────────────→ HOLDING
HOLDING  ──(chạm TP)──────────────────→ TAKE_PROFIT
HOLDING  ──(chạm SL/invalidation)─────→ INVALIDATED
HOLDING  ──(sell scenario trigger)────→ EXIT
```

Quan trọng:
- Không bỏ qua trạng thái. FORMING phải được lưu lại (để biết setup hình thành bao lâu)
- Một mã có thể có NHIỀU ScenarioResult song song (ví dụ: MBB vừa FORMING Nổ hướng lên, vừa FORMING Hồi hỗ trợ)
- WATCHING không cần lưu DB (chỉ là mặc định khi chưa đạt gì)

## §7. Opportunity Ranker

Chỉ chạy trên các ScenarioResult có trạng thái TRIGGERED. KHÔNG có quyền veto.

### Tiêu chí xếp hạng:

| # | Tiêu chí | Trọng số | Nguồn dữ liệu | Ghi chú |
|---|---|---|---|---|
| 1 | RS (sức mạnh tương đối vs VNINDEX) | 30% | RS5, RS percentile | Đã có sẵn |
| 2 | Sector (sóng ngành) | 20% | SectorWaveService | Đã có sẵn |
| 3 | Chất lượng trigger | 20% | Volume ratio, MACD strength, RSI position | MỚI — tính từ ScenarioResult |
| 4 | Pha thị trường (Regime) | 10% | MarketPhaseClassifier | Đã có sẵn |
| 5 | Tỷ lệ Lãi/Lỗ (R:R) | 10% | (TP1 - Entry) / (Entry - SL) | MỚI — tính từ ScenarioResult |
| 6 | Nhiều kịch bản đồng thời (Confluence) | 10% | Đếm số scenario TRIGGERED cùng mã | MỚI |

### "Chất lượng trigger" đo bằng:
- Volume ratio: 3x TB = điểm tối đa, 1.5x = điểm tối thiểu
- MACD histogram: mở rộng mạnh = cao, vừa đủ = thấp
- RSI position: 55-65 = đẹp (breakout), >75 = gần quá mua (giảm điểm)
- Bollinger: breakout từ compression = tốt hơn breakout từ trạng thái bình thường

### BuyDecisionEngine cũ:
- KHÔNG còn quyền veto (không còn ResolveTopGateFailure)
- Buy Score cũ (8 thành phần) bị thay thế bởi Opportunity Ranker mới (6 tiêu chí trên)
- Các thành phần trùng lặp (base, breakout, shakeout, volume, wyckoff) bị loại — Scenario Engine đã xử lý

> **Thực tế land 2026-09:** các dòng trên mô tả vai trò của BuyDecisionEngine **trong pipeline V2**. Trong pipeline V1 (vẫn chạy song song), `ResolveTopGateFailure` còn 3 cổng và Buy Score 8 tiêu chí vẫn là thang điểm của Home Top / Watchlist.

## §8. Sell Side — chạy riêng

```
Vị thế đang giữ (HOLDING)
      │
      ├── Kịch bản Kiệt sức → trigger → BÁN 50%
      ├── Kịch bản Gãy nền → trigger → BÁN 100%
```

- Sell scenarios KHÔNG đi qua sơ tuyển 70 mã
- Sell scenarios chạy trong Pha 2 (mỗi 1 phút) cùng với Buy triggers
- Luồng bán VIP của V1 chạy độc lập, không nối với V2. Từ 10/2026 luồng này chỉ còn luật rút từ mốc + phân phối, xem [`vip-sell-exit-simplify`](../vip-sell-exit-simplify/spec.md).
- Luồng bán V2 đã sửa lỗi (L1–L4) 10/2026: không lọc theo ngày, truyền đúng giá vào, chặn lặp bằng trạng thái vị thế (`ThoiGianBanNua`/`ThoiGianThoatHet`/`CanhBaoDaGui`), kiem T+2.5. Chi tiết: [`v2-sell-fix`](../v2-sell-fix/spec.md).
- **Từ 2026-10-08 (phương án B):** Pha 2 thêm theo dõi mức giá SL/TP1/TP2 **trước** kiểm tra chỉ báo Kiệt/Gãy. Dùng chung bộ cột trạng thái. Pha 3 ưu tiên giá thoát Pha 2 (`GiaBanNua`/`GiaThoatHet`). Chi tiết: [`v2-sell-plan-tracking`](../v2-sell-plan-tracking/spec.md).

## §9. Snapshot & Đo lường Outcome

### Snapshot (lưu tại thời điểm TRIGGERED):
- Toàn bộ giá trị 13 chỉ báo
- Trạng thái 4 vai trò (Bối cảnh, Hình thái, Trigger, Risk)
- Entry/SL/TP đã tính
- Market regime, sector state
- Thời gian từ FORMING → TRIGGERED (bao nhiêu phiên?)

### Outcome (đo sau T+1, T+2, T+3):
- Giá thực tế vs TP/SL
- MFE (lãi cao nhất đạt được)
- MAE (lỗ sâu nhất)
- Kịch bản đúng hay sai?

### Mục đích đo lường:
- Tinh chỉnh ngưỡng (nếu trigger quá nhạy → nâng ngưỡng)
- Loại kịch bản yếu (nếu Kịch bản X chỉ đúng 30% → xem xét bỏ)
- KHÔNG dùng làm "giấy phép" chặn kịch bản hoạt động

## §10. Bảng thuật ngữ Việt hóa

| English | Tiếng Việt | Viết tắt trong code |
|---|---|---|
| Scenario Engine | Máy nhận kịch bản | ScenarioEngine / MayNhanKichBan |
| Context | Bối cảnh | BoiCanh |
| Setup | Hình thái | HinhThai |
| Trigger | Cò kích hoạt | CoKichHoat |
| Risk/Exit | Rủi ro/Thoát | RuiRo |
| Breakout | Nổ hướng lên | NoHuongLen |
| Pullback | Hồi về hỗ trợ | HoiHoTro |
| Liquidity Sweep | Quét thanh khoản | QuetThanhKhoan |
| Exhaustion | Kiệt sức | KietSuc |
| Breakdown | Gãy nền | GayNen |
| Reclaim | Giành lại | GianhLai |
| Volume Climax | Volume đạt đỉnh | VolDatDinh |
| Compression | Co hẹp | CoHep |
| Contraction | Teo volume | TeoVolume |
| Invalidation | Điều kiện hủy | DieuKienHuy |
| Opportunity Ranker | Bộ xếp hạng cơ hội | XepHangCoHoi |
| Pre-filter | Sơ tuyển | SoTuyen |
| Position Size | Khối lượng vào lệnh | KhoiLuong |

## §11. Alert format (Telegram)

### Khi BUY trigger:
```
🎯 [Mã] — [TÊN KỊCH BẢN]
━━━━━━━━━━━━━━━━
[Mã] — MUA ĐIỂM 1

Kích hoạt lúc: [giờ]
Giá trigger: [giá]

Bối cảnh: ✓ [mô tả ngắn]
Hình thái: ✓ [mô tả ngắn]
Kích hoạt: ✓ [mô tả ngắn]

💰 Vào lệnh: [range]
🛑 Dừng lỗ: [giá] (-X%)
🎯 TP1: [giá] (+X%)
🎯 TP2: [giá] (+X%)

❌ Hủy nếu: [invalidation]

Evidence:
✓ [điều kiện 1]
✓ [điều kiện 2]
✓ [điều kiện 3]
```

### Khi SELL trigger:
```
⚠️ [Mã] — [BÁN NỬA / BÁN HẾT]
━━━━━━━━━━━━━━━━
Kịch bản: [Kiệt sức / Gãy nền]

Lý do:
• [evidence 1]
• [evidence 2]
• [evidence 3]

Hành động: Bán [50%/100%] vị thế
```

## §12. Những gì thay đổi so với V1

| V1 (hiện tại) | V2 (mới) |
|---|---|
| BuyDecisionEngine 7 cổng veto | Bị loại bỏ — Scenario Engine thay thế *(thiết kế gốc; thực tế hai pipeline chạy song song)* |
| Chỉ báo bị rút khỏi quyết định | Chỉ báo là trung tâm quyết định |
| Chạy 1 lần/ngày | Pha 2 chạy mỗi 1 phút |
| Alert không có Entry/SL/TP | Alert kèm kế hoạch giao dịch đầy đủ |
| Sell mù (không chỉ báo) | Sell có 2 kịch bản chỉ báo |
| Top rỗng cả tháng | Sơ tuyển thoáng + Scenario Engine linh hoạt |
| Buy Score = cổng chặn | Opportunity Ranker = chỉ xếp hạng |
| Không lưu quá trình hình thành | State machine: WATCHING → FORMING → TRIGGERED |

## §13. Ngưỡng cấu hình (tunable — không hardcode)

Tất cả ngưỡng phải nằm trong appsettings, không hardcode trong logic:

```json
{
  "SoTuyen": {
    "MinAvgDailyValueVnd": 10000000000,
    "MinMarketCap": 500000000000,
    "MinHistoryDays": 250
  },
  "KichBan": {
    "NoHuongLen": {
      "MinAdx": 25,
      "MinVolumeRatio": 1.5,
      "MaxVolumeContraction": 0.7,
      "BollingerCompressionThreshold": 0.04
    },
    "HoiHoTro": {
      "RsiPullbackMin": 40,
      "RsiPullbackMax": 50,
      "MaxEma20Distance": 0.02
    },
    "QuetThanhKhoan": {
      "MinVolumeSpike": 2.0,
      "ReclaimMargin": 0.005
    },
    "KietSuc": {
      "MinRsi": 75,
      "MinVolumeClimax": 2.5,
      "MinExtensionAtr": 2.0,
      "MinGainFromEntry": 0.08
    },
    "GayNen": {
      "MinSellVolumeRatio": 1.5,
      "MinMacdNegativeDays": 2
    }
  },
  "XepHang": {
    "RsWeight": 0.25,
    "SectorWeight": 0.20,
    "TriggerQualityWeight": 0.20,
    "RegimeWeight": 0.15,
    "RiskRewardWeight": 0.10,
    "ConfluenceWeight": 0.10,
    "TopCount": 5
  },
  "Pha2": {
    "IntervalMinutes": 1,
    "SessionStart": "09:00",
    "SessionEnd": "14:45"
  }
}
```

## §14. Bất biến (Invariants)

- **B1:** KHÔNG composite scoring kiểu cộng trung bình nhiều chỉ báo thành 1 điểm duy nhất. Scenario Engine nhận diện MẪU (pattern), không tính điểm.
- **B2:** Opportunity Ranker KHÔNG có quyền veto. Nó chỉ xếp hạng trong số các mã đã TRIGGERED.
- **B3:** Fail-open: nếu không lấy được data intraday → bỏ qua tick đó, không crash, không bắn alert sai.
- **B4:** Mỗi chỉ báo chỉ đóng 1 vai trò trong 1 kịch bản (chống đếm trùng).
- **B5:** Snapshot PHẢI được lưu tại thời điểm trigger (không reconstruct sau).
- **B6:** Sell side chạy độc lập, không phụ thuộc sơ tuyển hay Opportunity Ranker.

## §15. Lộ trình triển khai

### Giai đoạn 1: Nền tảng
- Khôi phục 7 công thức thiếu vào IndicatorMath (Ichimoku, Stochastic, ADX, VWAP, VSA, POC, SMC)
- Tạo entity ScenarioResult + migration DB
- Tạo module Sơ tuyển mới

### Giai đoạn 2: Scenario Engine
- Triển khai 3 kịch bản BUY (Nổ hướng lên trước, rồi Hồi hỗ trợ, rồi Quét thanh khoản)
- Triển khai Pha 1 (đánh giá Bối cảnh + Hình thái, chạy trước phiên)
- Triển khai Pha 2 (kiểm tra Trigger mỗi phút)

### Giai đoạn 3: Sell + Ranker + Alert
- Triển khai 2 kịch bản SELL
- Triển khai Opportunity Ranker
- Cập nhật Telegram formatter
- Cập nhật UI (mobile + web)

### Giai đoạn 4: Đo lường + Tinh chỉnh
- Triển khai Pha 3 (outcome measurement)
- Dashboard theo dõi hiệu quả từng kịch bản
- Tinh chỉnh ngưỡng dựa trên data thực tế

---

## §16. Quy ước đặt tên code (tiếng Việt không dấu)

**Quy ước chung:**
- Identifiers (class, method, variable, enum, property): tiếng Việt KHÔNG dấu, PascalCase cho class/method, camelCase cho variable
- Comments: tiếng Việt đầy đủ (có dấu)
- Technical concepts trừu tượng (RSI, MACD, EMA, ADX, ATR, VWAP, Bollinger, Ichimoku, Stochastic): giữ English, nhưng comment giải thích bằng tiếng Việt

### Jobs & Runners

| Tên code | Nghĩa tiếng Việt | Vai trò |
|---|---|---|
| `Pha1TruocPhienJob` | Pha 1 trước phiên | Quartz job chạy 08:30 |
| `Pha1TruocPhienRunner` | Bộ chạy pha 1 trước phiên | Orchestrator: sơ tuyển + bối cảnh + hình thái |
| `Pha2TrongPhienJob` | Pha 2 trong phiên | Quartz job chạy mỗi 1 phút |
| `Pha2TrongPhienRunner` | Bộ chạy pha 2 trong phiên | Kiểm tra cò kích hoạt realtime |
| `Pha3DoLuongJob` | Pha 3 đo lường | Quartz job chạy 16:00 |
| `Pha3DoLuongRunner` | Bộ chạy pha 3 đo lường | Lưu state + đo outcome |
| `TuanLeDuyetKichBanJob` | Tuần lễ duyệt kịch bản | Weekly review T6 15:30 |

### Domain Services

| Tên code | Nghĩa | Vai trò |
|---|---|---|
| `MayNhanKichBan` | Máy nhận kịch bản | Interface chính (IScenarioEngine) |
| `MayNhanKichBanService` | Bộ máy nhận kịch bản | Implementation |
| `SoTuyenService` | Service sơ tuyển | Lọc ~1500 → ~70 mã |
| `KichBanNoHuongLen` | Kịch bản nổ hướng lên | Evaluator cho Breakout |
| `KichBanHoiHoTro` | Kịch bản hồi về hỗ trợ | Evaluator cho Pullback |
| `KichBanQuetThanhKhoan` | Kịch bản quét thanh khoản | Evaluator cho Liquidity Sweep |
| `KichBanKietSuc` | Kịch bản kiệt sức | Evaluator cho Exhaustion (SELL) |
| `KichBanGayNen` | Kịch bản gãy nền | Evaluator cho Breakdown (EXIT) |
| `DanhGiaBoiCanh` | Đánh giá bối cảnh | Method kiểm tra Context |
| `DanhGiaHinhThai` | Đánh giá hình thái | Method kiểm tra Setup |
| `KiemTraCoKichHoat` | Kiểm tra cò kích hoạt | Method kiểm tra Trigger |
| `TinhRuiRo` | Tính rủi ro | Method tính SL/TP |
| `DieuKienTriggerBuilder` | Bộ xây điều kiện kích hoạt | Pre-compute trigger conditions cho Pha 2 |
| `XepHangCoHoiService` | Service xếp hạng cơ hội | Opportunity Ranker V2 |
| `TinhKhoiLuongService` | Service tính khối lượng | Position sizing |
| `BanChupChiBaoBuilder` | Bộ tạo bản chụp chỉ báo | Snapshot builder |
| `GiaiMaTrangThaiKichBan` | Giải mã trạng thái kịch bản | State machine resolver |

### Entities & Value Objects

| Tên code | Nghĩa | Vai trò |
|---|---|---|
| `KetQuaKichBan` | Kết quả kịch bản | Entity chính lưu DB |
| `KetQuaKichBanEntity` | Entity kết quả kịch bản | EF Core entity |
| `BangChupChiBao` | Bản chụp chỉ báo | VO: giá trị 13 chỉ báo tại thời điểm T |
| `KeHoachGiaoDich` | Kế hoạch giao dịch | VO: Entry/SL/TP1/TP2/Invalidation |
| `DieuKienTrigger` | Điều kiện kích hoạt | VO: pre-computed trigger conditions |
| `BangChung` | Bằng chứng | VO: Evidence entry |
| `KetQuaSoTuyen` | Kết quả sơ tuyển | VO: danh sách mã qua sơ tuyển |

### Enums

| Tên code | Nghĩa | Values |
|---|---|---|
| `LoaiKichBan` | Loại kịch bản | NoHuongLen, HoiHoTro, QuetThanhKhoan, KietSuc, GayNen |
| `TrangThaiKichBan` | Trạng thái kịch bản | DangTheoDoi, DangHinhThanh, DaKichHoat, DangGiu, ChotLoi, HuyLenh, ThoatLenh |
| `VaiTroChiBao` | Vai trò chỉ báo | BoiCanh, HinhThai, CoKichHoat, RuiRo |
| `LoatHanhDong` | Loại hành động | MuaDiem1, MuaDiem2, BanNua, BanHet |

### Properties & Variables (examples)

| Tên code | Nghĩa |
|---|---|
| `mucHoanThien` | Mức hoàn thiện kịch bản (0-100%) |
| `datBoiCanh` | Đạt bối cảnh (bool) |
| `datHinhThai` | Đạt hình thái (bool) |
| `datCoKichHoat` | Đạt cò kích hoạt (bool) |
| `giaVaoLenh` | Giá vào lệnh (entry) |
| `giaDungLo` | Giá dừng lỗ (SL) |
| `giaChotLoi1` | Giá chốt lời 1 (TP1) |
| `giaChotLoi2` | Giá chốt lời 2 (TP2) |
| `dieuKienHuy` | Điều kiện hủy (invalidation) |
| `danhSachBangChung` | Danh sách bằng chứng |
| `thoiGianKichHoat` | Thời gian kích hoạt |
| `diemXepHang` | Điểm xếp hạng (rank score) |
| `tyLeLaiLo` | Tỷ lệ lãi/lỗ (R:R) |
| `soKichBanDongThoi` | Số kịch bản đồng thời (confluence) |
| `chatLuongTrigger` | Chất lượng trigger |
| `danhSachMaSoTuyen` | Danh sách mã qua sơ tuyển |
| `giaTriGiaoDichTrungBinh` | Giá trị giao dịch TB 20 phiên |
| `vonHoaThiTruong` | Vốn hóa thị trường |

### Options (appsettings)

| Tên code | Nghĩa |
|---|---|
| `SoTuyenOptions` | Cấu hình sơ tuyển |
| `KichBanOptions` | Cấu hình ngưỡng từng kịch bản |
| `XepHangOptions` | Cấu hình trọng số xếp hạng |
| `Pha2Options` | Cấu hình pha 2 (interval, session start/end) |

### Controller & API

| Tên code | Nghĩa |
|---|---|
| `KichBanController` | Controller kịch bản |
| `GET /api/v1/kich-ban/xep-hang` | Top kịch bản đã xếp hạng (chạy Pha 1 + `XepHangCoHoi`) |
| `GET /api/v1/stocks/{symbol}/kich-ban` | Kịch bản mới nhất per loại của 1 mã (StocksController) |
| `GET /api/v1/hieu-qua/tom-tat` · `/lich-su` · `/chi-tiet/{id}` | Hiệu quả kịch bản (HieuQuaController) |

### Mapping tên CŨ → MỚI

| V1 (kế hoạch xóa — thực tế 2026-09 vẫn còn, chạy song song) | V2 (mới) |
|---|---|
| `BuyDecisionEngine` | `MayNhanKichBanService` |
| `IBuyDecisionEngine` | `IMayNhanKichBan` |
| `SmartMoneyOpportunitySelector` | `SoTuyenService` |
| `DailyAnalysisRunner` | `Pha1TruocPhienRunner` |
| `OpportunityIntradayMonitorRunner` | `Pha2TrongPhienRunner` |
| `OpportunityPerformanceRunner` | `Pha3SauPhienRunner` |
| `GateFailureClassifier` | (xóa — không thay thế) |
| `TradeStateResolver` | `GiaiMaTrangThaiKichBan` |
| `AdaptiveScoringProfile` | (xóa — không thay thế) |
| `PlaybookClassifier` | (xóa — không thay thế) |
| `CriterionScoringService` | (xóa — không thay thế) |
| `DailyCriterionScoringRunner` | (xóa — không thay thế) |
| `HitCalibrationService` | (xóa — không thay thế) |
| `ShadowAnalysisService` | (xóa — không thay thế) |
| `TopOpportunityVipAlertEvaluator` | `Pha2TrongPhienRunner` (trigger logic) |
| `TopOpportunityVipAlertPublisher` | Sửa thành publisher cho ScenarioResult |
| `VipTelegramMessageFormatter` | Viết lại theo template §11 |
