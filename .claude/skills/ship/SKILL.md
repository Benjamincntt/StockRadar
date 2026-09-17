---
name: ship
description: Viết lệnh scripts/ship-all.ps1 để user tự copy-chạy, dựa theo thay đổi hiện tại. Dùng khi user hỏi kiểu "viết lệnh ship để tôi tự chạy".
---

# Ship

Dựa vào `git status`/`git diff` hiện tại (staged + unstaged) và các thay đổi đã thảo luận/thực hiện trong session này, viết ra lệnh `scripts/ship-all.ps1` phù hợp để user tự copy và chạy trong terminal (KHÔNG tự chạy lệnh này).

Quy tắc soạn lệnh:

- `-Message "..."`: tóm tắt ngắn (khớp văn phong commit message hiện có trong repo — xem `git log` gần đây) nội dung đã đổi.
- `-DeployAction`: mặc định của script đã là `be` (chỉ backend). Chỉ thêm `-DeployAction all` (hoặc `fe`) nếu thay đổi có chạm `frontend/src/**`. Nếu chỉ backend/mobile/docs thì không cần truyền cờ này (dùng default).
- Nếu thay đổi có chạm `mobile/lib/**`, nhắc thêm 1 dòng ngoài code block: ship-all không build/deploy mobile — app mobile build/publish riêng, hỏi user có cần build APK/IPA không.
- Nếu chưa có gì để commit (git status sạch), báo vậy và hỏi lại có phải muốn deploy lại không cần commit mới (`-SkipCommit`).

Output CHỈ một fenced code block ` ```bash ` chứa đúng 1 lệnh `.\scripts\ship-all.ps1 ...`, không thêm lệnh khác, không giải thích dài dòng phía trên/dưới trừ khi có lưu ý mobile như trên.
