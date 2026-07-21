import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/labels/reversal_bounce_labels.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import 'score_pill.dart';

/// Card 1 ứng viên Sóng hồi: stage badge + 6 trục điểm §5 + bằng chứng + trade plan (Stage C).
class ReversalBounceCard extends StatelessWidget {
  const ReversalBounceCard({super.key, required this.candidate});

  final ReversalCandidate candidate;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final stageColor = ReversalBounceLabels.stageColor(context, candidate.stage);

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: () => context.push('/stocks/${candidate.symbol}'),
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.surfaceLow(context),
            borderRadius: BorderRadius.circular(16),
            border: Border(left: BorderSide(color: stageColor, width: 3)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(candidate.symbol,
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                  const SizedBox(width: 8),
                  _stageBadge(context, stageColor),
                  const Spacer(),
                  ScorePill(candidate.totalScore),
                ],
              ),
              const SizedBox(height: 6),
              _subline(context),
              const SizedBox(height: 12),
              _componentGrid(context),
              if (candidate.reasons.isNotEmpty) ...[
                const SizedBox(height: 4),
                _evidence(context),
              ],
              if (candidate.stage == 'Confirmed' && candidate.hasTradePlan) ...[
                const SizedBox(height: 12),
                _tradePlan(context),
              ],
              if (candidate.riskWarnings.isNotEmpty) ...[
                const SizedBox(height: 10),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: candidate.riskWarnings
                      .map((w) => Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                            decoration: BoxDecoration(
                              color: scheme.error.withValues(alpha: 0.12),
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: Text(w,
                                style: TextStyle(
                                    fontSize: 10, fontWeight: FontWeight.w600, color: scheme.error)),
                          ))
                      .toList(),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _stageBadge(BuildContext context, Color color) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.14),
          borderRadius: BorderRadius.circular(6),
        ),
        child: Text(
          ReversalBounceLabels.stage(candidate.stage),
          style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: color),
        ),
      );

  Widget _subline(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final parts = <String>[];
    if (candidate.recoveryAttemptCount > 1) {
      parts.add('Lần hồi thứ ${candidate.recoveryAttemptCount}');
    }
    if (candidate.capitulationDate != null && candidate.capitulationDate!.isNotEmpty) {
      parts.add('Bán tháo: ${candidate.capitulationDate}');
    }
    return Row(
      children: [
        if (candidate.isActionable) ...[
          Container(
            width: 7,
            height: 7,
            decoration: BoxDecoration(shape: BoxShape.circle, color: scheme.primary),
          ),
          const SizedBox(width: 6),
          Text('Có thể hành động',
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: scheme.primary)),
        ] else
          Text('Theo dõi',
              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
        if (parts.isNotEmpty) ...[
          const SizedBox(width: 8),
          Expanded(
            child: Text('· ${parts.join(' · ')}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
          ),
        ],
      ],
    );
  }

  Widget _componentGrid(BuildContext context) {
    double valueOf(String key) {
      final c = candidate.componentScores;
      switch (key) {
        case 'capitulation':
          return c.capitulation;
        case 'stabilization':
          return c.stabilization;
        case 'demand':
          return c.demand;
        case 'relativeStrength':
          return c.relativeStrength;
        case 'liquidity':
          return c.liquidity;
        case 'riskPenalty':
          return c.riskPenalty;
        default:
          return 0;
      }
    }

    final cells = ReversalBounceLabels.componentLabels
        .map((e) => _componentCell(context, e.$2, valueOf(e.$1), penalty: e.$1 == 'riskPenalty'))
        .toList();

    return Column(
      children: [
        Row(children: _spread(cells.sublist(0, 3))),
        const SizedBox(height: 6),
        Row(children: _spread(cells.sublist(3, 6))),
      ],
    );
  }

  List<Widget> _spread(List<Widget> cells) {
    final out = <Widget>[];
    for (var i = 0; i < cells.length; i++) {
      out.add(Expanded(child: cells[i]));
      if (i != cells.length - 1) out.add(const SizedBox(width: 6));
    }
    return out;
  }

  Widget _componentCell(BuildContext context, String label, double value, {bool penalty = false}) {
    final scheme = Theme.of(context).colorScheme;
    final color = penalty && value != 0 ? scheme.error : scheme.onSurface;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 6),
      decoration: BoxDecoration(
        color: scheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        children: [
          Text(
            value.toStringAsFixed(value.abs() < 10 && value % 1 != 0 ? 1 : 0),
            style: dataFont(context, size: 14, weight: FontWeight.w700, color: color),
          ),
          const SizedBox(height: 2),
          Text(label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 9.5, color: scheme.onSurfaceVariant)),
        ],
      ),
    );
  }

  Widget _evidence(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Theme(
      data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
      child: ExpansionTile(
        tilePadding: EdgeInsets.zero,
        childrenPadding: const EdgeInsets.only(bottom: 8),
        expandedCrossAxisAlignment: CrossAxisAlignment.start,
        title: Text('Bằng chứng (${candidate.reasons.length})',
            style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: scheme.onSurfaceVariant)),
        children: candidate.reasons.map((r) {
          final color = r.pass ? scheme.primary : scheme.onSurfaceVariant;
          final detail = r.threshold != null
              ? '${r.numericValue.toStringAsFixed(2)} / ${r.threshold!.toStringAsFixed(2)}'
              : r.numericValue.toStringAsFixed(2);
          return Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(r.pass ? Icons.check_circle : Icons.remove_circle_outline, size: 14, color: color),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(r.label, style: TextStyle(fontSize: 11, color: scheme.onSurface)),
                ),
                const SizedBox(width: 6),
                Text(detail,
                    style: dataFont(context, size: 10, weight: FontWeight.w500, color: scheme.onSurfaceVariant)),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _tradePlan(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    Widget row(String label, String value, {Color? valueColor}) => Padding(
          padding: const EdgeInsets.only(bottom: 4),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(label, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
              Text(value,
                  style: dataFont(context, size: 12, weight: FontWeight.w600, color: valueColor ?? scheme.onSurface)),
            ],
          ),
        );

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: scheme.primary.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: scheme.primary.withValues(alpha: 0.25)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Kế hoạch giao dịch',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: scheme.primary)),
          const SizedBox(height: 8),
          row('Giá tham chiếu', formatPrice(candidate.entryReference!)),
          if (candidate.maxEntryPrice != null)
            row('Mua tối đa', formatPrice(candidate.maxEntryPrice!)),
          row('Ngưỡng mất hiệu lực', formatPrice(candidate.invalidationPrice!), valueColor: scheme.error),
          row('Mục tiêu 1', formatPrice(candidate.firstTarget!), valueColor: scheme.primary),
          if (candidate.rewardToRisk != null)
            row('Lãi/Lỗ (R:R)', '${candidate.rewardToRisk!.toStringAsFixed(2)}×'),
          if (candidate.positionFactor != null)
            row('Tỷ trọng gợi ý', '${(candidate.positionFactor! * 100).toStringAsFixed(0)}%'),
        ],
      ),
    );
  }
}
