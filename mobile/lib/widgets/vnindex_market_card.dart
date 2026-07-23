import 'package:flutter/material.dart';

import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/time/api_date.dart';
import 'glass_card.dart';

/// Card VNINDEX đầu Home — tổng quan giá / KL·GT / độ rộng (không chart).
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
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final data = snapshot;
    final upColor = isDark ? AppColors.darkPrimary : AppColors.lightPrimary;
    final downColor = isDark ? AppColors.darkError : AppColors.lightError;
    final flatColor = isDark ? AppColors.darkWarning : AppColors.lightWarning;
    final bullish = (data?.changePercent ?? 0) >= 0;
    final changeColor = bullish ? upColor : downColor;

    if (data == null && loading) {
      return GlassCard(
        wave: true,
        child: const SizedBox(
          height: 120,
          child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
        ),
      );
    }

    if (data == null) {
      return GlassCard(
        wave: true,
        child: Text(
          'Chưa có dữ liệu VNINDEX.',
          style: TextStyle(color: scheme.onSurfaceVariant, fontSize: 13),
        ),
      );
    }

    final totalBreath = data.advancing + data.unchanged + data.declining;
    final upW = totalBreath == 0 ? 0.0 : data.advancing / totalBreath;
    final flatW = totalBreath == 0 ? 0.0 : data.unchanged / totalBreath;
    final downW = totalBreath == 0 ? 0.0 : data.declining / totalBreath;

    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(
                  color: upColor.withValues(alpha: isDark ? 0.22 : 0.14),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.show_chart, size: 14, color: upColor),
                    const SizedBox(width: 4),
                    Text(
                      'VN-INDEX',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w800,
                        color: upColor,
                      ),
                    ),
                  ],
                ),
              ),
              const Spacer(),
              Flexible(
                child: Text(
                  data.exchangeLabel,
                  textAlign: TextAlign.right,
                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                _formatIndex(data.price),
                style: TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.w800,
                  height: 1,
                  color: scheme.onSurface,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 2),
                  child: Row(
                    children: [
                      Icon(
                        bullish ? Icons.arrow_drop_up : Icons.arrow_drop_down,
                        color: changeColor,
                        size: 22,
                      ),
                      Flexible(
                        child: Text(
                          '${bullish ? '+' : ''}${data.changePoints.toStringAsFixed(2)} '
                          '(${bullish ? '+' : ''}${data.changePercent.toStringAsFixed(2)}%)',
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w700,
                            color: changeColor,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            data.phaseLabelVi,
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w600,
              color: _phaseColor(data.phase, upColor, flatColor, downColor),
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'KL ${_formatVolume(data.volume)}   ·   GT ${_formatTurnover(data.turnoverBillionVnd)}',
            style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: scheme.onSurface),
          ),
          const SizedBox(height: 12),
          ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: SizedBox(
              height: 6,
              child: Row(
                children: [
                  if (upW > 0) Expanded(flex: _flex(upW), child: ColoredBox(color: upColor)),
                  if (flatW > 0) Expanded(flex: _flex(flatW), child: ColoredBox(color: flatColor)),
                  if (downW > 0) Expanded(flex: _flex(downW), child: ColoredBox(color: downColor)),
                  if (totalBreath == 0) const Expanded(child: ColoredBox(color: Colors.grey)),
                ],
              ),
            ),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              _BreathLegend(color: upColor, label: '${data.advancing} Tăng'),
              const SizedBox(width: 14),
              _BreathLegend(color: flatColor, label: '${data.unchanged} Đứng'),
              const SizedBox(width: 14),
              _BreathLegend(color: downColor, label: '${data.declining} Giảm'),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Cập nhật ${formatApiDateTime(data.asOfUtc)}'
            '${usingCachedSnapshot ? ' · snapshot' : ''} · làm mới mỗi 1 phút',
            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }

  static Color _phaseColor(String phase, Color up, Color flat, Color down) {
    switch (phase) {
      case 'Favorable':
        return up;
      case 'Neutral':
        return flat;
      default:
        return down;
    }
  }

  static int _flex(double w) => (w * 1000).round().clamp(1, 1000);

  static String _formatIndex(double v) {
    final parts = v.toStringAsFixed(2).split('.');
    final whole = parts[0];
    final buf = StringBuffer();
    for (var i = 0; i < whole.length; i++) {
      final fromEnd = whole.length - i;
      if (i > 0 && fromEnd % 3 == 0) buf.write(',');
      buf.write(whole[i]);
    }
    return '${buf.toString()}.${parts[1]}';
  }

  static String _formatVolume(int volume) {
    if (volume >= 1000000000) return '${(volume / 1000000000).toStringAsFixed(2)}B';
    if (volume >= 1000000) return '${(volume / 1000000).toStringAsFixed(2)}M';
    if (volume >= 1000) return '${(volume / 1000).toStringAsFixed(1)}K';
    return '$volume';
  }

  static String _formatTurnover(double billionVnd) {
    if (billionVnd >= 1000) {
      return '${_formatIndex(billionVnd)} tỷ';
    }
    return '${billionVnd.toStringAsFixed(1)} tỷ';
  }
}

class _BreathLegend extends StatelessWidget {
  const _BreathLegend({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 7,
          height: 7,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 5),
        Text(
          label,
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w600,
            color: Theme.of(context).colorScheme.onSurface,
          ),
        ),
      ],
    );
  }
}
