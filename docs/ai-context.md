# Tiết kiệm token — StockRadar (4 công cụ)

Chạy cài đặt một lần:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "D:\Source\StockRadar\scripts\setup-ai-tools.ps1"
```

| # | Công cụ | Vai trò | Khi nào dùng |
|---|---------|---------|--------------|
| 1 | **Continue.dev** | Semantic index local | Hỏi code hàng ngày — chỉ retrieve 5 file |
| 2 | **CLAUDE.md + .cursor/rules** | Context cố định (~ít token) | Mọi session Cursor Agent |
| 3 | **Understand-Anything** | Knowledge graph kiến trúc | Hiểu sâu pipeline, onboarding |
| 4 | **Repomix** | Pack toàn repo → 1 file XML | Chỉ khi review/refactor lớn |

---

## 1. Continue.dev

- **Đã cài**: `Continue.continue-2.1.0-win32-x64` (Windows phải dùng bản này, không phải universal).
- **Lỗi activating?** Chạy `scripts\fix-continue-extension.ps1` rồi Reload Window.
- **Config**: `.continue/config.yaml`, `.continueignore`, `.continue/rules/stockradar.md`
- **Sau reload Cursor**: mở sidebar Continue → index workspace tự chạy.
- Embeddings lưu local: `%USERPROFILE%\.continue\index`

---

## 2. CLAUDE.md + Cursor rules

| File | Mô tả |
|------|--------|
| `CLAUDE.md` | Bản đồ repo + pipeline — Claude/Cursor đọc tự động |
| `.cursor/rules/token-cost-efficiency.mdc` | Ép agent search hẹp, không quét build |
| `.cursor/rules/auto-run-dev.mdc` | Restart API sau sửa backend |

Không tốn token mỗi lần hỏi — context ngắn, cố định.

---

## 3. Understand-Anything

- Repo clone: `%USERPROFILE%\.understand-anything\repo`
- Junction trong StockRadar: `.cursor-plugin/`, `understand-anything-plugin/`
- Ignore: `.understandignore`

**Lần đầu** (tốn token — chỉ chạy 1 lần hoặc khi đổi kiến trúc lớn):

```
/understand
/understand-dashboard
```

Nếu không thấy lệnh: **Settings → Plugins** → thêm  
`https://github.com/Egonex-AI/Understand-Anything`

Graph lưu tại `.understand-anything/knowledge-graph.json` (có thể commit, bỏ `intermediate/`).

---

## 4. Repomix

- CLI global: `repomix 1.16.0`
- Config: `repomix.config.json` (chỉ source, bỏ build)
- Pack:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\repomix-pack.ps1
```

Output: `repomix-output.xml` — **chỉ attach vào chat khi cần**, không dùng mặc định.

---

## Reload Cursor

`Ctrl+Shift+P` → **Developer: Reload Window**

Sau reload kiểm tra:
1. Sidebar có **Continue**
2. Chat Agent có `/understand`
3. `CLAUDE.md` hiện trong project root
