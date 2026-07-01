import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_theme.dart';
import '../widgets/glass_card.dart';

class PerformanceScreen extends StatefulWidget {
  const PerformanceScreen({super.key});

  @override
  State<PerformanceScreen> createState() => _PerformanceScreenState();
}

class _PerformanceScreenState extends State<PerformanceScreen> {
  ApiClient get _api => context.read<ApiClient>();
  OpportunityPerformanceSummary? _data;
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
      final data = await _api.getPerformanceSummary();
      setState(() => _data = data);
    } catch (_) {
      setState(() => _error = 'Không tải được dữ liệu hiệu quả.');
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (_loading) return const LoadingView();

    final data = _data;
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        children: [
          const PageHeader(
            title: 'Hiệu quả Top cơ hội',
            subtitle: 'Review tự động hàng tuần · đo T+2.5 phiên VN',
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
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
                child: Text(data.shadowStatusMessage!, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
              ),
          ],
        ],
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
              Expanded(child: _MetricPill(label: 'Hệ số global', value: '×${factor.toStringAsFixed(3)}', accent: true)),
              const SizedBox(width: 8),
              Expanded(
                child: _MetricPill(
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
    final winRate = (review['winRatePercent'] as num?)?.toDouble();
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
            _MetricPill(label: 'Win rate', value: '${winRate.toStringAsFixed(1)}%', accent: true),
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

class _MetricPill extends StatelessWidget {
  const _MetricPill({required this.label, required this.value, this.accent = false, this.danger = false});

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
