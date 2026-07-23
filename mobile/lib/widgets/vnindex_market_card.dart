import 'package:flutter/material.dart';

import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/time/api_date.dart';
import 'chart_widgets.dart';
import 'glass_card.dart';

/// Card VNINDEX đầu Home: chart giống detail (MA20) + badge pha tăng trưởng.
class VnIndexMarketCard extends StatelessWidget {
  const VnIndexMarketCard({
    super.key,
    required this.snapshot,
    this.loading = false,
    this.usingCachedSnapshot = false,
  });

  final VnIndexChartSnapshot? snapshot;
  final bool loading;
  final bool usingCachedSnapshot;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final data = snapshot;
    final bullish = (data?.changePercent ?? 0) >= 0;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final accent = bullish
        ? (isDark ? AppColors.darkPrimary : AppColors.lightPrimary)
        : scheme.error;

    return GlassCard(
      wave: true,
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
                    Text(
                      data?.symbol ?? 'VNINDEX',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      data == null ? '—' : data.price.toStringAsFixed(2),
                      style: TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                        color: scheme.onSurface,
                      ),
                    ),
                    if (data != null)
                      Text(
                        '${data.changePercent >= 0 ? '+' : ''}${data.changePercent.toStringAsFixed(2)}%',
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: accent,
                        ),
                      ),
                  ],
                ),
              ),
              if (data != null) _PhaseBadge(phase: data.phase, label: data.phaseLabelVi),
            ],
          ),
          if (data != null) ...[
            const SizedBox(height: 6),
            Text(
              _flagsLine(data),
              style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant, height: 1.3),
            ),
          ],
          const SizedBox(height: 8),
          if (data == null && loading)
            const SizedBox(
              height: 200,
              child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
            )
          else if (data == null)
            Text(
              'Chưa có dữ liệu VNINDEX.',
              style: TextStyle(color: scheme.onSurfaceVariant, fontSize: 13),
            )
          else
            PriceVolumeChart(
              bars: data.bars,
              interval: data.interval,
              symbol: data.symbol,
              name: 'VN-Index',
              loading: loading && data.bars.isEmpty,
              livePrice: data.price,
              liveChangePercent: data.changePercent,
              ma20Focus: true,
              compact: true,
            ),
          const SizedBox(height: 8),
          Text(
            data == null
                ? 'Làm mới mỗi 1 phút'
                : 'Cập nhật ${formatApiDateTime(data.asOfUtc)}${usingCachedSnapshot ? ' · snapshot' : ''} · làm mới mỗi 1 phút',
            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }

  static String _flagsLine(VnIndexChartSnapshot d) {
    String bit(bool ok, String label) => ok ? '✓ $label' : '· $label';
    return [
      bit(d.closeAboveMa20, 'Close>MA20'),
      bit(d.ma20SlopeNonNegative, 'Slope'),
      bit(d.hasFollowThroughDay, 'FTD'),
      bit(d.hasHigherLow, 'HL'),
    ].join('  ');
  }
}

class _PhaseBadge extends StatelessWidget {
  const _PhaseBadge({required this.phase, required this.label});

  final String phase;
  final String label;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final Color bg;
    final Color fg;
    switch (phase) {
      case 'Favorable':
        bg = (isDark ? AppColors.darkPrimary : AppColors.lightPrimary).withValues(alpha: 0.18);
        fg = isDark ? AppColors.darkPrimary : AppColors.lightPrimary;
      case 'Neutral':
        bg = (isDark ? AppColors.darkWarning : AppColors.lightWarning).withValues(alpha: 0.2);
        fg = isDark ? AppColors.darkWarning : AppColors.lightWarning;
      default:
        bg = Theme.of(context).colorScheme.error.withValues(alpha: 0.15);
        fg = Theme.of(context).colorScheme.error;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: fg),
      ),
    );
  }
}
