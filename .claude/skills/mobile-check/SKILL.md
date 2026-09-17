---
name: mobile-check
description: Kiểm tra nhanh xem thay đổi hiện tại có cần build lại app mobile không. Dùng khi user hỏi kiểu "có cần build lại app mobile không?".
---

# Mobile Check

Kiểm tra xem các thay đổi trong session này (và `git status`/`git diff` hiện tại, cả staged + unstaged) có chạm vào `mobile/lib/**`, `mobile/pubspec.yaml`, hoặc bất kỳ file nào dưới `mobile/` không.

Trả lời theo đúng format sau, không thêm giải thích dài dòng:

- Dòng đầu: **"Yes"** hoặc **"No"** (in đậm).
- Dòng thứ hai (chỉ 1 dòng): nếu Yes → liệt kê ngắn gọn file/khu vực mobile đã đổi. Nếu No → nói rõ thay đổi chỉ ở backend/frontend/docs (không đụng mobile).
- Nếu Yes: thêm ngay sau đó một fenced code block ` ```bash ` chứa lệnh build APK để user copy-chạy trong cmd:
  - Mặc định (release, arm64, nối API production): `.\mobile\build-apk.ps1`
  - Nếu user hay test bằng máy + điện thoại cùng LAN với API local: gợi ý thêm biến thể `.\mobile\build-apk.ps1 -Local` (script tự dò IP LAN máy, khỏi cần gõ `-ApiBase` tay).
  - Chỉ đưa 1 lệnh chính (bản production `.\mobile\build-apk.ps1`); nêu biến thể `-Local` bằng 1 dòng chú thích ngắn bên dưới code block, không cần liệt kê hết mọi flag của script.

Không tự chạy `flutter build`/`flutter analyze`/`build-apk.ps1` — chỉ đưa lệnh để user tự chạy.
