import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/services/app_services.dart';

enum JuiceLogoVariant { mark, full }

enum JuiceLogoSize { sm, md, lg, xl }

/// Logo JUICE (assets/juice-logo*.png) — mark cho AppBar, full cho login/splash.
class JuiceLogo extends StatelessWidget {
  const JuiceLogo({
    super.key,
    this.variant = JuiceLogoVariant.full,
    this.size = JuiceLogoSize.md,
    this.isDarkOverride,
  });

  final JuiceLogoVariant variant;
  final JuiceLogoSize size;
  /// Dùng khi chưa có [ThemeService] trong context (splash bootstrap).
  final bool? isDarkOverride;

  double _markHeight() => switch (size) {
        JuiceLogoSize.sm => 36,
        JuiceLogoSize.md => 44,
        JuiceLogoSize.lg => 52,
        JuiceLogoSize.xl => 60,
      };

  double _fullMaxWidth(BuildContext context) => switch (size) {
        JuiceLogoSize.sm => 140,
        JuiceLogoSize.md => 168,
        JuiceLogoSize.lg => 220,
        JuiceLogoSize.xl => math.min(MediaQuery.sizeOf(context).width - 48, 360),
      };

  @override
  Widget build(BuildContext context) {
    final isDark = isDarkOverride ?? context.watch<ThemeService>().isDark;
    final asset = isDark ? 'assets/juice-logo-dark.png' : 'assets/juice-logo.png';

    if (variant == JuiceLogoVariant.mark) {
      return SizedBox(
        width: 44,
        height: _markHeight(),
        child: Image.asset(
          asset,
          fit: BoxFit.contain,
          alignment: Alignment.center,
          filterQuality: FilterQuality.high,
          semanticLabel: 'JUICE',
        ),
      );
    }

    final scheme = Theme.of(context).colorScheme;
    final maxW = _fullMaxWidth(context);
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth.isFinite ? constraints.maxWidth.clamp(120.0, maxW) : maxW;
        final image = Image.asset(
          asset,
          width: width,
          fit: BoxFit.contain,
          filterQuality: FilterQuality.high,
          semanticLabel: 'JUICE',
        );
        if (!isDark) return Center(child: image);
        return Center(
          child: DecoratedBox(
            decoration: BoxDecoration(
              boxShadow: [BoxShadow(color: scheme.primary.withValues(alpha: 0.22), blurRadius: 28)],
            ),
            child: image,
          ),
        );
      },
    );
  }
}
