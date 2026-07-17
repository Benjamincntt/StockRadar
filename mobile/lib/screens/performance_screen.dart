import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_theme.dart';
import '../widgets/glass_card.dart';
import '../widgets/pushed_page_scaffold.dart';
import '../widgets/smart_money_backtest_card.dart';

class PerformanceScreen extends StatefulWidget {
  const PerformanceScreen({super.key});

  @override
  State<PerformanceScreen> createState() => _PerformanceScreenState();
}

class _PerformanceScreenState extends State<PerformanceScreen> {
  ApiClient get _api => context.read<ApiClient>();
  OpportunityPerformanceSummary? _data;
  var _loadingLive = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadLive();
  }

  Future<void> _loadLive() async {
    setState(() {
      _loadingLive = true;
      _error = null;
    });
    try {
      final data = await _api.getPerformanceSummary();
      if (!mounted) return;
      setState(() => _data = data);
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'Không tải được dữ liệu hiệu quả live.');
    } finally {
      if (mounted) setState(() => _loadingLive = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final data = _data;

    return PushedPageScaffold(
      title: 'Hiệu quả & Backtest',
      subtitle: 'Review live T+2.5 · replay SmartMoney trên lịch sử',
      padding: EdgeInsets.zero,
      child: RefreshIndicator(
        onRefresh: _loadLive,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          children: [
          GlassCard(
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(Icons.fact_check_outlined, color: scheme.primary),
              title: const Text(
                'Lịch sử lệnh & đúng/sai T+2.5',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
              subtitle: const Text(
                'Top cơ hội · Mua điểm · tỷ lệ thành công',
                style: TextStyle(fontSize: 11),
              ),
              trailing: Icon(Icons.chevron_right, color: scheme.onSurfaceVariant),
              onTap: () => context.push('/performance/alert-history'),
            ),
          ),
          const SizedBox(height: 16),
          const SmartMoneyBacktestCard(),
          const SizedBox(height: 16),
          if (_loadingLive)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 24),
              child: Center(child: CircularProgressIndicator()),
            )
          else ...[
            if (_error != null) ...[
              ErrorBanner(message: _error!, onRetry: _loadLive),
              const SizedBox(height: 12),
            ],
            if (data != null) ...[
              if (data.statusMessage != null && data.weeklyReview == null) ...[
                GlassCard(child: Text(data.statusMessage!, style: TextStyle(color: scheme.onSurfaceVariant))),
                const SizedBox(height: 12),
              ],
              if (data.calibration != null) ...[
                _CalibrationCard(calibration: data.calibration!),
                const SizedBox(height: 12),
              ],
              if (data.weeklyReview != null) ...[
                _WeeklyReviewCard(review: data.weeklyReview!),
                const SizedBox(height: 12),
              ],
              if (data.shadowStatusMessage != null)
                GlassCard(
                  child: Text(
                    data.shadowStatusMessage!,
                    style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                  ),
                ),
            ],
          ],
        ],
        ),
      ),
    );
  }
}

class _CalibrationCard extends StatelessWidget {
  const _CalibrationCard({required this.calibration});

  final Map<String, dynamic> calibration;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final factor = (calibration['globalFactor'] as num?)?.toDouble() ?? 1;
    final bias = (calibration['predictionBiasPercent'] as num?)?.toDouble() ?? 0;
    final samples = (calibration['totalSamples'] as num?)?.toInt() ?? 0;
    final buckets = calibration['buckets'] as List<dynamic>? ?? [];

    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SectionTitle('Calibration P(thành công)', subtitle: 'Hiệu chỉnh từ setup đã đo T+2.5'),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _LiveMetricPill(label: 'Hệ số global', value: '×${factor.toStringAsFixed(3)}', accent: true)),
              const SizedBox(width: 8),
              Expanded(
                child: _LiveMetricPill(
                  label: 'Lệch dự báo',
                  value: '${bias >= 0 ? '+' : ''}${bias.toStringAsFixed(1)}%',
                  danger: bias.abs() > 10,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text('$samples setup có P(hit) lúc vào Top', style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
          if (buckets.isNotEmpty) ...[
            const SizedBox(height: 12),
            ...buckets.map((b) {
              final m = b as Map<String, dynamic>;
              return Padding(
                padding: const EdgeInsets.only(bottom: 6),
                child: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: scheme.surface.withValues(alpha: 0.5),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          'P ${m['bucketId']} · n=${m['sampleCount']}',
                          style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                        ),
                      ),
                      Text(
                        'dự ${m['predictedMidPercent']}% → thực ${m['actualHitRatePercent']}%',
                        style: dataFont(context, size: 11, weight: FontWeight.w600),
                      ),
                    ],
                  ),
                ),
              );
            }),
          ],
        ],
      ),
    );
  }
}

class _WeeklyReviewCard extends StatelessWidget {
  const _WeeklyReviewCard({required this.review});

  final Map<String, dynamic> review;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final winRate = (review['successRatePercent'] as num?)?.toDouble()
        ?? (review['winRatePercent'] as num?)?.toDouble();
    final measured = (review['measuredCount'] as num?)?.toInt() ?? 0;
    final good = (review['goodCount'] as num?)?.toInt() ?? 0;

    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SectionTitle(
            'Weekly review',
            subtitle: review['weekStartDate']?.toString(),
          ),
          const SizedBox(height: 12),
          if (winRate != null)
            _LiveMetricPill(label: 'Win rate', value: '${winRate.toStringAsFixed(1)}%', accent: true),
          const SizedBox(height: 8),
          Text('Đo $measured setup · $good tốt', style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
          if (review['summary'] != null) ...[
            const SizedBox(height: 8),
            Text(review['summary'].toString(), style: const TextStyle(fontSize: 13)),
          ],
        ],
      ),
    );
  }
}

class _LiveMetricPill extends StatelessWidget {
  const _LiveMetricPill({required this.label, required this.value, this.accent = false, this.danger = false});

  final String label;
  final String value;
  final bool accent;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = danger ? scheme.error : (accent ? scheme.primary : scheme.onSurface);
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: scheme.surface.withValues(alpha: 0.5),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
          const SizedBox(height: 4),
          Text(value, style: dataFont(context, size: 16, weight: FontWeight.w700, color: color)),
        ],
      ),
    );
  }
}
