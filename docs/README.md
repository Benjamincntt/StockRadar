# StockRadar — mục lục tài liệu

**Cửa vào duy nhất** cho tài liệu sản phẩm. Khi docs lệch code → **tin code trên disk**.

**Governance:** [`.specify/memory/constitution.md`](../.specify/memory/constitution.md) · Spec Kit: [`specs/001-docs-domain-canon/`](../specs/001-docs-domain-canon/)

## 1. Domain living (luật sản phẩm)

| Chủ đề | File | Ghi chú |
|--------|------|--------|
| Buy Score / cổng Top / hiển thị điểm | [`domain/buy-decision.md`](./domain/buy-decision.md) | V1 Top pipeline: 7 gate trong loop (chia chác FireAnt đầu tiên) + 4 cổng BuyDecisionEngine + ML rank + sector bonus + hygiene + sector cap; UC-003 · UC-005 · VIP LLM veto: [`features/vip-deepseek-veto/spec.md`](./features/vip-deepseek-veto/spec.md) · Playbook dimension (spec Superseded, chiều đo vẫn chạy): [`features/indicator-playbooks/spec.md`](./features/indicator-playbooks/spec.md) · Sóng ngành + kiểu điểm vào: [`features/sector-wave-entry-patterns/spec.md`](./features/sector-wave-entry-patterns/spec.md) |
| MA stack & pha thị trường | [`domain/ma-stack-and-market-phase.md`](./domain/ma-stack-and-market-phase.md) | MA không còn là cổng Top (checklist + `HasMaStack`) |
| Hộp phẳng / flatBox / Darvas | [`domain/base-price-flatbox.md`](./domain/base-price-flatbox.md) | FOMO, setup zone |
| Pipeline jobs | [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md) | Job 1/2/analysis/monitor V1 + **luồng bộ kịch bản V3 (4 nhịp) — sự thật chuẩn duy nhất** (mục canon trong file này) |
| Bộ kịch bản V3 (spec 012) | [`../specs/012-v3-scenario-engine/spec.md`](../specs/012-v3-scenario-engine/spec.md) · [`features/v3-scenario-engine/research/`](./features/v3-scenario-engine/research/) | Spec + nguồn luật chuẩn (normative) — as-is xem [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-bộ-kịch-bản-v3--sự-thật-chuẩn-duy-nhất). V2 đã gỡ toàn bộ |
| Lợi nhuận thực (Realized P&L) | [`domain/realized-pnl.md`](./domain/realized-pnl.md) | Song song T+2.5, không thay thế |

Kiến trúc tổng quan (không thay domain): [`architecture.md`](./architecture.md)

> **V1 ↔ V3 (canon 2026-10-26):** pipeline V1 (`DailyAnalysisRunner` → `DailyOpportunities`) chạy **song song** bộ kịch bản V3 spec 012 (4 nhịp trước phiên / trong phiên / ATC / sau phiên → `TinHieuV3` + `ViTheV3` + `ChiSoNgayV3`). Home Top phụ thuộc V1 (bắn tin VIP đã gỡ — spec 011); V3 phục vụ `GET /v3/hieu-qua/*`, `GET /stocks/{sym}/v3`, `GET /nhat-ky/tin-hieu/v3` + noti MUA/SELL Telegram realtime. **V2 đã gỡ toàn bộ theo spec 012. Sự thật chuẩn luồng V3: [`domain/pipeline-jobs.md`](./domain/pipeline-jobs.md#luồng-bộ-kịch-bản-v3--sự-thật-chuẩn-duy-nhất)**. Nguồn luật chuẩn (normative): [`features/v3-scenario-engine/research/`](./features/v3-scenario-engine/research/).

### Quy tắc cập nhật

Đổi cổng / điểm / MA·pha / flatBox / pipeline **trọng yếu** → Spec Kit feature + cập nhật file `docs/domain/*` tương ứng **trong cùng change set**.

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
| [`ai-context.md`](./ai-context.md) | Tham chiếu chính cho AI agent: pipeline flow, gates, config keys, API, UI |
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
| `features/reversal-bounce/*`, `domain/reversal-bounce.md` | **Đã gỡ** — spec `008-remove-reversal-bounce`; bản lưu trữ ở `_archive/` |

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
