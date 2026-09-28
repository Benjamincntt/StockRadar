import 'package:flutter/material.dart';

/// Định nghĩa DUY NHẤT về giá trần / giá sàn cho toàn bộ ứng dụng.
///
/// Mọi màn hình (nến trên biểu đồ chi tiết cổ phiếu, card "Tín hiệu mới nhất"
/// ở trang chủ, ...) đều dùng chung ngưỡng, cách nhận diện và màu sắc khai báo
/// tại đây — không lặp logic/màu ở từng nơi nữa.
enum PriceLimit { none, tran, san }

class PriceLimits {
  const PriceLimits._();

  /// Ngưỡng biến động tối thiểu để coi là chạm trần/sàn.
  /// 6.5% nằm an toàn dưới biên HOSE 7%, vẫn bắt được HNX/UPCoM 15%.
  static const double threshold = 0.065;

  /// Dung sai râu nến ~0.06% bù cho giá làm tròn theo bước giá.
  static const double _wickEps = 0.0006;

  // Màu theo nền sáng/tối — nguồn duy nhất của màu trần/sàn trong app.
  static const Color tranDark = Color(0xFFBA68C8);
  static const Color sanDark = Color(0xFF2962FF);
  static const Color tranLight = Color(0xFF8E24AA);
  static const Color sanLight = Color(0xFF0D47A1);

  /// Nhận diện theo % thay đổi (đơn vị đã là phần trăm, ví dụ -6.97).
  static PriceLimit classifyByChangePercent(double changePercent) {
    final pct = changePercent / 100.0;
    if (pct >= threshold) return PriceLimit.tran;
    if (pct <= -threshold) return PriceLimit.san;
    return PriceLimit.none;
  }

  /// Nhận diện theo hình học nến: đóng cửa tại cực trị (không có râu vượt qua)
  /// và biến động so với giá tham chiếu (đóng cửa phiên/ nến trước) đủ biên.
  static PriceLimit classifyByBar({
    required double close,
    required double high,
    required double low,
    required double reference,
  }) {
    if (reference <= 0) return PriceLimit.none;
    final chg = (close - reference) / reference;
    final closedAtHigh = high - close <= reference * _wickEps;
    final closedAtLow = close - low <= reference * _wickEps;
    if (closedAtHigh && chg >= threshold) return PriceLimit.tran;
    if (closedAtLow && chg <= -threshold) return PriceLimit.san;
    return PriceLimit.none;
  }

  /// Màu tương ứng với trạng thái trần/sàn theo nền hiện tại.
  /// Trả về `null` khi [limit] == [PriceLimit.none] để caller dùng màu mặc định.
  static Color? colorOf(BuildContext context, PriceLimit limit) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return switch (limit) {
      PriceLimit.tran => isDark ? tranDark : tranLight,
      PriceLimit.san => isDark ? sanDark : sanLight,
      PriceLimit.none => null,
    };
  }
}
