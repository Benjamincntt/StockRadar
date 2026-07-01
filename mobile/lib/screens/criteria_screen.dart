import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

const _indicatorMaxRank = 10;
const _bundleMaxRank = 16;

const _bundleComponents = <String, String>{
  'BundleBeginner': 'EMA + RSI + Volume',
  'BundleIntermediate': 'EMA + Volume + ATR',
  'BundleAdvanced': 'VWAP + EMA + Volume + ATR',
  'BundleProfessional': 'Wyckoff + VSA',
  'BundleInstitutional': 'Volume Profile + VWAP + Delta',
  'BundleSmartMoneyConcept': 'SMC + Volume + VWAP',
};

class CriteriaScreen extends StatefulWidget {
  const CriteriaScreen({super.key});

  @override
  State<CriteriaScreen> createState() => _CriteriaScreenState();
}

class _CriteriaScreenState extends State<CriteriaScreen> {
  ApiClient get _api => context.read<ApiClient>();
  CriteriaSummary? _summary;
  var _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final summary = await _api.getCriteriaSummary();
      setState(() => _summary = summary);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  List<CriterionAccuracy> _sorted(List<CriterionAccuracy> items) {
    final copy = [...items];
    copy.sort((a, b) => b.displayPercent.compareTo(a.displayPercent));
    return copy;
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const LoadingView();
    final summary = _summary;
    final criteria = summary?.criteria ?? [];
    final scheme = Theme.of(context).colorScheme;

    final indicators = _sorted(criteria.where((c) => c.rank <= _indicatorMaxRank).toList());
    final bundles = _sorted(
      criteria.where((c) => c.rank > _indicatorMaxRank && c.rank <= _bundleMaxRank).toList(),
    );
    final smartMoney = _sorted(criteria.where((c) => c.group == 'Top cơ hội').toList());
    final removeCandidates = (summary?.weeklyReview ?? [])
        .where((w) => w.recommendedAction == 'Remove' && w.totalCount7d >= 30)
        .toList()
      ..sort((a, b) => a.displayReliability7d.compareTo(b.displayReliability7d));
    if (removeCandidates.length > 5) removeCandidates.removeRange(5, removeCandidates.length);

    final groups = <CriterionGroupAccuracy>[...(summary?.groups ?? [])]
      ..sort((a, b) => b.displayPercent.compareTo(a.displayPercent));

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        children: [
          TextButton.icon(
            onPressed: () => context.go('/'),
            icon: const Icon(Icons.chevron_left, size: 18),
            label: const Text('Trang chủ'),
            style: TextButton.styleFrom(foregroundColor: scheme.onSurfaceVariant, padding: EdgeInsets.zero),
          ),
          const SizedBox(height: 8),
          GlassCard(
            padding: const EdgeInsets.all(20),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: AppColors.positiveDim(context),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Icon(Icons.trending_up, color: scheme.primary, size: 22),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Phân tích chỉ báo',
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              fontWeight: FontWeight.w700,
                              fontSize: 18,
                            ),
                      ),
                      if (summary?.statusMessage != null) ...[
                        const SizedBox(height: 4),
                        Text(summary!.statusMessage!, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                      ],
                      if (summary?.weekStartDate != null) ...[
                        const SizedBox(height: 4),
                        Text(
                          'Tuần review: ${_formatDate(summary!.weekStartDate!)}'
                          '${summary.asOfDate != null ? ' · T-1 ${_formatDate(summary.asOfDate!)}' : ''}',
                          style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: scheme.primary),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          if (criteria.isEmpty)
            const GlassCard(
              child: Text('Chưa có dữ liệu. Chạy Job 2 + phân tích sau phiên giao dịch để lưu điểm thực tế vào DB.'),
            )
          else ...[
            if (removeCandidates.isNotEmpty) _removeCandidatesCard(removeCandidates),
            if (groups.isNotEmpty) _groupsCard(groups),
            _criterionGroup(
              title: 'Top 10 chỉ báo đơn',
              subtitle: 'Sắp xếp theo reliability / độ khớp giảm dần',
              items: indicators,
              showRank: true,
            ),
            _criterionGroup(
              title: 'Bộ chỉ báo kết hợp',
              subtitle: 'Sắp xếp theo reliability / độ khớp giảm dần',
              items: bundles,
              showRank: true,
            ),
            _criterionGroup(
              title: 'Top cơ hội — SmartMoney',
              subtitle: 'Sắp xếp theo reliability / độ khớp giảm dần',
              items: smartMoney,
            ),
            if ((summary?.topStocks ?? []).isNotEmpty) _topStocksCard(summary!.topStocks),
          ],
        ],
      ),
    );
  }

  Widget _removeCandidatesCard(List<WeeklyCriterionReview> items) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: GlassCard(
        wave: true,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionTitle(
              'Cần xem lại (${items.length})',
              subtitle: '7 ngày gần nhất · R <42% và edge <3%',
            ),
            const SizedBox(height: 12),
            ...items.map((w) {
              final r = w.displayReliability7d;
              final edge = w.edge7d ?? 0;
              final showEdge = edge.abs() > 0.05;
              return Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: SurfaceRow(
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(w.label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                            Text(w.group, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
                          ],
                        ),
                      ),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text('R ${r.toStringAsFixed(0)}%', style: TextStyle(fontWeight: FontWeight.w700, color: scheme.error)),
                          Text(
                            '${showEdge ? 'edge ${edge >= 0 ? '+' : ''}${edge.toStringAsFixed(1)}% · ' : ''}${w.hitCount7d}/${w.totalCount7d}',
                            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              );
            }),
          ],
        ),
      ),
    );
  }

  Widget _groupsCard(List<CriterionGroupAccuracy> groups) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: GlassCard(
        wave: true,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionTitle(
              'Độ tin cậy theo nhóm',
              subtitle: 'Setup trend · reliability + edge · Keep / Watch / Remove',
            ),
            const SizedBox(height: 12),
            ...groups.map((g) {
              final percent = g.displayPercent;
              final showEdge = _showEdge(g.edgePercent, g.accuracyPercent, baseline: null);
              final showR = _showReliabilityR(g.reliabilityScore, g.accuracyPercent);
              final showCounts = g.keepCount + g.watchCount + g.removeCount > 0;
              return Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: SurfaceRow(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(child: Text(g.groupId, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13))),
                          _actionBadge(g.recommendedAction),
                          const SizedBox(width: 8),
                          Text(
                            '${percent.toStringAsFixed(1)}%',
                            style: dataFont(context, weight: FontWeight.w700, color: _scoreColor(percent)),
                          ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Khớp ${g.hitCount}/${g.totalCount} · Điểm TB ${g.avgScore.toStringAsFixed(0)}'
                        '${showEdge ? ' · Edge +${g.edgePercent!.toStringAsFixed(1)}%' : ''}'
                        '${showR ? ' · R ${g.reliabilityScore!.toStringAsFixed(0)}' : ''}'
                        '${showCounts ? ' · ${g.keepCount} giữ · ${g.watchCount} theo dõi · ${g.removeCount} loại' : ''}',
                        style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
              );
            }),
          ],
        ),
      ),
    );
  }

  Widget _topStocksCard(List<CriterionStockRank> stocks) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: GlassCard(
        wave: true,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SectionTitle('CP điểm tổng cao (T-1)', subtitle: 'Lưu trong StockCriterionScores'),
            const SizedBox(height: 12),
            ...stocks.map((s) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: SurfaceRow(
                    onTap: () => context.push('/stocks/${s.symbol}'),
                    child: Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(s.symbol, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
                              if (s.topCriteria.isNotEmpty)
                                Text(
                                  s.topCriteria.map((c) => '${c.label} ${c.score.toStringAsFixed(0)}').join(' · '),
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                                ),
                            ],
                          ),
                        ),
                        ScorePill(s.compositeScore),
                      ],
                    ),
                  ),
                )),
          ],
        ),
      ),
    );
  }

  Widget _criterionGroup({
    required String title,
    required String subtitle,
    required List<CriterionAccuracy> items,
    bool showRank = false,
  }) {
    if (items.isEmpty) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: GlassCard(
        wave: true,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SectionTitle(title, subtitle: subtitle),
            const SizedBox(height: 12),
            ...items.asMap().entries.map((entry) {
              final index = entry.key;
              final c = entry.value;
              final percent = c.displayPercent;
              final subLabel = c.group == 'Bộ chỉ báo' ? (_bundleComponents[c.id] ?? c.group) : c.group;
              final showEdge = _showOptional(c.edgePercent);
              final showMfe = _showOptional(c.avgMfePercent);
              final showRisk = _showOptional(c.invalidationRatePercent);
              final showBaseline = _showOptional(c.baselinePercent);

              return Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Opacity(
                  opacity: c.isActive ? 1 : 0.55,
                  child: SurfaceRow(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (showRank)
                              Container(
                                width: 24,
                                height: 24,
                                alignment: Alignment.center,
                                decoration: BoxDecoration(
                                  color: AppColors.positiveDim(context),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: Text(
                                  '${index + 1}',
                                  style: dataFont(context, size: 11, weight: FontWeight.w700, color: scheme.primary),
                                ),
                              ),
                            if (showRank) const SizedBox(width: 8),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    children: [
                                      Expanded(
                                        child: Text(c.label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                                      ),
                                      _actionBadge(c.recommendedAction),
                                      const SizedBox(width: 6),
                                      Text(
                                        '${percent.toStringAsFixed(1)}%',
                                        style: dataFont(context, weight: FontWeight.w700, color: _scoreColor(percent)),
                                      ),
                                    ],
                                  ),
                                  if (subLabel.isNotEmpty)
                                    Text(subLabel, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Khớp ${c.hitCount}/${c.totalCount} · Điểm TB ${c.avgScore.toStringAsFixed(0)}'
                          '${showEdge ? ' · Edge +${c.edgePercent!.toStringAsFixed(1)}%' : ''}'
                          '${showMfe ? ' · MFE ${c.avgMfePercent!.toStringAsFixed(1)}%' : ''}'
                          '${showRisk ? ' · Rũi ro ${c.invalidationRatePercent!.toStringAsFixed(0)}%' : ''}',
                          style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                        ),
                        Text(
                          'W ${c.weight.toStringAsFixed(2)}× · 7d ${c.accuracy7d.toStringAsFixed(1)}%'
                          '${showBaseline ? ' · baseline ${c.baselinePercent!.toStringAsFixed(1)}%' : ''}',
                          style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                        ),
                        if (c.buckets.isNotEmpty)
                          Text(
                            'Bucket: ${c.buckets.map((b) => '${b.bucketId} ${b.accuracyPercent.toStringAsFixed(0)}%').join(' · ')}',
                            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                          ),
                        if (c.phases.isNotEmpty)
                          Text(
                            'Pha TT: ${c.phases.map((p) => '${_phaseLabel(p.phase)} ${p.accuracyPercent.toStringAsFixed(0)}%').join(' · ')}',
                            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                          ),
                        const SizedBox(height: 6),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(999),
                          child: LinearProgressIndicator(
                            value: (percent / 100).clamp(0.0, 1.0),
                            minHeight: 6,
                            backgroundColor: AppColors.surfaceHigh(context),
                            color: _scoreColor(percent),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            }),
          ],
        ),
      ),
    );
  }

  Widget _actionBadge(String action) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    late final Color bg;
    late final Color fg;
    late final String label;
    switch (action) {
      case 'Watch':
        bg = AppColors.amberBg(context);
        fg = isDark ? AppColors.darkWarning : AppColors.lightWarning;
        label = 'Theo dõi';
      case 'Remove':
        bg = AppColors.redBg(context);
        fg = scheme.error;
        label = 'Loại';
      default:
        bg = AppColors.greenBg(context);
        fg = scheme.primary;
        label = 'Giữ';
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: TextStyle(fontSize: 10, fontWeight: FontWeight.w600, color: fg)),
    );
  }

  Color _scoreColor(double percent) {
    final scheme = Theme.of(context).colorScheme;
    if (percent >= 55) return scheme.primary;
    if (percent >= 45) return scheme.onSurfaceVariant;
    return scheme.error;
  }

  bool _showOptional(double? value) => value != null && value.abs() > 0.05;

  bool _showEdge(double? edge, double accuracy, {double? baseline}) {
    if (edge == null || edge.abs() <= 0.05) return false;
    if ((baseline ?? 0) <= 0 && (edge - accuracy).abs() <= 0.5) return false;
    return true;
  }

  bool _showReliabilityR(double? reliability, double accuracy) {
    if (reliability == null || reliability <= 0) return false;
    return (reliability - accuracy).abs() > 0.5;
  }

  String _formatDate(String iso) {
    final parts = iso.split('-');
    if (parts.length != 3) return iso;
    return '${parts[2]}/${parts[1]}/${parts[0]}';
  }

  String _phaseLabel(String phase) => switch (phase) {
        'Favorable' => 'Thuận',
        'Neutral' => 'TB',
        'Unfavorable' => 'Xấu',
        _ => phase,
      };
}
