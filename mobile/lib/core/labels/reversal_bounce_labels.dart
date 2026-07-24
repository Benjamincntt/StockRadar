import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Nhãn + màu trung tính cho các stage/regime của luồng Sóng hồi (counter-trend).
/// Dùng ngôn ngữ mô tả trạng thái, tránh hàm ý khuyến nghị mua/bán.
class ReversalBounceLabels {
  const ReversalBounceLabels._();

  static String stage(String stage) {
    switch (stage) {
      case 'Capitulating':
        return 'Đang bán tháo';
      case 'Stabilizing':
        return 'Đang cân bằng';
      case 'Confirmed':
        return 'Đang xác nhận hồi';
      case 'Invalidated':
        return 'Mất hiệu lực';
      default:
        return 'Chưa có tín hiệu';
    }
  }

  static Color stageColor(BuildContext context, String stage) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    switch (stage) {
      case 'Capitulating':
        return scheme.error;
      case 'Stabilizing':
        return isDark ? AppColors.darkWarning : AppColors.lightWarning;
      case 'Confirmed':
        return scheme.primary;
      default:
        return scheme.onSurfaceVariant;
    }
  }

  static String regime(String regime) {
    switch (regime) {
      // Cùng pha Top / VNINDEX (MarketPhaseClassifier)
      case 'Favorable':
        return 'TT thuận';
      case 'Neutral':
        return 'Nỗ lực hồi phục';
      case 'Unfavorable':
        return 'Điều chỉnh';
      // Legacy breadth (không còn dùng làm nhãn Thị trường)
      case 'Panic':
        return 'Hoảng loạn';
      case 'Stabilizing':
        return 'Đang cân bằng';
      case 'ReboundConfirmed':
        return 'Xác nhận hồi';
      case 'Normal':
        return 'Bình thường';
      default:
        return regime;
    }
  }

  static Color regimeColor(BuildContext context, String regime) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    switch (regime) {
      case 'Favorable':
        return scheme.primary;
      case 'Neutral':
        return isDark ? AppColors.darkWarning : AppColors.lightWarning;
      case 'Unfavorable':
      case 'Panic':
        return scheme.error;
      case 'Stabilizing':
        return isDark ? AppColors.darkWarning : AppColors.lightWarning;
      case 'ReboundConfirmed':
        return scheme.primary;
      default:
        return scheme.onSurfaceVariant;
    }
  }

  /// Nhãn 6 trục điểm §5 (thứ tự cố định để hiển thị grid).
  static const List<(String, String)> componentLabels = [
    ('capitulation', 'Bán tháo'),
    ('stabilization', 'Cân bằng'),
    ('demand', 'Lực cầu'),
    ('relativeStrength', 'Sức mạnh'),
    ('liquidity', 'Thanh khoản'),
    ('riskPenalty', 'Rủi ro'),
  ];
}
