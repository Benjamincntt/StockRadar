import 'package:flutter/material.dart';

import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import 'glass_card.dart';
import 'score_pill.dart';

/// Điểm vào lệnh ở trạng thái Ready/Watch mới hiển thị các mức giá.
bool showsPriceLevels(EntryPoint entry) =>
    entry.status == 'Ready' || entry.status == 'Watch';

class PriceLevelsCard extends StatelessWidget {
  const PriceLevelsCard({
    super.key,
    required this.entry,
    required this.buyZone,
    required this.stopLoss,
    required this.resistance,
    required this.target,
  });

  final EntryPoint entry;
  final double buyZone;
  final double stopLoss;
  final double resistance;
  final double target;

  @override
  Widget build(BuildContext context) {
    if (!showsPriceLevels(entry)) return const SizedBox.shrink();

    final fromEntry = entry.entryPrice > 0 ||
        entry.stopLoss > 0 ||
        entry.triggerPrice > 0 ||
        entry.targetPrice > 0;

    final cells = [
      _PriceBoxData('Giá vào', entry.entryPrice > 0 ? entry.entryPrice : buyZone, accent: true),
      _PriceBoxData('Cắt lỗ', entry.stopLoss > 0 ? entry.stopLoss : stopLoss, danger: true),
      _PriceBoxData('Kích hoạt', entry.triggerPrice > 0 ? entry.triggerPrice : resistance),
      _PriceBoxData('Mục tiêu', entry.targetPrice > 0 ? entry.targetPrice : target, accent: true),
    ].where((c) => c.value > 0).toList();

    if (cells.isEmpty) return const SizedBox.shrink();

    final scheme = Theme.of(context).colorScheme;
    final subtitle = fromEntry
        ? 'Mức từ điểm vào lệnh'
        : 'Tham chiếu nhanh (20 phiên)';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionTitle('Các mức giá', subtitle: subtitle),
        const SizedBox(height: 12),
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          childAspectRatio: 1.55,
          children: cells.map((c) => _PriceBox(data: c)).toList(),
        ),
        if (entry.riskRewardRatio > 0) ...[
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text('R:R ', style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
              Text(
                '1 : ${entry.riskRewardRatio.toStringAsFixed(1)}',
                style: dataFont(context, size: 13, weight: FontWeight.w700),
              ),
            ],
          ),
        ],
      ],
    );
  }
}

class _PriceBoxData {
  const _PriceBoxData(this.label, this.value, {this.danger = false, this.accent = false});
  final String label;
  final double value;
  final bool danger;
  final bool accent;
}

class _PriceBox extends StatelessWidget {
  const _PriceBox({required this.data});
  final _PriceBoxData data;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = data.danger ? scheme.error : data.accent ? scheme.primary : scheme.onSurface;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(data.label, style: labelCaps(context)),
          const SizedBox(height: 4),
          Text(
            formatPrice(data.value),
            style: dataFont(context, size: 18, weight: FontWeight.w700, color: color),
          ),
        ],
      ),
    );
  }
}

class SignalChips extends StatelessWidget {
  const SignalChips({super.key, required this.signals});

  final List<String> signals;

  @override
  Widget build(BuildContext context) {
    if (signals.isEmpty) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    return Wrap(
      spacing: 6,
      runSpacing: 6,
      children: signals.map((s) {
        return Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: AppColors.positiveDim(context),
            borderRadius: BorderRadius.circular(999),
          ),
          child: Text(
            '✓ $s',
            style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: scheme.primary),
          ),
        );
      }).toList(),
    );
  }
}
