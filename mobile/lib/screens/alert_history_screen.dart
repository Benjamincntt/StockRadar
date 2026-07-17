import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
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
      final data = await _api.getAlertHistory(limit: 100, kind: 'buy');
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
      title: 'Lịch sử Mua điểm',
      subtitle: 'Đúng / sai sau T+2.5 · chỉ Điểm mua 1 & 2',
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
            'Tỷ lệ Win',
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
                ? 'Chưa có lệnh Win/Lose rõ ràng'
                : 'Win / (Win + Lose) · Flat không tính',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
          const SizedBox(height: 14),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
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
    final style = _outcomeStyle(context);

    return GlassCard(
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: style.bg,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    item.symbol,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: dataFont(context, size: 18, weight: FontWeight.w800),
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  style.label,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w800,
                    color: style.accent,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
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
            const SizedBox(height: 8),
            Text(
              'Phiên ${formatApiDateVietnam(item.entryDate)} : giá ${_fmtPrice(item.entryPrice)}',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
            ),
            if (item.isPending) ...[
              const SizedBox(height: 6),
              Text(
                'Phiên T+2.5 : chờ đo',
                style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
              ),
            ] else if (item.isMeasured) ...[
              const SizedBox(height: 6),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'Phiên T+2.5 : giá ${_fmtPrice(_forwardPrice(item))}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: dataFont(
                        context,
                        size: 13,
                        weight: FontWeight.w700,
                        color: style.accent,
                      ),
                    ),
                  ),
                  if (item.forwardReturnPercent != null) ...[
                    const SizedBox(width: 8),
                    Text(
                      _fmtPct(item.forwardReturnPercent!),
                      style: dataFont(
                        context,
                        size: 14,
                        weight: FontWeight.w800,
                        color: style.accent,
                      ),
                    ),
                  ],
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  _OutcomeStyle _outcomeStyle(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (item.isPending) {
      return _OutcomeStyle(
        label: 'Chờ',
        accent: scheme.onSurfaceVariant,
        bg: AppColors.neutralBg(context),
      );
    }
    if (item.isSuccess == true) {
      return _OutcomeStyle(
        label: 'Win',
        accent: scheme.primary,
        bg: AppColors.positiveDim(context),
      );
    }
    if (item.isSuccess == false) {
      return _OutcomeStyle(
        label: 'Lose',
        accent: scheme.error,
        bg: AppColors.negativeDim(context),
      );
    }
    return _OutcomeStyle(
      label: 'Flat',
      accent: scheme.onSurfaceVariant,
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

  static String _fmtPrice(double v) {
    if (v <= 0) return '—';
    if (v >= 1000) return v.toStringAsFixed(0);
    return v.toStringAsFixed(2);
  }

  static String _fmtPct(double v) {
    final sign = v > 0 ? '+' : '';
    return '$sign${v.toStringAsFixed(0)}%';
  }
}

class _OutcomeStyle {
  const _OutcomeStyle({
    required this.label,
    required this.accent,
    required this.bg,
  });

  final String label;
  final Color accent;
  final Color bg;
}
