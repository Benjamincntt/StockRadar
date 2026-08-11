import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';
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
  var _loading = true;
  var _loadingTrends = false;
  String? _error;

  /// `t25` = Win/Lose sau T+2.5 (mặc định, chất lượng setup) · `realized` = lợi nhuận thực (giá bán thật/gần đúng).
  var _metric = 't25';

  static const _periodOptions = ['Tuần', 'Tháng', 'Quý'];
  static const _periodApi = {'Tuần': 'week', 'Tháng': 'month', 'Quý': 'quarter'};
  static const _metricOptions = ['T+2.5', 'Lợi nhuận thực'];

  @override
  void initState() {
    super.initState();
    _load();
  }

  String get _periodLabel =>
      _periodOptions.firstWhere((l) => _periodApi[l] == _period, orElse: () => 'Tuần');

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _api.getAlertHistory(limit: 100, kind: 'buy'),
        _api.getAlertHistoryTrends(period: _period, kind: 'buy', limit: 12),
      ]);
      if (!mounted) return;
      setState(() {
        _data = results[0] as AlertHistoryResponse;
        _trends = results[1] as AlertHistoryTrendsResponse;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'Không tải được lịch sử lệnh.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _loadTrends(String period) async {
    setState(() => _loadingTrends = true);
    try {
      final trends = await _api.getAlertHistoryTrends(period: period, kind: 'buy', limit: 12);
      if (!mounted) return;
      setState(() => _trends = trends);
    } catch (_) {
      // Giữ chart cũ nếu lỗi tạm thời.
    } finally {
      if (mounted) setState(() => _loadingTrends = false);
    }
  }

  void _onPeriodChanged(String label) {
    final api = _periodApi[label];
    if (api == null || api == _period) return;
    setState(() => _period = api);
    _loadTrends(api);
  }

  bool get _isRealized => _metric == 'realized';

  void _onMetricChanged(String label) {
    final metric = label == 'Lợi nhuận thực' ? 'realized' : 't25';
    if (metric == _metric) return;
    setState(() => _metric = metric);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final data = _data;
    final trends = _trends;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        children: [
          PageHeader(
            title: 'Lịch sử Mua điểm',
            subtitle: _isRealized
                ? 'Lợi nhuận thực (giá bán thật/gần đúng) · Giá vào 1 & 2'
                : 'Win / Lose sau T+2.5 · Giá vào 1 & 2',
          ),
          const SizedBox(height: 12),
          FilterChips(
            options: _metricOptions,
            selected: _isRealized ? 'Lợi nhuận thực' : 'T+2.5',
            onSelected: _onMetricChanged,
          ),
          const SizedBox(height: 12),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 32),
              child: Center(child: CircularProgressIndicator()),
            )
          else ...[
            if (_error != null) ...[
              ErrorBanner(message: _error!, onRetry: _load),
              const SizedBox(height: 12),
            ],
            if (data != null) ...[
              _SuccessRateHeader(data: data, realized: _isRealized),
              const SizedBox(height: 12),
            ],
            _WinRateTrendChart(
              periodLabel: _periodLabel,
              periodOptions: _periodOptions,
              onPeriodChanged: _onPeriodChanged,
              buckets: trends?.buckets ?? const [],
              loading: _loadingTrends,
              realized: _isRealized,
            ),
            const SizedBox(height: 16),
            if (data != null) ...[
              Text(
                'Lịch sử lệnh',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                  color: scheme.onSurface,
                ),
              ),
              const SizedBox(height: 8),
              if (data.alerts.isEmpty)
                GlassCard(
                  child: Text(
                    'Chưa có lệnh Mua điểm được theo dõi.',
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
  const _SuccessRateHeader({required this.data, this.realized = false});

  final AlertHistoryResponse data;
  final bool realized;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final decided = realized
        ? data.realizedWinCount + data.realizedLoseCount
        : data.totalSuccess + data.totalFailed;
    final ratePercent = realized ? data.realizedWinRatePercent : data.overallSuccessRatePercent;
    final rateText = decided == 0 ? '—' : '${ratePercent.toStringAsFixed(1)}%';

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
          Text(
            realized ? 'Tỷ lệ Win thực' : 'Tỷ lệ Win tổng',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: scheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            rateText,
            style: dataFont(
              context,
              size: 40,
              weight: FontWeight.w800,
              color: scheme.primary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            decided == 0
                ? 'Chưa có lệnh Win/Lose rõ ràng'
                : realized
                    ? 'Giá bán nửa/bán hết thật (hoặc gần đúng T+2.5) · trừ phí + thuế · Win ≥0%'
                    : 'Win = lãi ≥1% (sau thuế phí) · Flat 0…<1% không tính vào %',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
          if (realized && data.avgRealizedReturnPercent != null) ...[
            const SizedBox(height: 4),
            Text(
              'Avg %/lệnh: ${data.avgRealizedReturnPercent! >= 0 ? '+' : ''}'
              '${data.avgRealizedReturnPercent!.toStringAsFixed(2)}%',
              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
            ),
          ],
          const SizedBox(height: 14),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: realized
                ? [
                    _StatChip(label: 'Win', value: '${data.realizedWinCount}', positive: true),
                    _StatChip(label: 'Lose', value: '${data.realizedLoseCount}', negative: true),
                    _StatChip(label: 'Flat', value: '${data.realizedFlatCount}'),
                    _StatChip(label: 'Đang mở', value: '${data.totalOpenTrades}'),
                  ]
                : [
                    _StatChip(label: 'Win', value: '${data.totalSuccess}', positive: true),
                    _StatChip(label: 'Lose', value: '${data.totalFailed}', negative: true),
                    _StatChip(label: 'Flat', value: '${data.totalFlat}'),
                    _StatChip(label: 'Chờ đo', value: '${data.totalPending}'),
                  ],
          ),
        ],
      ),
    );
  }
}

class _WinRateTrendChart extends StatelessWidget {
  const _WinRateTrendChart({
    required this.periodLabel,
    required this.periodOptions,
    required this.onPeriodChanged,
    required this.buckets,
    required this.loading,
    this.realized = false,
  });

  final String periodLabel;
  final List<String> periodOptions;
  final ValueChanged<String> onPeriodChanged;
  final List<AlertHistoryTrendBucket> buckets;
  final bool loading;
  final bool realized;

  String _shortLabel(String label) {
    if (label.length <= 6) return label;
    return label.substring(label.length - 5);
  }

  double _rate(AlertHistoryTrendBucket b) => realized ? b.realizedWinRatePercent : b.winRatePercent;
  int _win(AlertHistoryTrendBucket b) => realized ? b.realizedWinCount : b.winCount;
  int _lose(AlertHistoryTrendBucket b) => realized ? b.realizedLoseCount : b.loseCount;
  int _decided(AlertHistoryTrendBucket b) => realized ? b.realizedClosedCount : b.decidedCount;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    AlertHistoryTrendBucket? current;
    for (final b in buckets) {
      if (b.isCurrentPeriod) {
        current = b;
        break;
      }
    }
    current ??= buckets.isNotEmpty ? buckets.last : null;

    return GlassCard(
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Biểu đồ hiệu quả',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: scheme.onSurface,
                  ),
                ),
              ),
              if (loading)
                SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: scheme.primary.withValues(alpha: 0.7),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          FilterChips(
            options: periodOptions,
            selected: periodLabel,
            onSelected: onPeriodChanged,
          ),
          const SizedBox(height: 12),
          if (buckets.isEmpty)
            SizedBox(
              height: 160,
              child: Center(
                child: Text(
                  'Chưa đủ dữ liệu để vẽ biểu đồ.',
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
              ),
            )
          else
            SizedBox(
              height: 180,
              child: BarChart(
                BarChartData(
                  alignment: BarChartAlignment.spaceAround,
                  maxY: 100,
                  minY: 0,
                  gridData: FlGridData(
                    show: true,
                    drawVerticalLine: false,
                    horizontalInterval: 25,
                    getDrawingHorizontalLine: (v) => FlLine(
                      color: scheme.outline.withValues(alpha: 0.15),
                      strokeWidth: 1,
                    ),
                  ),
                  borderData: FlBorderData(show: false),
                  titlesData: FlTitlesData(
                    topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    leftTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 28,
                        interval: 25,
                        getTitlesWidget: (value, meta) {
                          if (value % 25 != 0) return const SizedBox.shrink();
                          return Text(
                            '${value.toInt()}%',
                            style: TextStyle(fontSize: 9, color: scheme.onSurfaceVariant),
                          );
                        },
                      ),
                    ),
                    bottomTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 22,
                        getTitlesWidget: (value, meta) {
                          final i = value.toInt();
                          if (i < 0 || i >= buckets.length) return const SizedBox.shrink();
                          return Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text(
                              _shortLabel(buckets[i].periodLabel),
                              style: TextStyle(
                                fontSize: 8,
                                fontWeight: buckets[i].isCurrentPeriod
                                    ? FontWeight.w700
                                    : FontWeight.w500,
                                color: buckets[i].isCurrentPeriod
                                    ? scheme.primary
                                    : scheme.onSurfaceVariant,
                              ),
                            ),
                          );
                        },
                      ),
                    ),
                  ),
                  barTouchData: BarTouchData(
                    enabled: true,
                    touchTooltipData: BarTouchTooltipData(
                      getTooltipItem: (group, groupIndex, rod, rodIndex) {
                        final b = buckets[group.x];
                        final rate = _decided(b) > 0
                            ? '${_rate(b).toStringAsFixed(1)}%'
                            : '—';
                        return BarTooltipItem(
                          '${b.periodLabel}\n$rate · W${_win(b)}/L${_lose(b)}',
                          TextStyle(fontSize: 11, color: scheme.onPrimary),
                        );
                      },
                    ),
                  ),
                  barGroups: [
                    for (var i = 0; i < buckets.length; i++)
                      BarChartGroupData(
                        x: i,
                        barRods: [
                          BarChartRodData(
                            toY: _decided(buckets[i]) > 0 ? _rate(buckets[i]) : 2,
                            width: buckets.length > 8 ? 10 : 14,
                            borderRadius: const BorderRadius.vertical(top: Radius.circular(4)),
                            color: _decided(buckets[i]) == 0
                                ? scheme.outline.withValues(alpha: 0.35)
                                : _rate(buckets[i]) >= 50
                                    ? scheme.primary.withValues(
                                        alpha: buckets[i].isCurrentPeriod ? 1 : 0.75,
                                      )
                                    : scheme.error.withValues(
                                        alpha: buckets[i].isCurrentPeriod ? 1 : 0.75,
                                      ),
                          ),
                        ],
                      ),
                  ],
                ),
              ),
            ),
          if (current != null && _decided(current) > 0) ...[
            const SizedBox(height: 8),
            Text(
              'Kỳ hiện tại (${current.periodLabel}): '
              '${_rate(current).toStringAsFixed(1)}%'
              '${!realized && current.deltaWinRatePercent != null ? ' · ${current.deltaWinRatePercent! >= 0 ? '+' : ''}${current.deltaWinRatePercent!.toStringAsFixed(1)}pp vs kỳ trước' : ''}',
              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
            ),
          ],
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
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Text(
                  _buyPointLabel(item.alertType),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: scheme.onSurface,
                  ),
                ),
              ),
              if (item.positionStatus != 'None') ...[
                const SizedBox(width: 6),
                _PositionStatusBadge(item: item),
              ],
            ],
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
          if (item.isPositionClosed) ...[
            const SizedBox(height: 4),
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Expanded(
                  child: Text(
                    'Bán nửa ${item.sell1Price != null ? formatPrice(item.sell1Price!) : '—'} '
                    '→ Bán hết ${item.sellAllPrice != null ? formatPrice(item.sellAllPrice!) : '—'}'
                    '${item.holdingSessions != null ? ' · giữ ${item.holdingSessions} phiên' : ''}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                  ),
                ),
                if (item.realizedReturnPercent != null) ...[
                  const SizedBox(width: 8),
                  ChangePill(item.realizedReturnPercent!),
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
        'BuyPoint1' => 'Giá vào 1',
        'BuyPoint2' => 'Giá vào 2',
        _ => 'Giá vào',
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

class _PositionStatusBadge extends StatelessWidget {
  const _PositionStatusBadge({required this.item});

  final AlertHistoryItem item;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final String label;
    final Color color;
    if (item.isPositionClosed) {
      label = item.isRealizedApproximate ? 'Gần đúng' : 'Đã đóng';
      color = item.isRealizedApproximate
          ? (isDark ? AppColors.darkWarning : AppColors.lightWarning)
          : scheme.primary;
    } else if (item.isPositionOpen) {
      label = 'Đang mở';
      color = scheme.onSurfaceVariant;
    } else {
      return const SizedBox.shrink();
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withValues(alpha: 0.4)),
      ),
      child: Text(
        label,
        style: TextStyle(fontSize: 9, fontWeight: FontWeight.w700, color: color),
      ),
    );
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
