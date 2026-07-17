import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';
import '../widgets/glass_card.dart';
import '../widgets/pushed_page_scaffold.dart';

class AlertHistoryScreen extends StatefulWidget {
  const AlertHistoryScreen({super.key});

  @override
  State<AlertHistoryScreen> createState() => _AlertHistoryScreenState();
}

class _AlertHistoryScreenState extends State<AlertHistoryScreen> {
  ApiClient get _api => context.read<ApiClient>();
  AlertHistoryResponse? _data;
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
      final data = await _api.getAlertHistory(limit: 100);
      if (!mounted) return;
      setState(() => _data = data);
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'Không tải được lịch sử lệnh.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final data = _data;

    return PushedPageScaffold(
      title: 'Lịch sử lệnh',
      subtitle: 'Đúng / sai sau T+2.5 · Top cơ hội & Mua điểm',
      padding: EdgeInsets.zero,
      child: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          children: [
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
                _SuccessRateHeader(data: data),
                const SizedBox(height: 12),
                if (data.alerts.isEmpty)
                  GlassCard(
                    child: Text(
                      'Chưa có lệnh được theo dõi.',
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
      ),
    );
  }
}

class _SuccessRateHeader extends StatelessWidget {
  const _SuccessRateHeader({required this.data});

  final AlertHistoryResponse data;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final decided = data.totalSuccess + data.totalFailed;
    final rateText = decided == 0
        ? '—'
        : '${data.overallSuccessRatePercent.toStringAsFixed(1)}%';

    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Tỷ lệ đúng',
            style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
          ),
          const SizedBox(height: 4),
          Text(
            rateText,
            style: dataFont(
              context,
              size: 36,
              weight: FontWeight.w800,
              color: scheme.primary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            decided == 0
                ? 'Chưa có lệnh lãi/lỗ rõ ràng (Good/Failed)'
                : 'Good / (Good + Failed) · Flat không tính',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              _StatChip(label: 'Đúng', value: '${data.totalSuccess}', positive: true),
              _StatChip(label: 'Sai', value: '${data.totalFailed}', negative: true),
              _StatChip(label: 'Flat', value: '${data.totalFlat}'),
              _StatChip(label: 'Chờ đo', value: '${data.totalPending}'),
            ],
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

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: scheme.surface.withValues(alpha: 0.55),
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
    final (badgeLabel, badgeColor) = _badge(scheme);

    return GlassCard(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.symbol,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: dataFont(context, size: 16, weight: FontWeight.w800),
                ),
                const SizedBox(height: 2),
                Text(
                  item.alertTypeLabel,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
                const SizedBox(height: 4),
                Text(
                  'Phiên ${formatApiDateVietnam(item.entryDate)} · Entry ${_fmtPrice(item.entryPrice)}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
                if (item.isMeasured && item.forwardReturnPercent != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    'T+2.5: ${_fmtPct(item.forwardReturnPercent!)}',
                    style: dataFont(
                      context,
                      size: 13,
                      weight: FontWeight.w600,
                      color: item.forwardReturnPercent! >= 0
                          ? scheme.primary
                          : scheme.error,
                    ),
                  ),
                ],
                if (item.isPending) ...[
                  const SizedBox(height: 4),
                  Text(
                    'Chờ đo T+2.5',
                    style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 8),
          ConstrainedBox(
            constraints: const BoxConstraints(minWidth: 52),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: badgeColor.withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                badgeLabel,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w800,
                  color: badgeColor,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  (String, Color) _badge(ColorScheme scheme) {
    if (item.isPending) return ('Chờ', scheme.onSurfaceVariant);
    if (item.isSuccess == true) return ('True', scheme.primary);
    if (item.isSuccess == false) return ('False', scheme.error);
    return ('Flat', scheme.secondary);
  }

  static String _fmtPrice(double v) {
    if (v >= 1000) return v.toStringAsFixed(0);
    return v.toStringAsFixed(2);
  }

  static String _fmtPct(double v) {
    final sign = v > 0 ? '+' : '';
    return '$sign${v.toStringAsFixed(2)}%';
  }
}
