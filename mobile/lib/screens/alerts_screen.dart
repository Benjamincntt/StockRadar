import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/market_hub_service.dart';
import '../core/theme/app_colors.dart';
import '../core/time/api_date.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class AlertsScreen extends StatefulWidget {
  const AlertsScreen({super.key});

  @override
  State<AlertsScreen> createState() => _AlertsScreenState();
}

class _AlertsScreenState extends State<AlertsScreen> {
  ApiClient get _api => context.read<ApiClient>();
  MarketHubService get _hub => context.read<MarketHubService>();

  var _labelFilter = 'Tất cả';
  List<TradeEvent> _trades = [];
  IntradayMonitorStatus? _monitor;
  var _loading = true;
  String? _error;

  static const _filters = [
    'Tất cả',
    'Gom im',
    'Đẩy giá',
    'Xả hàng',
    'Khối ngoại mạnh',
  ];

  static const _filterToApi = <String, String?>{
    'Tất cả': null,
    'Gom im': 'GomIm',
    'Đẩy giá': 'DayGia',
    'Xả hàng': 'Xa',
    'Khối ngoại mạnh': 'ForeignStrong',
  };

  @override
  void initState() {
    super.initState();
    _hub.addListener(_onLiveTrade);
    _load();
  }

  @override
  void dispose() {
    _hub.removeListener(_onLiveTrade);
    super.dispose();
  }

  void _onLiveTrade() {
    if (!mounted || _labelFilter != 'Tất cả') return;
    final live = _hub.recentTrades;
    if (live.isEmpty) return;
    setState(() {
      final keys = _trades.map((t) => '${t.symbol}-${t.at}-${t.volume}').toSet();
      final merged = [
        ...live.where((t) => !keys.contains('${t.symbol}-${t.at}-${t.volume}')),
        ..._trades,
      ];
      _trades = merged.take(40).toList();
    });
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _api.getTradeEvents(label: _filterToApi[_labelFilter]),
        _api.getIntradayMonitorStatus(),
      ]);
      setState(() {
        _trades = results[0] as List<TradeEvent>;
        _monitor = results[1] as IntradayMonitorStatus;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  Color _labelAccent(BuildContext context, String label) {
    final scheme = Theme.of(context).colorScheme;
    switch (label) {
      case 'DayGia':
        return scheme.primary;
      case 'Xa':
        return scheme.error;
      case 'GomIm':
        return scheme.tertiary;
      default:
        return scheme.onSurfaceVariant;
    }
  }

  Color _labelBg(BuildContext context, String label) {
    switch (label) {
      case 'DayGia':
        return AppColors.positiveDim(context);
      case 'Xa':
        return AppColors.negativeDim(context);
      case 'GomIm':
        return Theme.of(context).colorScheme.tertiaryContainer.withValues(alpha: 0.35);
      default:
        return Theme.of(context).colorScheme.surfaceContainerHighest;
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (_loading) return const LoadingView();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        children: [
          const PageHeader(
            title: 'Khớp lệnh',
            subtitle: 'Lô lớn · VSA · dòng tiền NN/Tự doanh',
          ),
          const SizedBox(height: 12),
          if (_monitor != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: GlassCard(
                padding: const EdgeInsets.all(12),
                child: Text(_monitor!.status, style: const TextStyle(fontSize: 12)),
              ),
            ),
          FilterChips(
            options: _filters,
            selected: _labelFilter,
            onSelected: (v) {
              setState(() => _labelFilter = v);
              _load();
            },
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          if (_trades.isEmpty)
            GlassCard(
              child: Text(
                'Chưa có lô lớn trong phiên.',
                style: TextStyle(color: scheme.onSurfaceVariant),
              ),
            )
          else
            ..._trades.map(
              (t) => Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: _tradeRow(t),
              ),
            ),
        ],
      ),
    );
  }

  Widget _tradeRow(TradeEvent t) {
    final scheme = Theme.of(context).colorScheme;
    final accent = _labelAccent(context, t.label);
    final bg = _labelBg(context, t.label);
    final label = tradeLabelVi(t.label);

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: () => context.push('/stocks/${t.symbol}'),
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            color: bg,
            borderRadius: BorderRadius.circular(12),
            border: Border(left: BorderSide(color: accent, width: 3)),
          ),
          child: Row(
            children: [
              SizedBox(
                width: 52,
                child: Column(
                  children: [
                    Text(
                      label,
                      style: TextStyle(fontSize: 10, fontWeight: FontWeight.w800, color: accent),
                      textAlign: TextAlign.center,
                    ),
                    if (t.isAggregated)
                      Text(
                        'Gom lô',
                        style: TextStyle(fontSize: 9, color: scheme.onSurfaceVariant),
                      ),
                  ],
                ),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(t.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                    Text(
                      '${_formatVolume(t.volume)} CP · ${_formatValue(t.valueVnd)} · ${formatApiDateTime(t.at)}',
                      style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                    ),
                    if (t.sessionForeignNet != 0)
                      Text(
                        'NN phiên ${_formatNet(t.sessionForeignNet)} CP'
                        '${t.sessionPressure != 0 ? ' · Áp lực ${t.sessionPressure > 0 ? '+' : ''}${t.sessionPressure.toStringAsFixed(1)}' : ''}',
                        style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                      ),
                  ],
                ),
              ),
              Text(
                formatPrice(t.price),
                style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _formatVolume(int v) {
    if (v >= 1000000) return '${(v / 1000000).toStringAsFixed(2)}M';
    if (v >= 1000) return '${(v / 1000).toStringAsFixed(1)}K';
    return v.toString();
  }

  String _formatValue(double vnd) {
    if (vnd >= 1000000000) return '${(vnd / 1000000000).toStringAsFixed(2)} tỷ';
    if (vnd >= 1000000) return '${(vnd / 1000000).toStringAsFixed(0)} tr';
    return '${vnd.toStringAsFixed(0)}đ';
  }

  String _formatNet(int vol) {
    final sign = vol > 0 ? '+' : '';
    if (vol.abs() >= 1000000) return '$sign${(vol / 1000000).toStringAsFixed(1)}M';
    if (vol.abs() >= 1000) return '$sign${(vol / 1000).toStringAsFixed(0)}K';
    return '$sign$vol';
  }
}
