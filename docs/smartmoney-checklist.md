# Checklist SmartMoney (Huy Hoàng) trong StockRadar

Ánh xạ 8 bước rút gọn → Job 3 (`SmartMoneyOpportunitySelector`).

| Bước kênh | Rule trong code |
|-----------|-----------------|
| 1. Pha thị trường | `MarketWyckoffPhase`: uptrend/sideway = thuận; downtrend mạnh = chặn CP yếu |
| 2. Ngành mạnh | Xếp hạng ngành theo RS trung bình + Vol ratio; ưu tiên top 5 |
| 3. CP mạnh nhất ngành | RS 5 phiên vs VNINDEX; sort theo score + sector rank |
| 4. Tích lũy | `IsAccumulation` / pha `Accumulation` — cung cạn, đi ngang |
| 5. Breakout + volume | `IsBreakout` và `Vol ratio ≥ 1.5×` TB20 |
| 6–7. Quản trị vốn | Job 4: chỉ theo dõi list đã lọc; cắt lỗ = user (chưa auto) |
| 8. Tránh phân phối | `IsDistribution` → loại ngay |
| Không bắt đáy vì rẻ | Giảm >15% / 20 phiên mà chưa có nền → loại |
| Khỏe hơn VNINDEX | RS ≥ 0 (hoặc breakout mạnh được ngoại lệ nhẹ) |
| Đáy trước thị trường | `Shakeout` → cộng điểm |

**Điểm pass:** ≥ 60 và (tích lũy **hoặc** breakout+volume **hoặc** shakeout).

**Top list:** tối đa 30 mã → `DailyOpportunities` cho ngày giao dịch kế tiếp.

## Không có trong data KBS (chưa implement)

- Khối ngoại / tự doanh mua ròng 1 tuần (Smoney)
- MA20/50/100/200 stack đầy đủ (có thể thêm sau từ `HistoryJson`)
- Xác nhận "quỹ nào mua" — chỉ suy từ giá + volume
