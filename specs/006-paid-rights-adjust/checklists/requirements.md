# Specification Quality Checklist: Điều chỉnh giá khi quyền mua trả tiền

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-22
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

- Công thức là hợp đồng đo lường (mở rộng 005), không phải chọn stack. `tyLeQuyenMua` = m/n từ tỷ lệ HOSE n:m.
- Case neo HCM: GDKHQ 05/02 (chỉ tiền 0.4) và 16/07 (tiền 0.4 + mua 4:1 giá 10.0). `giaTruocQuyen` 16/07 = Close 15/07 = 26.95 vì không có nến 16/07.
- SC-002/003 cố ý cấm xấp xỉ thưởng miễn phí 1.25.
- Không cam kết HCM lọt Top; ESOP / bỏ quyền ngoài scope.
- Không có `hooks.before_specify` / `after_specify` (không có `.specify/extensions.yml`).
