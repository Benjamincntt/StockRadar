import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';

/// Nền gradient giống `.wave-bg` trên mobile web.
class WaveBackground extends StatelessWidget {
  const WaveBackground({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: isDark ? AppColors.darkBackground : AppColors.lightBackground,
        gradient: isDark
            ? null
            : const LinearGradient(
                begin: Alignment.topRight,
                end: Alignment.bottomLeft,
                colors: [Color(0xFFF8F9FF), Color(0xFFF8F9FF)],
              ),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: [
          if (isDark) ...[
            Positioned(
              right: -40,
              top: -40,
              child: Container(
                width: 220,
                height: 220,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: AppColors.darkPrimary.withValues(alpha: 0.05),
                ),
              ),
            ),
            Positioned(
              left: -20,
              bottom: 80,
              child: Container(
                width: 180,
                height: 180,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: AppColors.darkSecondary.withValues(alpha: 0.06),
                ),
              ),
            ),
          ] else ...[
            Positioned(
              right: 0,
              top: 0,
              child: Container(
                width: 260,
                height: 200,
                decoration: BoxDecoration(
                  gradient: RadialGradient(
                    colors: [const Color(0xFF00C076).withValues(alpha: 0.06), Colors.transparent],
                  ),
                ),
              ),
            ),
            Positioned(
              left: 0,
              bottom: 0,
              child: Container(
                width: 220,
                height: 180,
                decoration: BoxDecoration(
                  gradient: RadialGradient(
                    colors: [const Color(0xFF94A3B8).withValues(alpha: 0.08), Colors.transparent],
                  ),
                ),
              ),
            ),
          ],
          child,
        ],
      ),
    );
  }
}
