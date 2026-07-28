import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Bộ màu + nhãn cho badge trạng thái job.
class JobStateStyle {
  const JobStateStyle({
    required this.label,
    required this.foreground,
    required this.background,
  });

  final String label;
  final Color foreground;
  final Color background;
}

/// Map `status` (idle | success | failed) hoặc trạng thái đang chạy → bộ màu theo theme.
JobStateStyle jobStateStyle(BuildContext context, String status, {bool running = false}) {
  final scheme = Theme.of(context).colorScheme;
  final isDark = Theme.of(context).brightness == Brightness.dark;

  if (running) {
    return JobStateStyle(
      label: 'Đang chạy',
      foreground: isDark ? AppColors.darkWarning : AppColors.lightWarning,
      background: AppColors.amberBg(context),
    );
  }

  switch (status) {
    case 'success':
      return JobStateStyle(
        label: 'Thành công',
        foreground: scheme.primary,
        background: AppColors.positiveDim(context),
      );
    case 'failed':
      return JobStateStyle(
        label: 'Lỗi',
        foreground: scheme.error,
        background: AppColors.negativeDim(context),
      );
    default:
      return JobStateStyle(
        label: 'Chưa chạy',
        foreground: scheme.onSurfaceVariant,
        background: AppColors.neutralBg(context),
      );
  }
}
