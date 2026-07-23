# StockRadar — mục lục tài liệu

**Cửa vào duy nhất** cho tài liệu sản phẩm. Khi docs lệch code → **tin code trên disk**.

**Governance:** [`.specify/memory/constitution.md`](../.specify/memory/constitution.md) · Spec Kit: [`specs/001-docs-domain-canon/`](../specs/001-docs-domain-canon/)

## 1. Domain living (luật sản phẩm)

| Chủ đề | File | Ghi chú |
|--------|------|--------|
| Buy Score / cổng Top / hiển thị điểm | [`domain/buy-decision.md`](./domain/buy-decision.md) | UC-003 tăng trưởng |
| MA stack & pha thị trường | [`domain/ma-stack-and-market-phase.md`](./domain/ma-stack-and-market-phase.md) | Gap uptrend 1 phiên |
| Hộp phẳng / flatBox / Darvas | [`domain/base-price-flatbox.md`](./domain/base-price-flatbox.md) | FOMO, setup zone |
| Pipeline jobs | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md) | Job 1/2/analysis/monitor |
| Sóng hồi (ReversalBounce) | [`domain/reversal-bounce.md`](./domain/reversal-bounce.md) | Regime ≠ pha Wyckoff |

Kiến trúc tổng quan (không thay domain): [`architecture.md`](./architecture.md)

### Quy tắc cập nhật

Đổi cổng / điểm / MA·pha / flatBox / pipeline / ngữ nghĩa ReversalBounce **trọng yếu** → Spec Kit feature + cập nhật file `docs/domain/*` tương ứng **trong cùng change set**.

## 2. Artifact AIUP (phân tích — không phải bảng cổng runtime)

| Artifact | Path |
|----------|------|
| Sơ đồ use case | [`use_cases.puml`](./use_cases.puml) |
| Đặc tả UC-001…008 | [`use_cases/`](./use_cases/) |
| Mô hình thực thể | [`entity_model.md`](./entity_model.md) |

## 3. Archive (lịch sử / proposal / audit)

[`_archive/`](./_archive/) — **không** dùng làm luật sống. Xem [`_archive/README.md`](./_archive/README.md).

## 4. Ops & agent

| File | Vai trò |
|------|---------|
| [`build-and-deploy.md`](./build-and-deploy.md) | Deploy / ship |
| [`ai-context.md`](./ai-context.md) | Tiết kiệm token / Continue |
| [`CLAUDE.md`](../CLAUDE.md) | Bản đồ agent ngắn |
| [`.continue/rules/stockradar.md`](../.continue/rules/stockradar.md) | Bản đồ Continue |

## 5. Map supersede (file cũ → domain)

| File cũ (stub hoặc archive) | Living thay thế |
|-----------------------------|-----------------|
| `opportunity-scan-rules.md` | `domain/buy-decision.md` + `domain/ma-stack-and-market-phase.md` |
| `smartmoney-checklist.md` | `domain/buy-decision.md` |
| `buy-score-display.md` | `domain/buy-decision.md` (mục hiển thị) |
| `base-price-engine.md` | `domain/base-price-flatbox.md` |
| `pipeline-jobs.md` | `domain/pipeline-jobs.md` |
| `telegram-vip-alerts-flow.md` | `domain/buy-decision.md` (VIP) + `architecture.md` |
| `features/reversal-bounce/*` (spec dài) | `domain/reversal-bounce.md` |

## Kiểm chứng nhanh (SC)

- SC-001: từ bảng §1 → đúng một file domain mỗi chủ đề
- SC-005: gap MA/pha nằm trong `domain/ma-stack-and-market-phase.md`
- SC-006: quy tắc cập nhật ở §1

### Kết quả implement (2026-07-23)

| SC | Kết quả |
|----|---------|
| SC-001 | PASS — 5 link domain từ README |
| SC-002 | PASS — AIUP tách mục §2 |
| SC-003 | PASS — growth vs rebound tách; cấm gộp trong domain docs |
| SC-004 | PASS — CLAUDE / Continue chỉ map + link |
| SC-005 | PASS — G-MA-1 trong `ma-stack-and-market-phase.md` |
| SC-006 | PASS — quy tắc cập nhật §1 + CLAUDE |

Quickstart 5 kịch bản: PASS (thủ công cùng ngày).
