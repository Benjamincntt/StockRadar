import 'package:flutter/material.dart';

import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import 'glass_card.dart';

class AdvancedIndicatorsCard extends StatefulWidget {
  const AdvancedIndicatorsCard({super.key, required this.scores});

  final List<CriterionScore> scores;

  @override
  State<AdvancedIndicatorsCard> createState() => _AdvancedIndicatorsCardState();
}

class _AdvancedIndicatorsCardState extends State<AdvancedIndicatorsCard> {
  var _open = false;

  @override
  Widget build(BuildContext context) {
    if (widget.scores.isEmpty) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    final singles = widget.scores.where((p) => p.rank <= 10).toList();
    final bundles = widget.scores.where((p) => p.rank > 10 && p.rank <= 16).toList();
    final topOpp = widget.scores.where((p) => p.group == 'Top cơ hội').toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        InkWell(
          onTap: () => setState(() => _open = !_open),
          borderRadius: BorderRadius.circular(12),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              children: [
                Expanded(
                  child: SectionTitle(
                    'Chỉ báo nâng cao',
                    subtitle: _open ? 'Ẩn chi tiết' : 'Mở rộng để xem điểm chỉ báo đơn / bộ',
                  ),
                ),
                Text(_open ? 'Thu gọn' : 'Xem', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: scheme.primary)),
              ],
            ),
          ),
        ),
        if (_open) ...[
          const SizedBox(height: 8),
          Text('Top 10 chỉ báo đơn', style: labelCaps(context)),
          const SizedBox(height: 8),
          ...singles.map((item) => _CriterionRow(item: item)),
          const SizedBox(height: 12),
          Text('Bộ chỉ báo kết hợp', style: labelCaps(context)),
          const SizedBox(height: 8),
          ...bundles.map((item) => _CriterionRow(item: item, levelBadge: true)),
          const SizedBox(height: 12),
          Text('Top cơ hội — Buy Score', style: labelCaps(context)),
          const SizedBox(height: 8),
          ...topOpp.map((item) => _CriterionRow(item: item, opportunityBadge: true)),
        ],
      ],
    );
  }
}

class _CriterionRow extends StatelessWidget {
  const _CriterionRow({required this.item, this.levelBadge = false, this.opportunityBadge = false});

  final CriterionScore item;
  final bool levelBadge;
  final bool opportunityBadge;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final badgeLabel = opportunityBadge ? item.rank - 19 : levelBadge ? item.rank - 10 : item.rank;
    Color badgeFg = scheme.primary;
    Color badgeBg = AppColors.greenBg(context);
    if (opportunityBadge) {
      badgeFg = AppColors.darkWarning;
      badgeBg = AppColors.amberBg(context);
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: scheme.outlineVariant),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 24,
            height: 24,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: badgeBg, borderRadius: BorderRadius.circular(8)),
            child: Text('$badgeLabel', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: badgeFg)),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(child: Text(item.label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13))),
                    _BiasTag(item.bias),
                    const SizedBox(width: 8),
                    Text(
                      item.score.toStringAsFixed(0),
                      style: dataFont(context, size: 13, weight: FontWeight.w700),
                    ),
                  ],
                ),
                Text(item.group, style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
                if (item.summary.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(item.summary, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _BiasTag extends StatelessWidget {
  const _BiasTag(this.bias);
  final String bias;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    late final String label;
    late final Color bg;
    late final Color fg;
    switch (bias) {
      case 'Bullish':
        label = 'Tăng';
        bg = AppColors.greenBg(context);
        fg = scheme.primary;
      case 'Bearish':
        label = 'Giảm';
        bg = AppColors.redBg(context);
        fg = scheme.error;
      default:
        label = 'Trung tính';
        bg = AppColors.neutralBg(context);
        fg = scheme.onSurfaceVariant;
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: TextStyle(fontSize: 10, fontWeight: FontWeight.w600, color: fg)),
    );
  }
}
