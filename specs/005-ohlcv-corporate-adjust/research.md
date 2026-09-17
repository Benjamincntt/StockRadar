# Phase 0 — Research: Điều chỉnh giá theo sự kiện quyền

Mọi mục đã đối chiếu code trên disk (`SignalAnalyzer`, `BuyDecisionEngine`, `EntityMapper`, `SmartMoneyOpportunitySelector`).

## R-1. Lùi OHLC về thang nến cuối — không ghi đè kho

**Decision**: Với mỗi sự kiện, nến có `Date < ngayKhongHuongQuyen` nhân Open/High/Low/Close với `heSoNgayQuyen`; nến từ ngày quyền trở đi hệ số 1 cho sự kiện đó. Volume không đổi. Nhiều sự kiện: hệ số nến = tích các `heSoNgayQuyen` của sự kiện **sau** ngày nến; mỗi `heSoNgayQuyen` tính từ **Close thô** phiên liền trước ngày quyền.

**Rationale**: Nến cuối = giá sàn (FR-003). Hộp / FOMO / MA trong `Evaluate` so sánh cùng thang với giá hiện tại. `EntityMapper.ToDomain` + `ToEntity` vẫn serialize thô — tránh ghi đè history (FR-004).

**Alternatives considered**:
- *Ghi Close điều chỉnh vào DB*: phá giá khớp, chart sai, Job 2 conflict.
- *Chỉ vá `GetChangePercent` hai mốc Close*: FOMO/`AnalyzeFlatBox` vẫn so đỉnh hộp thô (`DarvasBreakoutAnalyzer`).
- *Điều chỉnh lúc `ToDomain`*: mọi API/chart thành Yahoo-adjust; `ToEntity` có nguy cơ persist dãy đã nhân.

## R-2. Một công thức hai tham số

**Decision**: `giaThamChieu = (giaTruocQuyen − tienMat) / heSoPhaLoang`; `heSoNgayQuyen = giaThamChieu / giaTruocQuyen`. Thiếu tiền → `tienMat = 0`; thiếu pha loãng → `heSoPhaLoang = 1`. Không enum loại. ESOP ngoài v1.

**Rationale**: Khớp spec; SSI (24.5, 1.0, 1.2) → `giaThamChieu` ≈ 19.58, hệ số ≈ 0.799. Tránh nhầm “hệ số” với 19.58.

**Alternatives considered**: nhánh `cash_dividend` / `stock_bonus` / `combined` — trùng công thức, dễ cộng chồng cùng ngày.

## R-3. Seed file, không SQL, không crawler

**Decision**: JSON versioned tại `backend/StockRadar.Api/Data/su-kien-quyen.json`, copy ra output, đọc qua `IHostEnvironment.ContentRootPath`. Đường dẫn cấu hình được, không secret. Invalid row → bỏ qua + log, không hệ số 0 giả.

**Rationale**: User chốt v1 nhập tay. Job 2 không có field quyền từ KBS.

**Alternatives considered**: bảng EF (cần CRUD/API, không cần để land P1); crawler HOSE (v2).

## R-4. Chỗ gọi: dãy chấm điểm, không phải mọi `History`

**Decision**:
1. `ISignalAnalyzer.LayLichSuChamDiem(Stock)` → dãy điều chỉnh (không sự kiện ⇒ cùng reference/list thô).
2. `GetChangePercent(Stock)` / `GetRelativeStrength(Stock)` dùng dãy đó.
3. `BuyDecisionEngine.Evaluate`: `stock with { History = LayLichSuChamDiem(stock) }` rồi mới `DetectSignals` / `AnalyzeFlatBox` / MA.
4. `BuildSectorSnapshots`: đổi `GetChangePercent(s.History, 1)` → `GetChangePercent(s, 1)`.
5. `StockService` (flatBox/levels) và `DarvasBreakoutAlertPublisher`: `AnalyzeFlatBox(LayLichSuChamDiem(...))`. Chart/`IChartBarProvider` giữ thô.

**Rationale**: `GetChangePercent(IReadOnlyList)` không có mã — giữ thô cho test dựng tay. Sóng ngành đang gọi overload list nên **phải** đổi 1 dòng kẻo RS sạch mà độ rộng phiên vẫn dump.

**Alternatives considered**: decorator repository (Job 2 cũng nhận dãy đã nhân → persist hỏng); chỉ sửa RS ngành (US2 cấm).

## R-5. `LastChangePercent` nguồn khớp không sửa

**Decision**: Cột/quote `LastChangePercent` từ sync giữ nguyên. Engine đủ nến thì không dùng fallback này (`GetChangePercent` ưu tiên hai Close).

**Rationale**: FR-003 là giá last/OHLC phiên đang khớp, không bắt chỉnh % ticker vendor.

## R-6. VNINDEX không seed

**Decision**: Không sự kiện cho chỉ số. RS = % mã (điều chỉnh) − % VNINDEX thô cùng N.

**Rationale**: Spec. `GetChangePercent(indexHistory)` trong ReversalBounce cho index giữ thô.

## R-7. SC-005 trong test, không chặn seed production

**Decision**: Case thứ hai = fixture unit (mã giả hoặc sự kiện đơn giản). Seed production P1 chỉ **bắt buộc SSI 17/08**. Ops thêm dòng JSON khi có TB GDKHQ.

**Rationale**: Spec không bắt backfill 3 năm trước khi land P1.

## R-8. Identifier mới tiếng Việt không dấu

**Decision**: `SuKienQuyen`, `tienMat`, `heSoPhaLoang`, `BoDieuChinhGiaTheoQuyen`, `TaoDayGiaDieuChinh`, `LayLichSuChamDiem`, `FileNguonSuKienQuyen`. Giữ `GetChangePercent`, `OhlcvBar`, route/DTO cũ.

**Rationale**: Luật `.cursor/rules/vietnamese-identifiers.mdc`. JSON seed mới dùng key tiếng Việt (camelCase) — chưa ship.

## R-9. Không đổi mobile/web

**Decision**: Không field API mới. Chart vẫn nến thô. Điểm/RS đổi vì engine.

**Rationale**: FR-003 + constitution III.
