import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';

class ScorePill extends StatelessWidget {
  const ScorePill(this.value, {super.key});

  final double value;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    Color bg;
    Color fg;
    if (value >= 85) {
      bg = AppColors.positiveDim(context);
      fg = scheme.primary;
    } else if (value >= 70) {
      bg = scheme.primary.withValues(alpha: 0.12);
      fg = scheme.primary;
    } else {
      bg = isDark ? AppColors.darkWarning.withValues(alpha: 0.12) : AppColors.lightWarning.withValues(alpha: 0.12);
      fg = isDark ? AppColors.darkWarning : AppColors.lightWarning;
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(8)),
      child: Text(
        value.toStringAsFixed(0),
        style: dataFont(context, size: 12, weight: FontWeight.w700, color: fg),
      ),
    );
  }
}

class PredictedHitPill extends StatelessWidget {
  const PredictedHitPill({super.key, this.percent, this.sampleCount = 0});

  final double? percent;
  final int sampleCount;

  @override
  Widget build(BuildContext context) {
    if (percent == null || percent! <= 0) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: scheme.onSurfaceVariant.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        'P ${percent!.toStringAsFixed(0)}%${sampleCount > 0 ? ' · n=$sampleCount' : ''}',
        style: dataFont(context, size: 10, weight: FontWeight.w600, color: scheme.onSurfaceVariant),
      ),
    );
  }
}

class ChangePill extends StatelessWidget {
  const ChangePill(this.percent, {super.key});

  final double percent;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final positive = percent >= 0;
    final color = positive ? scheme.primary : scheme.error;
    final bg = positive ? AppColors.positiveDim(context) : AppColors.negativeDim(context);
    final sign = positive ? '+' : '';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(8)),
      child: Text(
        '$sign${percent.toStringAsFixed(2)}%',
        style: dataFont(context, size: 13, weight: FontWeight.w600, color: color),
      ),
    );
  }
}

class FilterChips extends StatelessWidget {
  const FilterChips({
    super.key,
    required this.options,
    required this.selected,
    required this.onSelected,
  });

  final List<String> options;
  final String selected;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: options.map((label) {
          final active = selected == label;
          return Padding(
            padding: const EdgeInsets.only(right: 8),
            child: FilterChip(
              label: Text(label, style: TextStyle(fontSize: 12, fontWeight: active ? FontWeight.w600 : FontWeight.w500)),
              selected: active,
              showCheckmark: false,
              onSelected: (_) => onSelected(label),
              selectedColor: scheme.primary,
              backgroundColor: AppColors.surfaceLow(context),
              labelStyle: TextStyle(color: active ? (Theme.of(context).brightness == Brightness.dark ? const Color(0xFF002022) : Colors.white) : scheme.onSurfaceVariant),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
              side: BorderSide(color: scheme.outline.withValues(alpha: 0.25)),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class RecommendationBadge extends StatelessWidget {
  const RecommendationBadge(this.recommendation, {super.key});

  final String? recommendation;

  @override
  Widget build(BuildContext context) {
    if (recommendation == null || recommendation!.isEmpty) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    Color bg;
    switch (recommendation) {
      case 'StrongBuy':
        bg = AppColors.positiveDim(context);
      case 'Watch':
        bg = scheme.secondary.withValues(alpha: 0.2);
      default:
        bg = AppColors.negativeDim(context);
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(6)),
      child: Text(recommendation!, style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w700)),
    );
  }
}

String formatPrice(double price) {
  if (price >= 1000) return price.toStringAsFixed(0);
  return price.toStringAsFixed(2);
}

String formatPercent(double value) {
  final sign = value > 0 ? '+' : '';
  return '$sign${value.toStringAsFixed(2)}%';
}

String formatAlertTime(String iso) {
  try {
    final local = parseApiDateUtc(iso).toLocal();
    return '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
  } catch (_) {
    return iso;
  }
}
