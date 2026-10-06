# Spec — Máy kịch bản giao dịch & Snapshot chỉ báo (Nguồn sự thật)

**Status:** DRAFT — chờ vòng phân tích expert cho §2 (điều kiện kịch bản). Phần §3 (snapshot hiển thị thuần) đã đủ rõ để code.
> **Superseded (29/09/2026):** tài liệu này đã được thay thế bởi [`v2-scenario-engine/spec.md`](../v2-scenario-engine/spec.md) (APPROVED, đã land) — chỉ giữ lại làm tiền sử thiết kế. Luồng V2 as-is: canon duy nhất tại [`domain/pipeline-jobs.md`](../../domain/pipeline-jobs.md#luồng-v2-scenario-engine--sự-thật-chuẩn-duy-nhất).
**Cập nhật:** 29/09/2026
**Phạm vi:** 13 chỉ báo kỹ thuật tái xuất với tri thức diễn giải vốn có, ghép thành kịch bản giao dịch (Context/Setup/Trigger/Risk-Exit) và snapshot tại thời điểm quyết định.
**Thay thế:** mọi thảo luận trước về vai trò chỉ báo (plan snapshot-only 28/09; mục "Chỉ báo kỹ thuật & Playbook" trong `docs/domain/buy-decision.md` là bản gỡ bỏ). `docs/features/indicator-playbooks/*` giữ trạng thái Superseded (máy xếp hạng 22 dòng đã chết, không dựng lại).
**Quy tắc:** mọi thay đổi về chủ đề này **sửa file này trước, rồi mới sửa code**.

---

## 0. Tri thức nền (chốt 29/09/2026 — không tranh luận lại)

Chỉ báo **không phải những con số vô nghĩa chờ backtest cấp phép tồn tại**. Mỗi chỉ báo vốn có logic diễn giải và kịch bản riêng (RSI: quá mua/bán, phân kỳ, hồi từ cực trị; MACD: momentum, giao cắt, histogram mở rộng/thu hẹp; EMA: xu hướng, pullback, đổi cấu trúc; Bollinger: co hẹp→breakout, chạm biên; Ichimoku: trend + hỗ trợ/kháng cự; Stochastic: timing ngắn; ADX: độ mạnh trend; VWAP: trên/dưới vùng giá trị; ATR: biên độ→SL/TP; Volume: xác nhận/climax/exhaustion; VSA: giá–volume; POC: vùng giá trị; SMC: structure/liquidity).

Vấn đề thật: **kết hợp các logic đó thành hệ quyết định mà không đếm trùng cùng một loại tín hiệu** (EMA/MACD/ADX cùng đo trend ≠ 3 phiếu BUY).

**Hai vòng tách bạch:**

```
VÒNG 1 — Dựng (làm NGAY từ tri thức, không cần điều kiện tiên quyết):
  Tri thức chỉ báo → Kịch bản (Context/Setup/Trigger/Risk-Exit) → Rule engine → BUY/HOLD/SELL

VÒNG 2 — Tinh chỉnh (chạy SAU, trên dữ liệu Vòng 1 thu được):
  Snapshot + kịch bản lưu tại T + outcome T+1/2/2.5/3 + MFE/MAE
    → IC ngang / backtest / walk-forward → chỉnh ngưỡng, trọng số, LOẠI kịch bản yếu
```

Vòng 2 **không cấp phép** cho chỉ báo tồn tại; Vòng 2 **kiểm tra, tinh chỉnh, loại bỏ**.

## 1. Phân vai 13 chỉ báo (4 vai trò — chống đếm trùng)

| Vai trò | Câu hỏi | Chỉ báo đảm nhiệm |
|---|---|---|
| **1. Context — Bối cảnh** | Có nên tìm lệnh T+ ở mã này? | EMA20/50/200, Ichimoku, ADX, VNINDEX + Sector (đã có trong engine) |
| **2. Setup — Hình thái chuẩn bị** | Có setup đáng chú ý đang hình thành? | Bollinger co hẹp, Darvas/base (`DarvasBreakoutAnalyzer` sẵn có), Volume co (VSA), POC |
| **3. Trigger — Điểm kích hoạt** | BÂY GIỜ có phải lúc hành động? | Breakout + Volume nổ, MACD hist mở rộng, RSI reclaim >50, VWAP reclaim, Stochastic, sweep+reclaim (backlog) |
| **4. Risk/Exit — Quản trị lệnh** | Mua đâu, SL đâu, TP đâu, khi nào thoát? | ATR (SL/TP), resistance/overhead box, RSI exhaustion/phân kỳ, volume climax, MACD xấu đi, gãy nền, Bollinger chạm biên trên |

Quy tắc: **mỗi vai trò bỏ phiếu tối đa 1 lần** trong một kịch bản; chỉ báo cùng vai trò chỉ nêu điều kiện mạnh nhất, không cộng dồn điểm.

## 2. Năm kịch bản — BẢN THẢO CẦN EXPERT DUYỆT (chưa chốt ngưỡng)

> ⚠️ Mục này là câu hỏi mở cho vòng phân tích expert. Các ngưỡng dưới đây là **đề xuất ban đầu theo convention phổ biến, CHƯA được chủ dự án duyệt**. Bản code nháp đầu tiên dùng đúng các ngưỡng này đã bị reject ngày 29/09 — không code lại trước khi mục này được chốt.

| Kịch bản | Điều kiện đề xuất (cần duyệt từng con số) | Ghi chú mở |
|---|---|---|
| **Bứt phá** | Context: EMA20>EMA50, ADX≥25 · Setup: BB co hẹp/vol co · Trigger: close > đỉnh nền 20 phiên + vol ≥1.5×TB20 · Confirm: MACD hist↑, RSI>50 đi lên | Đỉnh nền 20 phiên hay nên lấy từ Darvas box có sẵn? ADX≥25 có quá gắt với sideway-up VN? |
| **Hồi trend reclaim** | Context: EMA20>EMA50>EMA200 · Trigger: hôm qua ≤ EMA20, hôm nay đóng > EMA20 · Confirm: RSI 40–65↑, vol bán thấp (<1.3×) | Nên dùng EMA20 hay VWAP làm mốc reclaim? Nhánh pullback MA10/20 hiện hữu của engine có trùng không? |
| **Quét thanh khoản** | Quét đáy vùng + spike + đóng reclaim nến xanh (công thức SMC cũ: low < minLow×0.995, close > minLow) | Xấp xỉ nến NGÀY — bản intraday thật cần thiết kế nguồn KBS 60s (backlog §5). Có chấp nhận xấp xỉ ngày ở v1? |
| **Kiệt sức — chốt lời** | ROC 5 phiên ≥8% + (vol ≥2× hoặc RSI ≥72) + nến đỏ/hist thu hẹp | Ngưỡng 8%/5 phiên có hợp lý với biên độ VN? Có cần thêm phân kỳ RSI (đã có `ISignalAnalyzer` phân kỳ 15m/1h/ngày)? |
| **Gãy nền — thoát** | Close < đáy vùng 20 phiên + vol ≥1.3× + mất EMA20 + MACD hist xấu đi | Phân biệt với cut-loss hiện hữu (peak-based) thế nào — nhãn chồng nhau? |

**Thứ tự ưu tiên khi nhiều kịch bản cùng khớp (đề xuất, cần duyệt):** Gãy nền → Kiệt sức → Bứt phá → Quét thanh khoản → Hồi trend. Lý do: trạng thái nguy hiểm thắng trạng thái cơ hội; breakout thắng sweep vì trigger mạnh hơn. **Cần expert phản biện.**

## 3. Snapshot chỉ báo (phần này ĐÃ chốt — code được ngay, không chứa ngưỡng quyết định)

Hàm: `SnapshotChiBao.TaoDanhSach(danhSachNen)` → `string?` (null khi <2 nến; thành phần thiếu dữ liệu bỏ qua, không in "N/A"). Định dạng 1–2 dòng, separator ` · `:

```
RSI 63↑ · EMA20>50>200 · MACD hist+ · BOLL %B 0.8 · ICHI trên mây · STOCH 72↑ · ADX 28 · VWAP+ · ATR 2.1% · VOL 1.7×
VSA 8.0 · POC 7.8 · SMC 8.8        ← chỉ khi ≥ 60 nến (điểm bundle thô /10, không gate)
```

Công thức: kế thừa nguyên bản git HEAD `TechnicalIndicatorAnalyzer.cs` / `IndicatorBundleScorer.cs` (chưa commit xóa — `git show HEAD:...` lấy lại được), tính toán dùng `IndicatorMath.cs` (đã tách, đang sống).

`PhanTichKichBan.PhanLoai(...)` → tên kịch bản §2 hoặc null. Cặp snapshot+kịch bản chụp tại sự kiện, **lưu vĩnh viễn cùng sự kiện**. Intraday: history ngày + nến sống (KBS row) ghi đè/append.

### Điểm cài cắm

| Sự kiện | File | Lưu / hiển thị |
|---|---|---|
| Lọt top (T-1 15:05) | `DailyAnalysisRunner.cs` (~dòng 196, `item.Stock.History` có sẵn) | Cột mới `IndicatorSnapshot`, `KichBan` trên `DailyOpportunities` (+EF migration; DB migrate tự động lúc khởi động qua `DatabaseInitializer.MigrateAsync`) → `OpportunityDto` → card Top web+mobile |
| Bắn noti (Entry Ready/Mua 1/Mua 2/Bán nửa/Bán hết/Cắt/Rủi ro) | `TopOpportunityVipAlertPublisher.cs` + `VipTelegramMessageFormatter.cs` | Dòng `🎬 Kịch bản` + `📊 snapshot` trong telegramBody → tự vào `Alert.Content` (lịch sử alert web/mobile đọc nguyên văn, không sửa UI alert) + SignalR |
| Noti MUA — quản trị lệnh | formatter Buy | Dòng `🎯 TP1/TP2` + `⛔ Vô hiệu: đóng < BaseLow` từ `entry`/`FindOverheadBox`/`atrPct` đã có trong publisher — không công thức mới |
| Hồ sơ LLM veto (mua) | `VipLlmContextBuilder.BuildAsync` | thêm `indicatorSnapshot`, `scenario` → log `VipAlertFires` phục vụ Vòng 2 |

KHÔNG gắn ở: StockDetail, CriteriaSummary, máy hậu kiểm 22 dòng (B1).

## 4. Bất biến (cấm đổi không bàn bạc)

- **B1 — Không bảng xếp hạng thường trực:** không dựng lại leaderboard/composite một nồi (đã chết 1 lần — D1–D3 spec playbooks).
- **B2 — Kịch bản chưa có quyền quyết (v1):** rule engine + Buy Score + ML ranker giữ nguyên. Snapshot/kịch bản chỉ diễn giải, hiển thị, ghi log. Muốn cho quyền chặn/đẩy → qua Vòng 2 + shadow-run + sửa §4.
- **B3 — Fail-open:** thiếu dữ liệu → bỏ snapshot/kịch bản/dòng TP; tín hiệu gốc chạy y hệt.
- **B4 — Vòng 2 chỉ tinh chỉnh, không cấp phép:** không viện cớ "chưa có IC" để từ chối dựng kịch bản từ tri thức; không đổi ngưỡng/trọng số "nghe hợp lý" mà chưa đo.

## 5. Backlog (chốt thứ tự, chưa làm đợt này)

1. `LiquiditySweepDetector` intraday thật (nguồn KBS 60s) — spec riêng.
2. Kịch bản đề xuất kích thước vị thế (position sizing theo RiskScore) — sau ≥2 tuần mẫu Vòng 2.
3. Vòng 2 harness: IC ngang từng vai trò × kịch bản trên outcome T+2.5 (MFE/MAE đã có một phần trong `VipAlertFires`).

## 6. Quy ước code (chỉ thị owner 28–29/09/2026)

- Định danh MỚI: tiếng Việt không dấu trong tên (`SnapshotChiBao`, `TaoDanhSach`, `PhanLoai`); thuật ngữ trừu tượng (RSI, MACD, ATR, EMA, VWAP, ADX, Bollinger, Ichimoku, Stochastic, Darvas, sweep) giữ nguyên + chú thích tiếng Việt. Comment tiếng Việt có dấu đầy đủ.
- Codebase hiện hữu KHÔNG đổi tên. Cột DB/DTO giữ tên kỹ thuật (`IndicatorSnapshot`, `KichBan`).
- File mới dự kiến: `backend/StockRadar.Domain/Services/SnapshotChiBao.cs`, `PhanTichKichBan.cs`; test `backend/StockRadar.Tests/Playbook/SnapshotChiBaoTests.cs`.

## 7. Nghiệm thu & lịch đo

| # | Tiêu chí | Cách đo |
|---|---|---|
| SC-001 | Build+test 3 nền tảng xanh | `dotnet build StockRadar.Api/...` 0 lỗi; `dotnet test` pass; `npm run build`; `flutter analyze` không lỗi mới |
| SC-002 | Lọt top có snapshot+kịch bản | job `daily-analysis` dev → `GET /api/v1/market/opportunities` → `indicatorSnapshot != null` |
| SC-003 | Noti đủ dòng mới | unit test formatter: có dữ liệu → body chứa `📊`/`🎬`/`🎯`; null → sạch |
| SC-004 | Fail-open | history 1 nến → null; hành vi cũ không đổi |
| SC-005 | Ship | `.\scripts\ship-all.ps1` (cổng L3 security review) |
| SC-006 | Đo sau ship | 15:05 phiên kế: top prod có snapshot; phiên kế: noti thật; ghi §8 |

## 8. Nhật ký đo lường

- _(trống)_

## 9. Bối cảnh bàn giao cho phiên expert (29/09/2026)

**Trạng thái repo:** thay đổi GỠ máy xếp hạng 22 dòng (xóa `TechnicalIndicatorAnalyzer.cs`, `IndicatorBundleScorer.cs`, `BundleGateTests.cs`; tách `IndicatorMath.cs`; enum `[Obsolete]`; dọn UI web/mobile) đang ở working tree **CHƯA COMMIT**. Build/test đã xanh (163/163). Công thức 13 chỉ báo vẫn lấy lại được: `git show HEAD:backend/StockRadar.Domain/Services/TechnicalIndicatorAnalyzer.cs`.

**Đã làm và bị REJECT (không tự ý làm lại):** bản nháp code đầy đủ gồm `SnapshotChiBao.cs` + `PhanTichKichBan.cs` (ngưỡng như bảng §2), cột `IndicatorSnapshot`/`KichBan` + mapping, và chính file spec này bản đầu. Lý do reject: phần logic kịch bản (§2) cần vòng phân tích expert trước khi thành code — chủ dự án hỏi "đưa phân tích logic sang expert mode có hợp lý không" và đồng ý hướng tách: §3 snapshot thuần = code được ngay; §2 kịch bản = cần duyệt.

**Câu hỏi cần expert trả lời (đầu vào cho phiên expert):**
1. Từng kịch bản §2: bộ điều kiện + ngưỡng nào đúng với tri thức diễn giải chuẩn của chỉ báo và biên độ thị trường VN? (bảng §2 cột "Ghi chú mở")
2. Thứ tự ưu tiên khi nhiều kịch bản khớp — đề xuất hiện tại có hợp lý?
3. Kịch bản sell-side (Kiệt sức/Gãy nền) có nên là nhãn trên noti bán vốn đã peak-based, hay cần ánh xạ sang hành động khác (giảm size/chờ xác nhận)?
4. Xấp xỉ nến ngày cho Quét thanh khoản ở v1: chấp nhận hay chờ detector intraday?
5. Vai trò nào được phép "cất tiếng" trong hồ sơ LLM veto (B2 cho phép hiển thị — nhưng có nên thêm luật diễn giải cho LLM)?

**Cách phiên expert nhận ngữ cảnh:** đọc file này + memory dài hạn (đã tự lưu: event-snapshot, cấm composite sớm, framework 4 tầng, quy ước tên Việt, T+ Decision Engine alignment). Kết quả expert → ghi thẳng vào §2 (thay cột đề xuất bằng cột đã duyệt) → sau đó code theo §3+§6+§7.
