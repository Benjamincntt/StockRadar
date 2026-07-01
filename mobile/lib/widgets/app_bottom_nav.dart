import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/theme/app_colors.dart';

class AppBottomNav extends StatelessWidget {
  const AppBottomNav({super.key, required this.currentIndex});

  /// -1 = không tab nào active (vd. màn chi tiết CP).
  final int currentIndex;

  static const _routes = ['/', '/alerts', '/watchlist', '/criteria'];
  static const _labels = ['Trang chủ', 'Lệnh realtime', 'Watchlist', 'Phân tích chỉ báo'];
  static const _icons = [
    Icons.home_outlined,
    Icons.notifications_outlined,
    Icons.star_outline,
    Icons.show_chart_outlined,
  ];
  static const _activeIcons = [
    Icons.home,
    Icons.notifications,
    Icons.star,
    Icons.show_chart,
  ];

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkSurface : AppColors.lightSurface,
        border: Border(top: BorderSide(color: scheme.outline.withValues(alpha: 0.2))),
        boxShadow: isDark
            ? [BoxShadow(color: Colors.black.withValues(alpha: 0.4), blurRadius: 20, offset: const Offset(0, -4))]
            : [BoxShadow(color: Colors.black.withValues(alpha: 0.05), blurRadius: 6, offset: const Offset(0, -4))],
      ),
      child: SafeArea(
        top: false,
        child: SizedBox(
          height: 64,
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: AppColors.maxContentWidth),
              child: Row(
                children: List.generate(4, (i) {
                  final active = currentIndex >= 0 && currentIndex == i;
                  return Expanded(
                    child: InkWell(
                      onTap: () {
                        if (!active) context.go(_routes[i]);
                      },
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(
                            active ? _activeIcons[i] : _icons[i],
                            size: 20,
                            color: active ? scheme.primary : scheme.onSurfaceVariant,
                          ),
                          const SizedBox(height: 2),
                          Text(
                            _labels[i],
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: active ? FontWeight.w600 : FontWeight.w500,
                              color: active ? scheme.primary : scheme.onSurfaceVariant,
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                }),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
