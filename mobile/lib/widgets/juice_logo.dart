import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../services/app_services.dart';

class JuiceLogo extends StatelessWidget {
  const JuiceLogo({super.key, this.size = 200});

  final double size;

  @override
  Widget build(BuildContext context) {
    final isDark = context.watch<ThemeService>().isDark;
    final asset = isDark ? 'assets/juice-logo-dark.png' : 'assets/juice-logo.png';
    return Center(
      child: Image.asset(
        asset,
        width: size,
        fit: BoxFit.contain,
        filterQuality: FilterQuality.high,
      ),
    );
  }
}
