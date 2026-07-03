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

  var _sideFilter = 'Tất cả';
  List<TradePrint> _trades = [];
  IntradayMonitorStatus? _monitor;
  var _loading = true;
  String? _error;

  static const _filters = ['Tất cả', 'Mua', 'Bán'];

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
    if (!mounted || _sideFilter != 'Tất cả') return;
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
        _api.getTradePrints(),
        _api.getIntradayMonitorStatus(),
      ]);
      var trades = results[0] as List<TradePrint>;
      if (_sideFilter == 'Mua') {
        trades = trades.where((t) => t.isBuy).toList();
      } else if (_sideFilter == 'Bán') {
        trades = trades.where((t) => !t.isBuy).toList();
      }
      setState(() {
        _trades = trades;
        _monitor = results[1] as IntradayMonitorStatus;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
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
            subtitle: 'Lệnh block lớn · ≥25K CP · ≥500M GTGD/phút',
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
            selected: _sideFilter,
            onSelected: (v) {
              setState(() => _sideFilter = v);
              _load();
            },
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          if (_trades.isEmpty)
            GlassCard(
              child: Text(
                'Chưa có lệnh block lớn trong phiên.',
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

  Widget _tradeRow(TradePrint t) {
    final scheme = Theme.of(context).colorScheme;
    final isBuy = t.isBuy;
    final bg = isBuy ? AppColors.positiveDim(context) : AppColors.negativeDim(context);
    final accent = isBuy ? scheme.primary : scheme.error;
    final label = isBuy ? 'MUA' : 'BÁN';

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
                width: 36,
                child: Text(
                  label,
                  style: TextStyle(fontSize: 11, fontWeight: FontWeight.w800, color: accent),
                ),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(t.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                    Text(
                      '${_formatVolume(t.volume)} CP · ${_formatValue(t.price, t.volume)} · ${formatApiDateTime(t.at)}',
                      style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
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

  String _formatValue(double price, int volume) {
    final vnd = price * 1000 * volume;
    if (vnd >= 1000000000) return '${(vnd / 1000000000).toStringAsFixed(2)} tỷ';
    if (vnd >= 1000000) return '${(vnd / 1000000).toStringAsFixed(0)} tr';
    return '${vnd.toStringAsFixed(0)}đ';
  }
}
