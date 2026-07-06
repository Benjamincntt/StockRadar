import 'package:flutter/material.dart';

import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/labels/trade_state_labels.dart';
import 'glass_card.dart';
import 'score_pill.dart';

const _entryStatusLabels = {
  'Ready': 'Vào ngay',
  'Watch': 'Chờ kích hoạt',
  'Late': 'Trễ / FOMO',
  'Invalid': 'Không vào',
};

const _entryTypeLabels = {
  'None': '',
  'Breakout': 'Breakout',
  'DarvasBreakout': 'Phá vỡ nền giá',
  'Shakeout': 'Shakeout',
};

bool showsEntryPointCard(EntryPoint entry) =>
    entry.status == 'Ready' || entry.status == 'Watch' || entry.status == 'Late';

bool showsPriceLevels(EntryPoint entry) =>
    entry.status == 'Ready' || entry.status == 'Watch';

bool showsMergedInvalidCard(BuyDecision decision) =>
    !showsEntryPointCard(decision.entryPoint) && decision.entryPoint.checklist.isNotEmpty;

List<BuyScoreComponent> visibleBreakdown(BuyDecision decision) {
  if (decision.gateFailure == null || decision.gateFailure!.isEmpty) {
    return decision.breakdown;
  }
  final incomplete = decision.breakdown
      .where((item) => item.points < item.maxPoints)
      .toList();
  if (incomplete.isNotEmpty) return incomplete;
  return decision.breakdown.where((item) => item.points <= 0).toList();
}

bool showsEntryPointCardForDecision(BuyDecision decision) {
  final trade = resolveBuyDecisionTradeState(decision);
  if (trade.state == 'Avoid') return false;
  return showsEntryPointCard(decision.entryPoint);
}

class BuyDecisionCard extends StatefulWidget {
  const BuyDecisionCard({super.key, required this.decision});

  final BuyDecision decision;

  @override
  State<BuyDecisionCard> createState() => _BuyDecisionCardState();
}

class _BuyDecisionCardState extends State<BuyDecisionCard> {
  var _showBreakdown = false;

  @override
  Widget build(BuildContext context) {
    final d = widget.decision;
    if (d.buyScore == null && d.recommendation == null) {
      return EntryPointCard(entry: d.entryPoint);
    }

    if (showsMergedInvalidCard(d)) {
      return _MergedInsufficientCard(
        decision: d,
        showBreakdown: _showBreakdown,
        onToggleBreakdown: () => setState(() => _showBreakdown = !_showBreakdown),
      );
    }

    final trade = resolveBuyDecisionTradeState(d);
    final style = tradeStateStyle(context, trade.state);
    final breakdown = visibleBreakdown(d);
    final hasHardGate = d.gateFailure != null && d.gateFailure!.isNotEmpty;
    final displayScore = hasHardGate ? (d.actionScore ?? 0) : (d.buyScore ?? 0);
    final scoreSuffix = hasHardGate ? 'điểm hành động' : '/ 100';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          decoration: BoxDecoration(
            color: style.bg,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: style.border),
          ),
          clipBehavior: Clip.antiAlias,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Buy Score', style: labelCaps(context)),
                          const SizedBox(height: 4),
                          Text(
                            d.passesTopFilter
                                ? 'Đạt bộ lọc Top cơ hội'
                                : (d.gateFailure ?? 'Chưa đạt Top cơ hội'),
                            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                          ),
                          if (!hasHardGate && d.reasons.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                              d.reasons.take(3).join(' · '),
                              style: TextStyle(
                                fontSize: 11,
                                height: 1.4,
                                color: Theme.of(context).colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                          if (hasHardGate && d.buyScore != null) ...[
                            const SizedBox(height: 8),
                            Text(
                              'Tiềm năng ranking ${d.buyScore!.toStringAsFixed(0)}/100',
                              style: TextStyle(
                                fontSize: 11,
                                color: Theme.of(context).colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                    const SizedBox(width: 12),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: style.pillBg,
                            borderRadius: BorderRadius.circular(999),
                          ),
                          child: Text(
                            trade.label,
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                              color: style.pillColor,
                            ),
                          ),
                        ),
                        if (trade.reason.isNotEmpty) ...[
                          const SizedBox(height: 4),
                          SizedBox(
                            width: 120,
                            child: Text(
                              trade.reason,
                              textAlign: TextAlign.right,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 9,
                                height: 1.3,
                                color: Theme.of(context).colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ),
                        ],
                        const SizedBox(height: 8),
                        Text(
                          displayScore.toStringAsFixed(0),
                          style: dataFont(
                            context,
                            size: 28,
                            weight: FontWeight.w700,
                            color: style.accent,
                          ),
                        ),
                        if (!hasHardGate)
                          PredictedHitPill(
                            percent: d.predictedHitPercent,
                            sampleCount: d.predictedSampleCount,
                          ),
                        Text(
                          scoreSuffix,
                          style: TextStyle(
                            fontSize: 10,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ),
                        if (d.setupDna != null && d.setupDna!.isNotEmpty) ...[
                          const SizedBox(height: 4),
                          SizedBox(
                            width: 120,
                            child: Text(
                              d.setupDna!,
                              textAlign: TextAlign.right,
                              style: TextStyle(
                                fontSize: 9,
                                height: 1.3,
                                color: Theme.of(context).colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
              if (breakdown.isNotEmpty)
                Material(
                  color: style.bg,
                  child: InkWell(
                    onTap: () => setState(() => _showBreakdown = !_showBreakdown),
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      decoration: BoxDecoration(
                        border: Border(
                          top: BorderSide(color: Theme.of(context).colorScheme.outlineVariant),
                        ),
                      ),
                      child: Row(
                        children: [
                          Text(
                            hasHardGate ? 'Điều kiện chưa đạt' : 'Chi tiết điểm cộng',
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: Theme.of(context).colorScheme.onSurfaceVariant,
                            ),
                          ),
                          const Spacer(),
                          Icon(
                            _showBreakdown ? Icons.expand_less : Icons.expand_more,
                            size: 18,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              if (_showBreakdown && breakdown.isNotEmpty)
                Container(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                  decoration: BoxDecoration(
                    color: style.bg,
                    border: Border(
                      top: BorderSide(color: Theme.of(context).colorScheme.outline.withValues(alpha: 0.35)),
                    ),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (!hasHardGate &&
                          d.topExplainLines != null &&
                          d.topExplainLines!.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        ...d.topExplainLines!.map(
                          (line) => Padding(
                            padding: const EdgeInsets.only(bottom: 2),
                            child: Text(
                              '✓ $line',
                              style: TextStyle(
                                fontSize: 10,
                                color: Theme.of(context).colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ),
                        ),
                        const Divider(height: 16),
                      ],
                      ...breakdown.map(
                        (item) => Padding(
                          padding: const EdgeInsets.symmetric(vertical: 6),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      item.label,
                                      style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
                                    ),
                                  ),
                                  Text(
                                    '+${item.points.toStringAsFixed(0)}/${item.maxPoints.toStringAsFixed(0)}',
                                    style: dataFont(context, size: 12, weight: FontWeight.w700),
                                  ),
                                ],
                              ),
                              if (item.detail.isNotEmpty)
                                Padding(
                                  padding: const EdgeInsets.only(top: 2),
                                  child: Text(
                                    item.detail,
                                    style: TextStyle(
                                      fontSize: 10,
                                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
        if (showsEntryPointCardForDecision(d)) ...[
          const SizedBox(height: 12),
          EntryPointCard(entry: d.entryPoint, buyScore: d.buyScore),
        ],
      ],
    );
  }
}

class _MergedInsufficientCard extends StatelessWidget {
  const _MergedInsufficientCard({
    required this.decision,
    required this.showBreakdown,
    required this.onToggleBreakdown,
  });

  final BuyDecision decision;
  final bool showBreakdown;
  final VoidCallback onToggleBreakdown;

  @override
  Widget build(BuildContext context) {
    final d = decision;
    final entry = d.entryPoint;
    final trade = resolveBuyDecisionTradeState(d);
    final style = tradeStateStyle(context, trade.state);
    final breakdown = visibleBreakdown(d);
    final headline = d.gateFailure ?? entry.headline;

    return Container(
      decoration: BoxDecoration(
        color: style.bg,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: style.border),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Chưa đủ điều kiện', style: labelCaps(context)),
                      const SizedBox(height: 4),
                      Text(
                        headline,
                        style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                      ),
                      if (entry.action.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Text(
                          entry.action,
                          style: TextStyle(
                            fontSize: 12,
                            height: 1.4,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                      if (d.buyScore != null) ...[
                        const SizedBox(height: 8),
                        Text(
                          'Tiềm năng ranking ${d.buyScore!.toStringAsFixed(0)}/100',
                          style: TextStyle(
                            fontSize: 11,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: style.pillBg,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        trade.label,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: style.pillColor,
                        ),
                      ),
                    ),
                    if (trade.reason.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      SizedBox(
                        width: 120,
                        child: Text(
                          trade.reason,
                          textAlign: TextAlign.right,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 9,
                            height: 1.3,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 8),
                    Text(
                      (d.actionScore ?? 0).toStringAsFixed(0),
                      style: dataFont(
                        context,
                        size: 28,
                        weight: FontWeight.w700,
                        color: style.accent,
                      ),
                    ),
                    Text(
                      'điểm hành động',
                      style: TextStyle(
                        fontSize: 10,
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          if (breakdown.isNotEmpty)
            Material(
              color: style.bg,
              child: InkWell(
                onTap: onToggleBreakdown,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                  decoration: BoxDecoration(
                    border: Border(
                      top: BorderSide(color: Theme.of(context).colorScheme.outlineVariant),
                    ),
                  ),
                  child: Row(
                    children: [
                      Text(
                        'Điều kiện chưa đạt',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),
                      const Spacer(),
                      Icon(
                        showBreakdown ? Icons.expand_less : Icons.expand_more,
                        size: 18,
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                    ],
                  ),
                ),
              ),
            ),
          if (showBreakdown && breakdown.isNotEmpty)
            Container(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              decoration: BoxDecoration(
                color: style.bg,
                border: Border(
                  top: BorderSide(color: Theme.of(context).colorScheme.outline.withValues(alpha: 0.35)),
                ),
              ),
              child: Column(
                children: breakdown
                    .map(
                      (item) => Padding(
                        padding: const EdgeInsets.symmetric(vertical: 6),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    item.label,
                                    style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
                                  ),
                                ),
                                Text(
                                  '+${item.points.toStringAsFixed(0)}/${item.maxPoints.toStringAsFixed(0)}',
                                  style: dataFont(context, size: 12, weight: FontWeight.w700),
                                ),
                              ],
                            ),
                            if (item.detail.isNotEmpty)
                              Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text(
                                  item.detail,
                                  style: TextStyle(
                                    fontSize: 10,
                                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    )
                    .toList(),
              ),
            ),
          _EntryChecklist(checklist: entry.checklist),
        ],
      ),
    );
  }
}

class EntryPointCard extends StatelessWidget {
  const EntryPointCard({super.key, required this.entry, this.buyScore});

  final EntryPoint entry;
  final double? buyScore;

  @override
  Widget build(BuildContext context) {
    if (entry.headline.isEmpty && entry.action.isEmpty) {
      return const SizedBox.shrink();
    }

    final scheme = Theme.of(context).colorScheme;
    final style = _entryStyle(context, entry.status);
    final typeLabel = _entryTypeLabels[entry.type] ?? '';
    final showConfidence = buyScore == null ||
        (entry.confidence - buyScore!).abs() >= 1;

    return Container(
      decoration: BoxDecoration(
        color: style.bg,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: style.border, width: 2),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Điểm vào', style: labelCaps(context)),
                      const SizedBox(height: 4),
                      Text(
                        entry.headline,
                        style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                      ),
                      if (typeLabel.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                          decoration: BoxDecoration(
                            color: AppColors.positiveDim(context),
                            borderRadius: BorderRadius.circular(999),
                          ),
                          child: Text(
                            typeLabel.toUpperCase(),
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w700,
                              color: scheme.primary,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: style.pillBg,
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        _entryStatusLabels[entry.status] ?? entry.status,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: style.pillColor,
                        ),
                      ),
                    ),
                    if (showConfidence) ...[
                      const SizedBox(height: 8),
                      Text(
                        '${entry.confidence.toStringAsFixed(0)}%',
                        style: dataFont(
                          context,
                          size: 24,
                          weight: FontWeight.w700,
                          color: style.accent,
                        ),
                      ),
                      Text(
                        'checklist đạt',
                        style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                      ),
                    ],
                  ],
                ),
              ],
            ),
          ),
          if (entry.action.isNotEmpty)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Text(
                entry.action,
                style: TextStyle(fontSize: 13, height: 1.4, color: scheme.onSurfaceVariant),
              ),
            ),
          if (showsPriceLevels(entry))
            _PriceGrid(
              cells: [
                _PriceCell('Vào', entry.entryPrice, accent: true),
                _PriceCell('Cắt lỗ', entry.stopLoss, danger: true),
                _PriceCell('Kích hoạt', entry.triggerPrice),
                _PriceCell('Mục tiêu', entry.targetPrice, accent: true),
              ],
            ),
          if (entry.riskRewardRatio > 0 && showsPriceLevels(entry))
            Container(
              padding: const EdgeInsets.symmetric(vertical: 8),
              decoration: BoxDecoration(
                border: Border(top: BorderSide(color: scheme.outlineVariant)),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text('R:R ', style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                  Text(
                    '1 : ${entry.riskRewardRatio.toStringAsFixed(1)}',
                    style: dataFont(context, size: 13, weight: FontWeight.w700),
                  ),
                ],
              ),
            ),
          if (entry.checklist.isNotEmpty) _EntryChecklist(checklist: entry.checklist),
        ],
      ),
    );
  }
}

class _EntryChecklist extends StatelessWidget {
  const _EntryChecklist({required this.checklist});

  final List<EntryPointCheck> checklist;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        border: Border(top: BorderSide(color: scheme.outline.withValues(alpha: 0.35))),
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(14),
          bottomRight: Radius.circular(14),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'CHECKLIST ĐIỂM VÀO',
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.8,
              color: scheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 8),
          ...checklist.map((item) {
            return Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    item.passed ? Icons.check : Icons.close,
                    size: 14,
                    color: item.passed ? scheme.primary : scheme.error,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: RichText(
                      text: TextSpan(
                        style: const TextStyle(fontSize: 11, height: 1.35),
                        children: [
                          TextSpan(
                            text: item.label,
                            style: TextStyle(
                              fontWeight: FontWeight.w600,
                              color: scheme.onSurface,
                            ),
                          ),
                          TextSpan(
                            text: ' — ${item.detail}',
                            style: TextStyle(color: scheme.onSurfaceVariant),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            );
          }),
        ],
      ),
    );
  }
}

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

    final cells = [
      _PriceBoxData('Điểm mua', entry.entryPrice > 0 ? entry.entryPrice : buyZone, accent: true),
      _PriceBoxData('Cắt lỗ', entry.stopLoss > 0 ? entry.stopLoss : stopLoss, danger: true),
      _PriceBoxData('Kích hoạt', entry.triggerPrice > 0 ? entry.triggerPrice : resistance),
      _PriceBoxData('Mục tiêu', entry.targetPrice > 0 ? entry.targetPrice : target, accent: true),
    ].where((c) => c.value > 0).toList();

    if (cells.isEmpty) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle(
          'Các mức giá',
          subtitle: 'Tham chiếu nhanh (20 phiên) — ưu tiên mức trong Điểm vào',
        ),
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

class _PriceCell {
  const _PriceCell(this.label, this.value, {this.danger = false, this.accent = false});

  final String label;
  final double value;
  final bool danger;
  final bool accent;
}

class _PriceGrid extends StatelessWidget {
  const _PriceGrid({required this.cells});

  final List<_PriceCell> cells;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final divider = scheme.outline.withValues(alpha: 0.35);
    final visible = cells.where((c) => c.value > 0).toList();
    if (visible.isEmpty) return const SizedBox.shrink();

    final rows = <List<_PriceCell>>[];
    for (var i = 0; i < visible.length; i += 2) {
      final end = (i + 2 <= visible.length) ? i + 2 : visible.length;
      rows.add(visible.sublist(i, end));
    }

    return Container(
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: divider)),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: rows.asMap().entries.map((entry) {
          final row = entry.value;
          return Container(
            decoration: entry.key > 0
                ? BoxDecoration(border: Border(top: BorderSide(color: divider)))
                : null,
            child: IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(child: _priceCell(context, row[0])),
                  if (row.length > 1) ...[
                    Container(width: 1, color: divider),
                    Expanded(child: _priceCell(context, row[1])),
                  ],
                ],
              ),
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _priceCell(BuildContext context, _PriceCell cell) {
    final scheme = Theme.of(context).colorScheme;
    final color = cell.danger
        ? scheme.error
        : cell.accent
            ? scheme.primary
            : scheme.onSurface;
    return Container(
      color: AppColors.surfaceLow(context),
      padding: const EdgeInsets.symmetric(vertical: 10),
      alignment: Alignment.center,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            cell.label.toUpperCase(),
            style: TextStyle(
              fontSize: 9,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.6,
              color: scheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            formatPrice(cell.value),
            style: dataFont(context, size: 13, weight: FontWeight.w700, color: color),
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

class _EntryStyle {
  const _EntryStyle({
    required this.bg,
    required this.border,
    required this.accent,
    required this.pillBg,
    required this.pillColor,
  });

  final Color bg;
  final Color border;
  final Color accent;
  final Color pillBg;
  final Color pillColor;
}

_EntryStyle _entryStyle(BuildContext context, String status) {
  final scheme = Theme.of(context).colorScheme;
  final isDark = Theme.of(context).brightness == Brightness.dark;
  switch (status) {
    case 'Ready':
      return _EntryStyle(
        bg: AppColors.greenBg(context),
        border: scheme.primary,
        accent: scheme.primary,
        pillBg: scheme.primary,
        pillColor: isDark ? const Color(0xFF002022) : Colors.white,
      );
    case 'Watch':
      final amber = isDark ? AppColors.darkWarning : AppColors.lightWarning;
      return _EntryStyle(
        bg: AppColors.amberBg(context),
        border: amber,
        accent: amber,
        pillBg: AppColors.amberBg(context),
        pillColor: amber,
      );
    case 'Late':
      return _EntryStyle(
        bg: AppColors.redBg(context),
        border: scheme.error,
        accent: scheme.error,
        pillBg: AppColors.redBg(context),
        pillColor: scheme.error,
      );
    default:
      return _EntryStyle(
        bg: AppColors.neutralBg(context),
        border: scheme.outlineVariant,
        accent: scheme.onSurfaceVariant,
        pillBg: AppColors.neutralBg(context),
        pillColor: scheme.onSurfaceVariant,
      );
  }
}
