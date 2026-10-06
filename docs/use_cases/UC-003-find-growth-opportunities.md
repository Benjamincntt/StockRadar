# Use Case: Tìm cơ hội tăng trưởng

## Tổng quan

**Mã Use Case:** UC-003
**Tên Use Case:** Tìm cơ hội tăng trưởng
**Tác nhân chính:** Nhà giao dịch
**Mục tiêu:** Nhà giao dịch thấy danh sách ngắn các mã vượt quét cơ hội tăng trưởng (SmartMoney / Buy Score) trong phiên, kèm tín hiệu radar sớm và radar phiên liên quan.
**Trạng thái:** Implemented

## Điều kiện tiên quyết

- Phân tích ngày cho phiên giao dịch đã chạy hoặc có thể được vận hành kích hoạt (UC-008).
- Universe cổ phiếu và lịch sử chỉ số sẵn có.

## Luồng thành công chính

1. Nhà giao dịch mở danh sách “cơ hội tốt nhất” (tăng trưởng) trên Home hoặc màn cơ hội.
2. Hệ thống tải snapshot cơ hội ngày đã lưu cho ngày giao dịch mới nhất.
3. Hệ thống xếp hạng và hiện mã kèm điểm, trạng thái giao dịch và lý do ngắn.
4. Nhà giao dịch có thể mở một mã để xem quyết định mua đầy đủ (UC-002).
5. Tuỳ chọn, nhà giao dịch xem mã phục hồi sớm đã qua lọc xu hướng lỏng nhưng chưa đủ cổng Top (`GET /api/v1/early-recovery`; màn Radar phiên đã dỡ khỏi UI 2026-09).

## Luồng thay thế

### A1: Top nghiêm ngặt trống — hiển thị giải thích gate

**Kích hoạt:** Không mã nào vượt lọc Top nghiêm trong ngày (bước 2)
**Luồng:**

1. Hệ thống trả danh sách rỗng kèm `analysisStatus = zero_matches` và `statusBullets` giải thích gate đã chặn — **không còn danh sách fallback thay thế** (relaxed fallback đã gỡ, spec `004-remove-relaxed-fallback`).
2. Nhà giao dịch hiểu vì sao Top trống thay vì nhận danh sách yếu hơn.
3. Use case tiếp tục ở bước 3.

### A2: Chưa có cơ hội

**Kích hoạt:** Phân tích chưa tạo snapshot (bước 2)
**Luồng:**

1. Hệ thống hiện trạng thái trống hoặc đang chờ.
2. Use case kết thúc.

## Điều kiện hậu quả

### Khi thành công

- Nhà giao dịch có shortlist hướng tăng trưởng trong ngày.
- Các dòng cơ hội được lưu theo ngày giao dịch để đo hiệu quả sau.

### Khi thất bại

- Không hiện danh sách “chắc chắn thắng” khi thiếu dữ liệu.

## Quy tắc nghiệp vụ

### BR-005: Buy Score và cổng Top

Một mã vào Top nghiêm chỉ khi vượt các gate hiện hành (10/2026): 7 gate trong loop của `DailyAnalysisRunner` (chia chác theo lịch quyền FireAnt — đầu tiên · thiếu ngành · không khối lượng · 4 cổng BuyDecisionEngine · volume-ratio ≥ 0.3 · GTGD TB 20 phiên ≥ 10 tỷ · không lệch giá corporate action), sau đó ML ranking kèm bonus ngành → Top hygiene → sector cap ≤ 2 mã/ngành → Top 5. Bốn cổng BuyDecisionEngine: chia chác [ngày chốt quyền → ngày thực hiện quyền] (RS Leader không được miễn) · FOMO (> 7% so đáy 5 phiên) · thị trường Unfavorable (trừ RS Leader: RS percentile ≥ 85 + RS5 ≥ 0) · ngành chưa có sóng + RS âm. Chi tiết: [`docs/domain/buy-decision.md`](../domain/buy-decision.md).

Hiển thị một điểm 0–100 trên Top, chi tiết mã, và danh sách theo dõi (UC-005 / BR-019) — cùng Buy Score; không trộn criterion composite.

### BR-006: MA stack theo pha thị trường (không còn là cổng Top)

Độ chặt MA stack theo pha: Favorable → Full, Neutral → Medium, Unfavorable → Loose — giờ chỉ còn dòng checklist "Xếp lớp MA" + cờ `HasMaStack`, không chặn vào Top (dọn cổng 2026-09). Ngưỡng percentile RS và RS5 khi mua ở Unfavorable thuộc cổng "Thị trường khó" của `BuyDecisionEngine.ResolveTopGateFailure`.

### BR-007: Buy Score là thang điểm duy nhất

Không tồn tại thang điểm song song nào cạnh Buy Score. Hệ sóng hồi (counter-trend) đã gỡ bỏ — spec `008-remove-reversal-bounce`.
