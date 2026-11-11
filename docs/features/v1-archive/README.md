# V1 — tài liệu lưu trữ

V1 là hệ chọn **Top 5 mã** trên trang Home của StockRadar, kèm radar trong phiên và theo dõi lô lớn. Toàn bộ code V1 bị gỡ theo [spec 016](../../../specs/016-remove-v1-top/spec.md).

Bộ tài liệu này ghi lại V1 làm thế nào, đủ để dựng lại khi cần mà không phải nghiên cứu lại. Nội dung đọc trực tiếp từ code và cấu hình **trước khi xoá**.

## Vì sao gỡ

Theo luật của owner: "đo trước rồi quyết". Hai phép đo cho cùng một kết luận: Top V1 **không chọn mã tốt hơn chọn ngẫu nhiên**.

- **PH-30**, Top thật trong 4 tháng: −1,14%/lệnh, trong khi chọn ngẫu nhiên được −1,60%, nằm trong cùng một khoảng.
- **PH-31**, phát lại 3 năm 2023–2026:
  - Mỗi lệnh lỗ −0,24%, chọn ngẫu nhiên lỗ −0,19%.
  - Cả danh mục lỗ −9%, trong cùng kỳ V3 lãi +264%.

Chi tiết ở [05-do-luong.md](./05-do-luong.md).

Phần duy nhất được giữ là **cổng sắp chốt quyền**. Cổng này chuyển sang V3, đo trước rồi mới bật, xem [spec 015 User Story 6](../../../specs/015-v3-rights-adjusted-prices/spec.md). Client FireAnt (`FireAntLichChotQuyenClient`, `INguonLichChotQuyen`, `ThongTinChotQuyen`, `FireAntOptions`) vẫn ở lại trong code.

## Mục lục

| File | Nội dung |
|---|---|
| [01-cong-loc.md](./01-cong-loc.md) | Universe và các cổng lọc của V1: thứ tự, điều kiện, ngưỡng, lý do loại |
| [02-cham-diem.md](./02-cham-diem.md) | Buy Score, PredictedHit, trạng thái giao dịch, pha thị trường, sóng ngành, RS, flatBox/Darvas |
| [03-xep-hang-ml.md](./03-xep-hang-ml.md) | 11 đặc trưng, model ML bản 25/09/2026 (đủ trọng số), huấn luyện, cộng điểm ngành, hygiene, giới hạn ngành, làm mới Top trong phiên |
| [04-radar-lo-lon.md](./04-radar-lo-lon.md) | Radar trong phiên, radar hồi phục sớm, theo dõi lô lớn và dòng tiền |
| [05-do-luong.md](./05-do-luong.md) | Harness phát lại, cách chạy lại, kết quả PH-30 và PH-31 |

## Dựng lại V1

1. **Tìm commit cuối còn code V1.**
   - Tài liệu này viết ngày 05/11/2026 từ code ở commit `da75683` (commit ngày 03/11/2026), lúc đó V1 còn đủ.
   - Commit cuối còn code V1 là `b2550e3` (ghi khi bắt đầu đợt 3 của [plan 016](../../../specs/016-remove-v1-top/plan.md); commit kế tiếp là commit xoá code V1).
   - Nếu chưa ghi kịp, tìm bằng lệnh:
     ```powershell
     git log --oneline -- backend/StockRadar.Infrastructure/MarketData/DailyAnalysisRunner.cs
     ```
     Lấy commit **trước** commit xoá file.
2. **Lấy code về một thư mục riêng.** Đừng đổi nhánh đang làm việc:
   ```powershell
   git worktree add ../StockRadar-v1 <commit>
   ```
3. **Chạy lại phép đo.** Làm theo [05-do-luong.md mục 5](./05-do-luong.md#5-cách-chạy-lại). Harness `backend/StockRadar.Tests/Research/V1TopReplayTests.cs` phải có trong commit đó.
4. **Bảng dữ liệu V1.** Trước khi xoá bảng, dữ liệu được xuất ra file theo [plan 016 mục D6](../../../specs/016-remove-v1-top/plan.md). Danh sách bảng nằm ở Phụ lục A của spec 016.

## Bẫy chung khi dựng lại

- **Pha thị trường không bao giờ ra Favorable trên production.**
  - Lý do: nến VNINDEX chỉ được ghi bởi `EfMarketDataWriter.UpsertIndexAsync`, với O = H = L = C và **khối lượng = 0**. Điều kiện FTD đòi khối lượng phiên tăng so với phiên trước (`MarketPhaseClassifier`), nên không bao giờ thoả, mà Favorable lại cần FTD. Ngoài ra bảng `MarketIndices` chỉ có VNINDEX từ 25/06/2026.
  - Muốn pha chạy đúng phải nạp nến VNINDEX đủ OHLCV từ KBS.
- **Top lưu lại là bản làm mới trong phiên, không phải bản cuối ngày.**
  - `GeneratedAt` lưu giờ UTC. Bản Top lưu lại được tạo khoảng 14:00 giờ VN, trong chính phiên đó.
  - Khi đo, chỉ được giả định mua ATC cùng phiên. Giả định mua giá mở cửa là đã dùng thông tin tương lai.
- **Model ML chỉ học trên 45 mẫu.**
  - Độ chính xác 100% chỉ là tự đo trên chính mẫu đã học, không phải độ chính xác thật.
  - Áp model này cho 2023–2025 là áp ngược về quá khứ, không phải dự báo.
- **Nến gồm ATC phải được ghi sau giờ đóng cửa.**
  - Hiện chỉ lần chạy 15:05 của `DailyAnalysisJob` ghi nến này.
  - Spec 016 tách việc ghi nến thành trigger riêng (`daily-session-sync-close`) trước khi gỡ V1. Nếu dựng lại V1, không được để hai nơi cùng ghi nến.
