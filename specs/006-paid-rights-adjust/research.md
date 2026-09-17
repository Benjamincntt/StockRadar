# Phase 0 — Research: quyền mua trả tiền

## R-1. Một công thức, 005 là trường hợp đặc biệt

**Decision**: `giaThamChieu = (P − tienMat + tyLeQuyenMua × giaPhatHanh) / (heSoPhaLoang + tyLeQuyenMua)` với `tyLeQuyenMua = soCoMoi / soCoCu` khi cả hai > 0, ngược lại 0.

**Rationale**: SSI (`tyLe` = 0) rút gọn `(P − 1) / 1.2`. HCM 16/07: `(26.95 − 0.4 + 0.25 × 10) / 1.25` = 23.24 = `(4 × 26.55 + 10) / 5`. Không hai nhánh cộng chồng cùng ngày.

**Alternatives considered**:
- *Chỉ ghi 0.4*: gap HCM ~1.5%, bỏ quyền mua — user từ chối (P.A. 1).
- *Pha loãng 1.25*: `(26.95 − 0.4) / 1.25` = 21.24 → % ~+19 thay vì +9 — user từ chối (P.A. 2).
- *Enum loại sự kiện*: trùng công thức, dễ cộng hai dòng cùng GDKHQ.

## R-2. Tỷ lệ n:m nguyên, không bắt user tính 0.25

**Decision**: Lưu `soCoCu` / `soCoMoi` (4 và 1). `tyLe` tính trong engine. JSON seed: `soCoCu`, `soCoMoi`, `giaPhatHanh`. API camelCase English: `oldShares`, `newShares`, `issuePrice` (JSON đã ship không đổi key cũ).

**Rationale**: HOSE viết “4:1”. Identifier mới tiếng Việt không dấu trong C#/Dart; key API English đã có `cash`/`dilution`.

**Alternatives considered**: một field `tyLeQuyenMua` 0.25 — dễ nhầm 4 vs 0.25.

## R-3. GDKHQ không có nến

**Decision**: Giữ `CloseThoTruoc`: nến mới nhất `Date < ngayKhongHuongQuyen`. HCM 16/07 vắng nến → P = Close 15/07 = 26.95; 17/07 đã là thang sau quyền.

**Rationale**: Đúng data-model 005; không bịa nến GDKHQ.

## R-4. Giả định nhận đủ quyền

**Decision**: Không mô phỏng tỷ lệ đăng ký / bỏ quyền.

**Rationale**: Spec v1; chuẩn điều chỉnh chỉ số.

## R-5. Seed HCM + hồi quy SSI

**Decision**: Thêm hai dòng HCM vào `su-kien-quyen.json`. Không sửa dòng SSI. Test unit: 23.24; % 15/07→17/07 ∈ +9±1.5; cấm xấp xỉ 21.24; SSI 19.58 giữ.

**Rationale**: FR-003/004 spec 006.
