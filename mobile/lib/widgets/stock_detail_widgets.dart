import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import 'glass_card.dart';

class SwingDecisionCard extends StatelessWidget {
  const SwingDecisionCard({super.key, required this.swing});

  final SwingDecision swing;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final style = _verdictStyle(context, swing.verdict);
    const labels = {'Go': 'Vào lệnh', 'Wait': 'Chờ / theo dõi', 'NoGo': 'Không vào'};

    return Container(
      decoration: BoxDecoration(
        color: style.bg,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: style.border),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Swing decision', style: labelCaps(context)),
                    const SizedBox(height: 4),
                    Text(swing.headline, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                    if (swing.detail.isNotEmpty) ...[
                      const SizedBox(height: 4),
                      Text(swing.detail, style: TextStyle(fontSize: 11, height: 1.4, color: scheme.onSurfaceVariant)),
                    ],
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(color: style.pillBg, borderRadius: BorderRadius.circular(999)),
                child: Text(
                  labels[swing.verdict] ?? swing.verdict,
                  style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: style.pillColor),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _MiniMetric('P điều chỉnh', '${swing.adjustedHitPercent.toStringAsFixed(0)}%')),
              const SizedBox(width: 8),
              Expanded(child: _MiniMetric('Size gợi ý', '${swing.suggestedSizePercent.toStringAsFixed(1)}% NAV')),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(child: _MiniMetric('R:R', swing.riskRewardRatio.toStringAsFixed(1))),
              const SizedBox(width: 8),
              Expanded(child: _MiniMetric('Cal cá nhân', '×${swing.personalCalibrationFactor.toStringAsFixed(2)}')),
            ],
          ),
          if (swing.requiresMasterConfirm)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                'Ưu tiên chờ Mua điểm 1 xác nhận trong phiên',
                style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: AppColors.darkWarning),
              ),
            ),
          if (swing.reasons.isNotEmpty) ...[
            const Divider(height: 20),
            ...swing.reasons.take(4).map(
                  (r) => Padding(
                    padding: const EdgeInsets.only(bottom: 2),
                    child: Text('· $r', style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
                  ),
                ),
          ],
        ],
      ),
    );
  }
}

class TradeJournalCard extends StatefulWidget {
  const TradeJournalCard({super.key, required this.symbol, required this.buyDecision});

  final String symbol;
  final BuyDecision buyDecision;

  @override
  State<TradeJournalCard> createState() => _TradeJournalCardState();
}

class _TradeJournalCardState extends State<TradeJournalCard> {
  var _busy = false;
  String? _msg;

  Future<void> _log(String action) async {
    final auth = context.read<AuthService>();
    if (!auth.isLoggedIn) {
      setState(() => _msg = 'Đăng nhập để ghi journal.');
      return;
    }
    setState(() {
      _busy = true;
      _msg = null;
    });
    final swing = widget.buyDecision.swingDecision;
    try {
      await context.read<ApiClient>().addTradeJournalEntry(
            symbol: widget.symbol,
            action: action,
            sizePercent: swing?.suggestedSizePercent,
            engineVerdict: swing?.verdict,
            buyScore: widget.buyDecision.buyScore,
            predictedHit: swing?.adjustedHitPercent ?? widget.buyDecision.predictedHitPercent,
            setupDna: widget.buyDecision.setupDna,
          );
      if (!mounted) return;
      setState(() {
        _msg = action == 'Entered'
            ? 'Đã ghi nhận vào lệnh — engine học thêm.'
            : 'Đã ghi nhận — engine điều chỉnh cal cá nhân.';
      });
    } on ApiException catch (_) {
      if (mounted) setState(() => _msg = 'Không lưu được journal.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Trade journal', subtitle: 'Ghi nhận để engine học style swing của bạn'),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            FilledButton(
              onPressed: _busy ? null : () => _log('Entered'),
              style: FilledButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                minimumSize: Size.zero,
                textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              child: const Text('Đã vào lệnh'),
            ),
            OutlinedButton(
              onPressed: _busy ? null : () => _log('Skipped'),
              style: OutlinedButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                minimumSize: Size.zero,
                foregroundColor: scheme.onSurface,
                textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              child: const Text('Bỏ qua setup'),
            ),
            OutlinedButton(
              onPressed: _busy ? null : () => _log('Vetoed'),
              style: OutlinedButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                minimumSize: Size.zero,
                foregroundColor: scheme.onSurfaceVariant,
                textStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              child: const Text('Veto (không tin engine)'),
            ),
          ],
        ),
        if (_msg != null) ...[
          const SizedBox(height: 8),
          Text(_msg!, style: TextStyle(fontSize: 12, color: scheme.primary)),
        ],
      ],
    );
  }
}

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

class _MiniMetric extends StatelessWidget {
  const _MiniMetric(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: TextStyle(fontSize: 10, color: Theme.of(context).colorScheme.onSurfaceVariant)),
          Text(value, style: dataFont(context, size: 12, weight: FontWeight.w700)),
        ],
      ),
    );
  }
}

class _VerdictStyle {
  const _VerdictStyle({required this.bg, required this.border, required this.pillBg, required this.pillColor});
  final Color bg;
  final Color border;
  final Color pillBg;
  final Color pillColor;
}

_VerdictStyle _verdictStyle(BuildContext context, String verdict) {
  final scheme = Theme.of(context).colorScheme;
  final isDark = Theme.of(context).brightness == Brightness.dark;
  switch (verdict) {
    case 'Go':
      return _VerdictStyle(
        bg: AppColors.greenBg(context),
        border: scheme.primary,
        pillBg: scheme.primary,
        pillColor: isDark ? const Color(0xFF002022) : Colors.white,
      );
    case 'Wait':
      final amber = isDark ? AppColors.darkWarning : AppColors.lightWarning;
      return _VerdictStyle(
        bg: AppColors.amberBg(context),
        border: amber,
        pillBg: AppColors.amberBg(context),
        pillColor: amber,
      );
    default:
      return _VerdictStyle(
        bg: AppColors.neutralBg(context),
        border: scheme.outlineVariant,
        pillBg: AppColors.neutralBg(context),
        pillColor: scheme.onSurfaceVariant,
      );
  }
}
