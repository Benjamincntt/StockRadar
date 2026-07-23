# Kế hoạch triển khai: Canon tài liệu domain

**Branch**: `001-docs-domain-canon` | **Date**: 2026-07-23 | **Spec**: [`spec.md`](./spec.md)

**Input**: Đặc tả tính năng từ `specs/001-docs-domain-canon/spec.md`

**Lưu ý**: Kế hoạch này chỉ thiết kế và tổ chức lại tài liệu. Không đổi hành vi runtime của Buy Score, Top, MA stack, flatBox hay ReversalBounce.

## Summary

Thiết lập một canon tài liệu living duy nhất cho StockRadar bằng cách tạo `docs/README.md` làm cửa vào, gom luật sản phẩm đang phân mảnh vào `docs/domain/*`, giảm `CLAUDE.md` / Continue rules về vai trò bản đồ ngắn, và tách hẳn tài liệu lịch sử sang `docs/_archive/`. Cách làm bám code as-is, dùng artifact AIUP (`docs/use_cases.puml`, `docs/use_cases/UC-*.md`, `docs/entity_model.md`) làm lớp phân tích, và giữ `docs/architecture.md` là tổng quan kiến trúc thay vì nơi chứa luật cổng domain.

## Technical Context

**Language/Version**: Markdown UTF-8 cho tài liệu; PowerShell cho script Spec Kit; repository chính là .NET / Flutter / React nhưng feature này không đổi code runtime

**Primary Dependencies**: Spec Kit (`.specify/`), AIUP/Tessl artifacts trong `docs/use_cases*` và `docs/entity_model.md`, các docs living hiện có trong `docs/`

**Storage**: Git repository (`docs/`, `CLAUDE.md`, `.continue/rules/`, `.cursor/rules/`, `specs/`)

**Testing**: Kiểm tra thủ công cấu trúc tài liệu, link nội bộ, độ phủ chủ đề, và đối chiếu nhanh với code entry files

**Target Platform**: Repository tài liệu dùng trong Cursor/agent + người đọc markdown trên Windows

**Project Type**: Tổ chức tài liệu cho monorepo ứng dụng web/mobile/API

**Performance Goals**: Người mới tìm đúng tài liệu sống cho 5 chủ đề chính trong dưới 5 phút; không có hơn 1 tài liệu “đúng hiện tại” cho cùng một luật domain

**Constraints**:

- Không đổi hành vi runtime
- Phải bám constitution v1.0.1
- Phải giữ artifact AIUP và phân biệt rõ với luật living
- Chỉ archive tài liệu lỗi thời; không làm mất thông tin tham chiếu cần thiết

**Scale/Scope**: ~10 file docs living top-level còn lại, `CLAUDE.md`, `.continue/rules/stockradar.md`, một số `.cursor/rules/*.mdc`, và thêm mới `docs/README.md` + ít nhất 5 file `docs/domain/*.md`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Source: `.specify/memory/constitution.md` v1.0.1*

- [x] **I. Code as truth**: Kế hoạch dùng `BuyDecisionEngine`, `SmartMoneyOpportunitySelector`, `SignalAnalyzer`, `DarvasBreakoutAnalyzer`, `DailyAnalysisRunner`, `MarketBreadthRunner`, `ReversalBounceAnalysisRunner` làm entry để đối chiếu as-is; không lấy `CLAUDE.md` làm runtime truth.
- [x] **II. Spec-first**: Đây là thay đổi thiết kế tài liệu domain trọng yếu; đã có `spec.md` trước plan và chưa implement code runtime.
- [x] **III. Minimal surface**: Chỉ chạm docs / map agent / spec artifacts; không refactor code hay đổi dependency.
- [x] **IV. Domain gates**: Kế hoạch tạo `docs/domain/*` để mọi thay đổi Buy Score / Top / MA·phase / flatBox / ReversalBounce sau này cập nhật cùng feature spec.
- [x] **V. Simplicity**: Không thêm abstraction mới; chỉ chuẩn hóa cấu trúc docs, index, cross-link và archive.
- [x] **Stack**: Feature chỉ chạm `docs/`, `specs/`, `CLAUDE.md`, `.continue/rules/`, `.cursor/rules/`; không cần restart API hay ship script.

## Project Structure

### Documentation (this feature)

```text
specs/001-docs-domain-canon/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── documentation-navigation.md
└── tasks.md
```

### Source Code (repository root)

```text
docs/
├── README.md                    # mới - cửa vào canon
├── architecture.md             # tổng quan kiến trúc
├── domain/                     # mới - luật sản phẩm living
│   ├── buy-decision.md
│   ├── ma-stack-and-market-phase.md
│   ├── base-price-flatbox.md
│   ├── pipeline-jobs.md
│   └── reversal-bounce.md
├── _archive/                   # lịch sử/audit/proposal
├── use_cases/                  # AIUP artifacts
└── entity_model.md             # AIUP artifact

CLAUDE.md
.continue/rules/stockradar.md
.cursor/rules/token-cost-efficiency.mdc
specs/001-docs-domain-canon/
```

**Structure Decision**: Feature này chỉ chạm lớp tài liệu và map agent. Không chạm `backend/`, `mobile/`, `frontend/` trừ việc đọc code để xác nhận luật as-is.

## Complexity Tracking

Không có vi phạm hiến pháp cần biện minh ở pha thiết kế này.
