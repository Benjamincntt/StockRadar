# Specification Quality Checklist: Điều chỉnh giá theo sự kiện quyền

**Purpose**: Validate specification completeness and quality before proceeding to planning  
**Created**: 2026-08-21  
**Updated**: 2026-08-21  
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- File seed / không bảng SQL / không crawler là **ràng buộc nguồn sự kiện v1** (user chốt), không phải chọn stack runtime.
- Công thức là hợp đồng đo lường: tách `giaThamChieu` (≈ 19.58) khỏi `heSoNgayQuyen` (≈ 0.799) — bản spec trước gọi lẫn “hệ số” với `(P − 1.0) / 1.2`.
- SC-003 cố ý **không** cam kết nhãn Sóng mạnh ngày 21/08 — chỉ hết fail RS vì gap quyền SSI.
- B (bỏ phiên) và C (trung vị) nằm ngoài scope; ESOP ngoài v1.
- FR-004: nến lưu kho giữ thô — điều chỉnh lúc tính %.
- Không có `hooks.before_specify` / `after_specify` (không có `.specify/extensions.yml`).
