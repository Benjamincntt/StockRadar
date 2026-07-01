import 'package:flutter/material.dart';

/// Tokens khớp mobile web (`frontend/src/index.css`).
class AppColors {
  // Light — Silver & Wave Pro
  static const lightBackground = Color(0xFFF8F9FF);
  static const lightSurface = Color(0xFFF8F9FF);
  static const lightSurfaceLow = Color(0xFFEFF4FF);
  static const lightSurfaceLowest = Color(0xFFFFFFFF);
  static const lightSurfaceHigh = Color(0xFFDCE9FF);
  static const lightPrimary = Color(0xFF006D41);
  static const lightPrimaryContainer = Color(0xFF00C076);
  static const lightSecondary = Color(0xFF516072);
  static const lightOnSurface = Color(0xFF0B1C30);
  static const lightOnSurfaceVariant = Color(0xFF3C4A40);
  static const lightError = Color(0xFFBA1A1A);
  static const lightWarning = Color(0xFFB45309);
  static const lightOutline = Color(0xFF6C7B6F);
  static const lightOutlineVariant = Color(0xFFBBCABD);
  static const lightPositiveDim = Color(0x1A006D41);
  static const lightNegativeDim = Color(0x1ABA1A1A);
  static const lightGlassBg = Color(0xBFFFFFFF);
  static const lightHeaderBg = Color(0xCCF8F9FF);

  // Dark — Obsidian Flow
  static const darkBackground = Color(0xFF111319);
  static const darkSurface = Color(0xFF1E1F26);
  static const darkSurfaceLow = Color(0xFF191B22);
  static const darkSurfaceLowest = Color(0xFF0C0E14);
  static const darkSurfaceHigh = Color(0xFF282A30);
  static const darkPrimary = Color(0xFF00F2FF);
  static const darkPrimaryContainer = Color(0xFF00F2FF);
  static const darkSecondary = Color(0xFFCE5DFF);
  static const darkOnSurface = Color(0xFFE2E2EB);
  static const darkOnSurfaceVariant = Color(0xFFB9CACB);
  static const darkError = Color(0xFFFF6B4A);
  static const darkWarning = Color(0xFFEBB2FF);
  static const darkOutline = Color(0xFF849495);
  static const darkOutlineVariant = Color(0xFF3A494B);
  static const darkPositiveDim = Color(0x1A00F2FF);
  static const darkNegativeDim = Color(0x24FF6B4A);
  static const darkGlassBg = Color(0xB81E1F26);
  static const darkHeaderBg = Color(0xD9111319);

  static const maxContentWidth = 430.0;

  static Color surfaceLow(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkSurfaceLow : lightSurfaceLow;

  static Color surfaceHigh(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkSurfaceHigh : lightSurfaceHigh;

  static Color positiveDim(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkPositiveDim : lightPositiveDim;

  static Color negativeDim(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkNegativeDim : lightNegativeDim;

  static Color glassBg(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkGlassBg : lightGlassBg;

  static Color headerBg(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkHeaderBg : lightHeaderBg;

  static Color surfaceLowest(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? darkSurfaceLowest : lightSurfaceLowest;

  static Color greenBg(BuildContext context) => positiveDim(context);

  static Color redBg(BuildContext context) => negativeDim(context);

  static Color amberBg(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final warning = isDark ? darkWarning : lightWarning;
    return warning.withValues(alpha: 0.12);
  }

  static Color neutralBg(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return isDark ? Colors.white.withValues(alpha: 0.05) : lightSurfaceLow;
  }
}
