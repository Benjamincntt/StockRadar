import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';
import '../widgets/chart_widgets.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class AlertHistoryScreen extends StatefulWidget {
  const AlertHistoryScreen({super.key});

  @override
  State<AlertHistoryScreen> createState() => _AlertHistoryScreenState();
}

class _AlertHistoryScreenState extends State<AlertHistoryScreen> {
  ApiClient get _api => context.read<ApiClient>();
  AlertHistoryResponse? _data;
  AlertHistoryTrendsResponse? _trends;
  var _period = 'week';
  String? _selectedPeriodStart;
  var _loading = true;
  String? _error;

  static const _periodOptions = ['Tuần', 'Tháng', 'Quý'];
  static const _periodApi = {'Tuần': 'week', 'Tháng': 'month', 'Quý': 'quarter'};

  @override
  void initState() {
    super.initState();
    _load();
  }

  String get _periodLabel =>
      _periodOptions.firstWhere((l) => _periodApi[l] == _period, orElse: () => 'Tuần');

  Future<void> _load({String? periodStart}) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final trends = await _api.getAlertHistoryTrends(
        period: _period,
        kind: 'buy',
        limit: 12,
        selectedPeriodStart: periodStart ?? _selectedPeriodStart,
      );
      final bucket = trends.selectedBucket;
      final start = bucket?.periodStart;
      final end = bucket?.periodEnd;

      final data = await _api.getAlertHistory(
        limit: 100,
        kind: 'buy',
        from: start,
        to: end,
      );

      if (!mounted) return;
      setState(() {
        _trends = trends;
        _data = data;
        _selectedPeriodStart = bucket?.periodStart;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'Không tải được lịch sử lệnh.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _onPeriodChanged(String label) {
    final api = _periodApi[label];
    if (api == null || api == _period) return;
    setState(() {
      _period = api;
      _selectedPeriodStart = null;
    });
    _load();
  }

  void _onBucketSelected(AlertHistoryTrendBucket bucket) {
    if (bucket.periodStart == _selectedPeriodStart) return;
    _load(periodStart: bucket.periodStart);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final data = _data;
    final trends = _trends;
    final bucket = trends?.selectedBucket;

    return RefreshIndicator(
      onRefresh: () => _load(),
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        children: [
          const PageHeader(
            title: 'Lịch sử Mua điểm',
            subtitle: 'Win / Lose sau T+2.5 · Điểm mua 1 & 2',
          ),
          const SizedBox(height: 12),
          FilterChips(
            options: _periodOptions,
            selected: _periodLabel,
            onSelected: _onPeriodChanged,
          ),
          const SizedBox(height: 12),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 32),
              child: Center(child: CircularProgressIndicator()),
            )
          else ...[
            if (_error != null) ...[
              ErrorBanner(message: _error!, onRetry: () => _load()),
              const SizedBox(height: 12),
            ],
            if (bucket != null && trends != null) ...[
              _SuccessRateHeader(bucket: bucket),
              const SizedBox(height: 12),
              _TrendBucketsRow(
                buckets: trends.buckets,
                selectedPeriodStart: _selectedPeriodStart,
                onSelected: _onBucketSelected,
              ),
              const SizedBox(height: 12),
            ],
            if (data != null) ...[
              if (bucket != null)
                Text(
                  'Chi tiết · ${bucket.periodLabel}',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: scheme.onSurface,
                  ),
                ),
              if (bucket != null) const SizedBox(height: 8),
              if (data.alerts.isEmpty)
                GlassCard(
                  child: Text(
                    'Không có lệnh Mua điểm trong kỳ này.',
                    style: TextStyle(color: scheme.onSurfaceVariant),
                  ),
                )
              else
                ...data.alerts.map(
                  (a) => Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: _AlertHistoryTile(item: a),
                  ),
                ),
            ],
          ],
        ],
      ),
    );
  }
}

class _SuccessRateHeader extends StatelessWidget {
  const _SuccessRateHeader({required this.bucket});

  final AlertHistoryTrendBucket bucket;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final decided = bucket.decidedCount;
    final rateText = decided == 0 ? '—' : '${bucket.winRatePercent.toStringAsFixed(1)}%';
    final delta = bucket.deltaWinRatePercent;

    final gradientColors = isDark
        ? [
            scheme.primary.withValues(alpha: 0.35),
            scheme.secondary.withValues(alpha: 0.22),
            AppColors.darkSurfaceHigh.withValues(alpha: 0.9),
          ]
        : [
            scheme.primary.withValues(alpha: 0.18),
            const Color(0xFFDCE9FF),
            AppColors.lightSurface,
          ];

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: gradientColors,
        ),
        border: Border.all(
          color: isDark
              ? scheme.primary.withValues(alpha: 0.45)
              : scheme.primary.withValues(alpha: 0.25),
        ),
        boxShadow: isDark
            ? [
                BoxShadow(
                  color: scheme.primary.withValues(alpha: 0.12),
                  blurRadius: 24,
                  offset: const Offset(0, 8),
                ),
              ]
            : [
                BoxShadow(
                  color: scheme.primary.withValues(alpha: 0.08),
                  blurRadius: 20,
                  offset: const Offset(0, 6),
                ),
              ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  bucket.periodLabel,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: scheme.onSurfaceVariant,
                  ),
                ),
              ),
              if (bucket.isCurrentPeriod)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: scheme.primary.withValues(alpha: 0.15),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    'Kỳ hiện tại',
                    style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: scheme.primary),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 6),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                rateText,
                style: dataFont(
                  context,
                  size: 40,
                  weight: FontWeight.w800,
                  color: scheme.primary,
                ),
              ),
              if (delta != null) ...[
                const SizedBox(width: 10),
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Text(
                    '${delta >= 0 ? '▲' : '▼'} ${delta.abs().toStringAsFixed(1)}pp',
                    style: dataFont(
                      context,
                      size: 14,
                      weight: FontWeight.w700,
                      color: delta >= 0 ? scheme.primary : scheme.error,
                    ),
                  ),
                ),
              ],
            ],
          ),
          const SizedBox(height: 4),
          Text(
            bucket.isSmallSample
                ? 'Mẫu nhỏ (<3 Win+Lose) — thận trọng khi đọc %'
                : 'Win / (Win + Lose) · Flat không tính',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              _StatChip(label: 'Win', value: '${bucket.winCount}', positive: true),
              _StatChip(label: 'Lose', value: '${bucket.loseCount}', negative: true),
              _StatChip(label: 'Flat', value: '${bucket.flatCount}'),
              _StatChip(label: 'Chờ đo', value: '${bucket.pendingCount}'),
            ],
          ),
        ],
      ),
    );
  }
}

class _TrendBucketsRow extends StatelessWidget {
  const _TrendBucketsRow({
    required this.buckets,
    required this.selectedPeriodStart,
    required this.onSelected,
  });

  final List<AlertHistoryTrendBucket> buckets;
  final String? selectedPeriodStart;
  final ValueChanged<AlertHistoryTrendBucket> onSelected;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (buckets.isEmpty) return const SizedBox.shrink();

    final winRates = buckets
        .where((b) => b.decidedCount > 0)
        .map((b) => b.winRatePercent)
        .toList();

    return GlassCard(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Xu hướng',
                  style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: scheme.onSurface),
                ),
              ),
              if (winRates.length >= 2)
                SparklineMini(
                  closes: winRates,
                  fallbackChange: winRates.last,
                  width: 72,
                  height: 28,
                ),
            ],
          ),
          const SizedBox(height: 10),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: buckets.map((b) {
                final selected = b.periodStart == selectedPeriodStart;
                final hasRate = b.decidedCount > 0;
                final barH = hasRate ? (b.winRatePercent.clamp(0, 100) / 100 * 48 + 8) : 8.0;
                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: InkWell(
                    onTap: () => onSelected(b),
                    borderRadius: BorderRadius.circular(8),
                    child: Container(
                      width: 44,
                      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
                      decoration: BoxDecoration(
                        color: selected
                            ? scheme.primary.withValues(alpha: 0.12)
                            : scheme.surfaceContainerHighest.withValues(alpha: 0.5),
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: selected ? scheme.primary : Colors.transparent,
                        ),
                      ),
                      child: Column(
                        children: [
                          Align(
                            alignment: Alignment.bottomCenter,
                            child: Container(
                              width: 16,
                              height: barH,
                              decoration: BoxDecoration(
                                color: hasRate
                                    ? (b.winRatePercent >= 50
                                        ? scheme.primary.withValues(alpha: 0.85)
                                        : scheme.error.withValues(alpha: 0.85))
                                    : scheme.outline.withValues(alpha: 0.35),
                                borderRadius: BorderRadius.circular(4),
                              ),
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            hasRate ? '${b.winRatePercent.toStringAsFixed(0)}%' : '—',
                            style: dataFont(context, size: 9, weight: FontWeight.w700),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }
}

class _StatChip extends StatelessWidget {
  const _StatChip({
    required this.label,
    required this.value,
    this.positive = false,
    this.negative = false,
  });

  final String label;
  final String value;
  final bool positive;
  final bool negative;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = positive
        ? scheme.primary
        : negative
            ? scheme.error
            : scheme.onSurfaceVariant;
    final bg = positive
        ? AppColors.positiveDim(context)
        : negative
            ? AppColors.negativeDim(context)
            : scheme.surface.withValues(alpha: 0.55);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(label, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
          const SizedBox(width: 6),
          Text(
            value,
            style: dataFont(context, size: 13, weight: FontWeight.w700, color: color),
          ),
        ],
      ),
    );
  }
}

class _AlertHistoryTile extends StatelessWidget {
  const _AlertHistoryTile({required this.item});

  final AlertHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final style = _outcomeStyle(context);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: style.bg,
        borderRadius: BorderRadius.circular(12),
        border: Border(
          left: BorderSide(color: style.border, width: 3),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  item.symbol,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
              ),
              const SizedBox(width: 8),
              _OutcomePill(label: style.label, accent: style.accent),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            _buyPointLabel(item.alertType),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: scheme.onSurface,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            'Phiên ${formatApiDateVietnam(item.entryDate)} : giá ${formatPrice(item.entryPrice)}',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
          ),
          if (item.isPending) ...[
            const SizedBox(height: 4),
            Text(
              'Phiên T+2.5 : chờ đo',
              style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
            ),
          ] else if (item.isMeasured) ...[
            const SizedBox(height: 4),
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Expanded(
                  child: Text(
                    'Phiên T+2.5 : giá ${formatPrice(_forwardPrice(item))}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                  ),
                ),
                if (item.forwardReturnPercent != null) ...[
                  const SizedBox(width: 8),
                  ChangePill(item.forwardReturnPercent!),
                ],
              ],
            ),
          ],
        ],
      ),
    );
  }

  _OutcomeStyle _outcomeStyle(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (item.isPending) {
      return _OutcomeStyle(
        label: 'Chờ',
        accent: scheme.onSurfaceVariant,
        border: scheme.outline.withValues(alpha: 0.5),
        bg: AppColors.neutralBg(context),
      );
    }
    if (item.isSuccess == true) {
      return _OutcomeStyle(
        label: 'Win',
        accent: scheme.primary,
        border: scheme.primary,
        bg: AppColors.positiveDim(context),
      );
    }
    if (item.isSuccess == false) {
      return _OutcomeStyle(
        label: 'Lose',
        accent: scheme.error,
        border: scheme.error,
        bg: AppColors.negativeDim(context),
      );
    }
    return _OutcomeStyle(
      label: 'Flat',
      accent: scheme.onSurfaceVariant,
      border: scheme.onSurfaceVariant.withValues(alpha: 0.5),
      bg: AppColors.neutralBg(context),
    );
  }

  static String _buyPointLabel(String alertType) => switch (alertType) {
        'BuyPoint1' => 'Điểm mua 1',
        'BuyPoint2' => 'Điểm mua 2',
        _ => 'Điểm mua',
      };

  static double _forwardPrice(AlertHistoryItem item) {
    if (item.forwardPriceT25 != null && item.forwardPriceT25! > 0) {
      return item.forwardPriceT25!;
    }
    final ret = item.forwardReturnPercent;
    if (ret == null || item.entryPrice <= 0) return 0;
    return item.entryPrice * (1 + ret / 100);
  }
}

class _OutcomePill extends StatelessWidget {
  const _OutcomePill({required this.label, required this.accent});

  final String label;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final isWin = label == 'Win';
    final isLose = label == 'Lose';
    final bg = isWin
        ? AppColors.positiveDim(context)
        : isLose
            ? AppColors.negativeDim(context)
            : Theme.of(context).colorScheme.surfaceContainerHighest;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w800,
          color: accent,
        ),
      ),
    );
  }
}

class _OutcomeStyle {
  const _OutcomeStyle({
    required this.label,
    required this.accent,
    required this.border,
    required this.bg,
  });

  final String label;
  final Color accent;
  final Color border;
  final Color bg;
}
