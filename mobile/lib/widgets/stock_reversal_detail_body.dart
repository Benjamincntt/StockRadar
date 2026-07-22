import 'package:flutter/material.dart';

import '../core/labels/reversal_bounce_labels.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import 'glass_card.dart';
import 'score_pill.dart';

/// Body màn chi tiết theo chế độ Sóng hồi (thay toàn bộ nội dung tăng trưởng).
class StockReversalDetailBody extends StatelessWidget {
  const StockReversalDetailBody({super.key, required this.detail});

  final ReversalCandidateDetail detail;

  ReversalCandidate get c => detail.current;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final stageColor = ReversalBounceLabels.stageColor(context, c.stage);

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
      children: [
        _card(
          context,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Trạng thái sóng hồi',
                          style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                        ),
                        const SizedBox(height: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                          decoration: BoxDecoration(
                            color: stageColor.withValues(alpha: 0.14),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            ReversalBounceLabels.stage(c.stage),
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w700,
                              color: stageColor,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  ScorePill(c.totalScore),
                ],
              ),
              const SizedBox(height: 12),
              _regimeLine(context),
              if (c.isActionable) ...[
                const SizedBox(height: 10),
                Text(
                  'Có thể vào lệnh (đã qua hard gate)',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: scheme.primary),
                ),
              ],
              if (c.recoveryAttemptCount > 1 ||
                  (c.capitulationDate != null && c.capitulationDate!.isNotEmpty)) ...[
                const SizedBox(height: 8),
                Text(
                  [
                    if (c.recoveryAttemptCount > 1) 'Lần hồi thứ ${c.recoveryAttemptCount}',
                    if (c.capitulationDate != null && c.capitulationDate!.isNotEmpty)
                      'Bán tháo: ${c.capitulationDate}',
                  ].join(' · '),
                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
              ],
            ],
          ),
        ),
        _card(
          context,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SectionTitle('Điểm 6 trục'),
              const SizedBox(height: 12),
              _componentGrid(context),
            ],
          ),
        ),
        if (c.reasons.isNotEmpty)
          _card(
            context,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SectionTitle('Bằng chứng (${c.reasons.length})'),
                const SizedBox(height: 10),
                ...c.reasons.map((r) => _reasonRow(context, r)),
              ],
            ),
          ),
        if (c.hasTradePlan)
          _card(
            context,
            child: _tradePlan(context),
          ),
        if (c.riskWarnings.isNotEmpty)
          _card(
            context,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Cảnh báo rủi ro',
                    style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: scheme.error)),
                const SizedBox(height: 8),
                ...c.riskWarnings.map(
                  (w) => Padding(
                    padding: const EdgeInsets.only(bottom: 4),
                    child: Text('• $w', style: TextStyle(fontSize: 12, color: scheme.onSurface)),
                  ),
                ),
              ],
            ),
          ),
        if (detail.history.isNotEmpty)
          _card(
            context,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SectionTitle('Lịch sử stage'),
                const SizedBox(height: 8),
                ...detail.history.take(10).map((h) => Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Row(
                        children: [
                          Expanded(
                            child: Text(
                              h.tradingDate,
                              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                            ),
                          ),
                          Text(
                            ReversalBounceLabels.stage(h.stage),
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: ReversalBounceLabels.stageColor(context, h.stage),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            h.totalScore.toStringAsFixed(0),
                            style: dataFont(context, size: 12, weight: FontWeight.w700),
                          ),
                        ],
                      ),
                    )),
              ],
            ),
          ),
      ],
    );
  }

  Widget _card(BuildContext context, {required Widget child}) => Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: GlassCard(solid: true, child: child),
      );

  Widget _regimeLine(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = ReversalBounceLabels.regimeColor(context, c.marketRegime);
    return Row(
      children: [
        Container(width: 8, height: 8, decoration: BoxDecoration(shape: BoxShape.circle, color: color)),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            'Thị trường: ${ReversalBounceLabels.regime(c.marketRegime)}',
            style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
          ),
        ),
      ],
    );
  }

  Widget _componentGrid(BuildContext context) {
    double valueOf(String key) {
      final s = c.componentScores;
      switch (key) {
        case 'capitulation':
          return s.capitulation;
        case 'stabilization':
          return s.stabilization;
        case 'demand':
          return s.demand;
        case 'relativeStrength':
          return s.relativeStrength;
        case 'liquidity':
          return s.liquidity;
        case 'riskPenalty':
          return s.riskPenalty;
        default:
          return 0;
      }
    }

    final cells = ReversalBounceLabels.componentLabels
        .map((e) => _cell(context, e.$2, valueOf(e.$1), penalty: e.$1 == 'riskPenalty'))
        .toList();

    return Column(
      children: [
        Row(children: [Expanded(child: cells[0]), const SizedBox(width: 6), Expanded(child: cells[1]), const SizedBox(width: 6), Expanded(child: cells[2])]),
        const SizedBox(height: 6),
        Row(children: [Expanded(child: cells[3]), const SizedBox(width: 6), Expanded(child: cells[4]), const SizedBox(width: 6), Expanded(child: cells[5])]),
      ],
    );
  }

  Widget _cell(BuildContext context, String label, double value, {bool penalty = false}) {
    final scheme = Theme.of(context).colorScheme;
    final color = penalty && value != 0 ? scheme.error : scheme.onSurface;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 6),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        children: [
          Text(
            value.toStringAsFixed(value.abs() < 10 && value % 1 != 0 ? 1 : 0),
            style: dataFont(context, size: 15, weight: FontWeight.w700, color: color),
          ),
          const SizedBox(height: 2),
          Text(label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
        ],
      ),
    );
  }

  Widget _reasonRow(BuildContext context, ReversalReason r) {
    final scheme = Theme.of(context).colorScheme;
    final color = r.pass ? scheme.primary : scheme.onSurfaceVariant;
    final detail = r.threshold != null
        ? '${r.numericValue.toStringAsFixed(2)} / ${r.threshold!.toStringAsFixed(2)}'
        : r.numericValue.toStringAsFixed(2);
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(r.pass ? Icons.check_circle : Icons.remove_circle_outline, size: 16, color: color),
          const SizedBox(width: 8),
          Expanded(child: Text(r.label, style: TextStyle(fontSize: 12, color: scheme.onSurface))),
          Text(detail, style: dataFont(context, size: 11, weight: FontWeight.w500, color: scheme.onSurfaceVariant)),
        ],
      ),
    );
  }

  Widget _tradePlan(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    Widget row(String label, String value, {Color? valueColor}) => Padding(
          padding: const EdgeInsets.only(bottom: 6),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(label, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
              Text(value,
                  style: dataFont(context, size: 13, weight: FontWeight.w600, color: valueColor ?? scheme.onSurface)),
            ],
          ),
        );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Kế hoạch giao dịch',
            style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700, color: scheme.primary)),
        const SizedBox(height: 10),
        row('Giá tham chiếu', formatPrice(c.entryReference!)),
        if (c.maxEntryPrice != null) row('Mua tối đa', formatPrice(c.maxEntryPrice!)),
        row('Ngưỡng mất hiệu lực', formatPrice(c.invalidationPrice!), valueColor: scheme.error),
        row('Mục tiêu 1', formatPrice(c.firstTarget!), valueColor: scheme.primary),
        if (c.rewardToRisk != null) row('Lãi/Lỗ (R:R)', '${c.rewardToRisk!.toStringAsFixed(2)}×'),
        if (c.positionFactor != null)
          row('Tỷ trọng gợi ý', '${(c.positionFactor! * 100).toStringAsFixed(0)}%'),
      ],
    );
  }
}
