import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class SmartMoneyBacktestCard extends StatefulWidget {
  const SmartMoneyBacktestCard({super.key});

  @override
  State<SmartMoneyBacktestCard> createState() => _SmartMoneyBacktestCardState();
}

class _SmartMoneyBacktestCardState extends State<SmartMoneyBacktestCard> {
  ApiClient get _api => context.read<ApiClient>();

  int _days = 90;
  int _maxPicks = 10;
  int _holdSessions = 5;
  String _mode = 'relaxed';

  var _running = false;
  String? _error;
  SmartMoneyBacktestResult? _result;

  Future<void> _run() async {
    setState(() {
      _running = true;
      _error = null;
    });
    try {
      final data = await _api.runSmartMoneyBacktest(
        days: _days,
        maxPicksPerDay: _maxPicks,
        holdSessions: _holdSessions,
        mode: _mode,
      );
      if (!mounted) return;
      setState(() => _result = data);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _error = e.message);
    } catch (_) {
      if (!mounted) return;
      setState(() => _error = 'Backtest thất bại — thử lại.');
    } finally {
      if (mounted) setState(() => _running = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final summary = _result?.summary;
    final trades = [...?_result?.trades]
      ..sort((a, b) {
        final byDate = b.entryDate.compareTo(a.entryDate);
        if (byDate != 0) return byDate;
        return a.symbol.compareTo(b.symbol);
      });
    final recent = trades.take(40).toList();

    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SectionTitle(
            'Backtest SmartMoney',
            subtitle: 'Replay trên lịch sử OHLCV — win rate & drawdown đa mã',
          ),
          const SizedBox(height: 12),
          _DropdownRow<int>(
            label: 'Số phiên',
            value: _days,
            items: const [30, 60, 90, 120, 180],
            labelBuilder: (v) => '$v ngày',
            onChanged: _running ? null : (v) => setState(() => _days = v),
          ),
          const SizedBox(height: 8),
          _DropdownRow<int>(
            label: 'Top mã / ngày',
            value: _maxPicks,
            items: const [5, 10, 15, 20, 30],
            labelBuilder: (v) => '$v',
            onChanged: _running ? null : (v) => setState(() => _maxPicks = v),
          ),
          const SizedBox(height: 8),
          _DropdownRow<int>(
            label: 'Giữ (T+N)',
            value: _holdSessions,
            items: const [3, 5, 10, 20],
            labelBuilder: (v) => 'T+$v',
            onChanged: _running ? null : (v) => setState(() => _holdSessions = v),
          ),
          const SizedBox(height: 8),
          _DropdownRow<String>(
            label: 'Chế độ',
            value: _mode,
            items: const ['relaxed', 'strict-then-relaxed', 'strict'],
            labelBuilder: (v) => switch (v) {
              'relaxed' => 'Nới (top Buy Score)',
              'strict' => 'Strict SmartMoney',
              _ => 'Strict → fallback',
            },
            onChanged: _running ? null : (v) => setState(() => _mode = v),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _running ? null : _run,
            child: Text(_running ? 'Đang chạy backtest...' : 'Chạy backtest'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: TextStyle(fontSize: 13, color: scheme.error)),
          ],
          if (summary != null) ...[
            const SizedBox(height: 16),
            Text(
              '${formatApiDate(summary.fromDate)} → ${formatApiDate(summary.toDate)}'
              ' · ${summary.universeSize} mã'
              ' · ${summary.daysWithPicks}/${summary.tradingDaysScanned} ngày có tín hiệu'
              ' · thắng ≥${summary.successThresholdPercent.toStringAsFixed(0)}%',
              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant, height: 1.35),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: _MetricPill(
                    label: 'Win rate',
                    value: '${summary.winRatePercent.toStringAsFixed(1)}%',
                    accent: true,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _MetricPill(
                    label: 'Lợi nhuận TB',
                    value: formatPercent(summary.avgReturnPercent),
                    accent: summary.avgReturnPercent > 0,
                    danger: summary.avgReturnPercent < 0,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: _MetricPill(
                    label: 'Median',
                    value: formatPercent(summary.medianReturnPercent),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: _MetricPill(
                    label: 'Max DD',
                    value: '${summary.maxDrawdownPercent.toStringAsFixed(1)}%',
                    danger: true,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(child: _CountBox(label: 'Tốt', value: summary.winCount, color: scheme.primary)),
                const SizedBox(width: 8),
                Expanded(child: _CountBox(label: 'Ngang', value: summary.flatCount, color: scheme.onSurface)),
                const SizedBox(width: 8),
                Expanded(child: _CountBox(label: 'Xịt', value: summary.lossCount, color: scheme.error)),
              ],
            ),
            Text(
              'Tổng ${summary.totalTrades} lệnh (vào đóng cửa, thoát T+$_holdSessions)',
              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
            ),
          ],
          if (recent.isNotEmpty) ...[
            const SizedBox(height: 16),
            Text(
              'Lệnh gần đây (${recent.length})',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: scheme.onSurfaceVariant),
            ),
            const SizedBox(height: 8),
            ...recent.map((t) => _TradeRow(trade: t)),
          ],
        ],
      ),
    );
  }
}

class _DropdownRow<T> extends StatelessWidget {
  const _DropdownRow({
    required this.label,
    required this.value,
    required this.items,
    required this.labelBuilder,
    required this.onChanged,
  });

  final String label;
  final T value;
  final List<T> items;
  final String Function(T) labelBuilder;
  final void Function(T)? onChanged;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
        const SizedBox(height: 4),
        DropdownButtonFormField<T>(
          value: value,
          isExpanded: true,
          decoration: InputDecoration(
            isDense: true,
            contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            filled: true,
            fillColor: scheme.surfaceContainerHighest.withValues(alpha: 0.5),
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
          ),
          items: items
              .map((v) => DropdownMenuItem(value: v, child: Text(labelBuilder(v), style: const TextStyle(fontSize: 14))))
              .toList(),
          onChanged: onChanged == null ? null : (v) { if (v != null) onChanged!(v); },
        ),
      ],
    );
  }
}

class _TradeRow extends StatelessWidget {
  const _TradeRow({required this.trade});

  final SmartMoneyBacktestTrade trade;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final outcome = trade.outcome;
    final (bg, fg, label) = switch (outcome) {
      'Good' => (scheme.primaryContainer.withValues(alpha: 0.5), scheme.primary, 'Tốt'),
      'Bad' => (scheme.errorContainer.withValues(alpha: 0.5), scheme.error, 'Xịt'),
      _ => (scheme.surfaceContainerHighest, scheme.onSurfaceVariant, 'Ngang'),
    };

    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () => context.push('/stocks/${trade.symbol}'),
          borderRadius: BorderRadius.circular(12),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: scheme.surface.withValues(alpha: 0.5),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.5)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(trade.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                      Text(
                        '${formatApiDate(trade.entryDate)} · điểm ${trade.buyScore}'
                        '${trade.usedRelaxedFallback ? ' · fallback' : ''}',
                        style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(20)),
                      child: Text(
                        '$label ${formatPercent(trade.returnPercent)}',
                        style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: fg),
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${formatPrice(trade.entryPrice)} → ${formatPrice(trade.exitPrice)}',
                      style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _CountBox extends StatelessWidget {
  const _CountBox({required this.label, required this.value, required this.color});

  final String label;
  final int value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface.withValues(alpha: 0.5),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        children: [
          Text(label, style: TextStyle(fontSize: 10, color: Theme.of(context).colorScheme.onSurfaceVariant)),
          Text('$value', style: dataFont(context, size: 18, weight: FontWeight.w700, color: color)),
        ],
      ),
    );
  }
}

class _MetricPill extends StatelessWidget {
  const _MetricPill({
    required this.label,
    required this.value,
    this.accent = false,
    this.danger = false,
  });

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
