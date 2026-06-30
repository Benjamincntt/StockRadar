import 'package:flutter/material.dart';

class ScorePill extends StatelessWidget {
  const ScorePill(this.value, {super.key});

  final double value;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: scheme.primary.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        value.toStringAsFixed(0),
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: scheme.primary,
        ),
      ),
    );
  }
}

class ChangePill extends StatelessWidget {
  const ChangePill(this.percent, {super.key});

  final double percent;

  @override
  Widget build(BuildContext context) {
    final positive = percent >= 0;
    final color = positive ? Theme.of(context).colorScheme.primary : Theme.of(context).colorScheme.error;
    final sign = positive ? '+' : '';
    return Text(
      '$sign${percent.toStringAsFixed(2)}%',
      style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: color),
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
        bg = scheme.primary.withValues(alpha: 0.2);
      case 'Watch':
        bg = scheme.secondary.withValues(alpha: 0.2);
      default:
        bg = scheme.error.withValues(alpha: 0.15);
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(6)),
      child: Text(
        recommendation!,
        style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w700),
      ),
    );
  }
}

String formatPrice(double price) {
  if (price >= 1000) return price.toStringAsFixed(0);
  return price.toStringAsFixed(2);
}

String formatAlertTime(String iso) {
  try {
    final dt = DateTime.parse(iso).toLocal();
    return '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  } catch (_) {
    return iso;
  }
}
