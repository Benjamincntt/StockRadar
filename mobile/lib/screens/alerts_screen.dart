import 'package:flutter/material.dart';

import 'package:go_router/go_router.dart';

import 'package:provider/provider.dart';



import '../core/api/api_client.dart';

import '../core/models/models.dart';

import '../core/services/market_hub_service.dart';

import '../core/theme/app_colors.dart';

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



  var _category = 'Tất cả';

  List<AlertItem> _alerts = [];

  IntradayMonitorStatus? _monitor;

  var _loading = true;

  String? _error;



  static const _filters = ['Tất cả', 'Tăng', 'Giảm'];

  static const _apiCategories = {'Tất cả': 'All', 'Tăng': 'Buy', 'Giảm': 'Sell'};



  @override

  void initState() {

    super.initState();

    _hub.addListener(_onLiveAlert);

    _load();

  }



  @override

  void dispose() {

    _hub.removeListener(_onLiveAlert);

    super.dispose();

  }



  void _onLiveAlert() {

    if (!mounted || _category != 'Tất cả') return;

    final live = _hub.recentAlerts;

    if (live.isEmpty) return;

    setState(() {

      final ids = _alerts.map((a) => a.id).toSet();

      final merged = [...live.where((a) => !ids.contains(a.id)), ..._alerts];

      _alerts = merged.take(50).toList();

    });

  }



  Future<void> _load() async {

    setState(() {

      _loading = true;

      _error = null;

    });

    try {

      final results = await Future.wait([

        _api.getAlerts(category: _apiCategories[_category] ?? 'All'),

        _api.getIntradayMonitorStatus(),

      ]);

      setState(() {

        _alerts = results[0] as List<AlertItem>;

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

            title: 'Lệnh realtime',

            subtitle: 'Khối ngoại · tự doanh · thỏa thuận',

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

            selected: _category,

            onSelected: (v) {

              setState(() => _category = v);

              _load();

            },

          ),

          const SizedBox(height: 12),

          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),

          if (_alerts.isEmpty)

            GlassCard(child: Text('Chưa có lệnh realtime.', style: TextStyle(color: scheme.onSurfaceVariant)))

          else

            ..._alerts.map((a) => Padding(

                  padding: const EdgeInsets.only(bottom: 8),

                  child: _alertRow(a),

                )),

        ],

      ),

    );

  }



  Widget _alertRow(AlertItem a) {

    final scheme = Theme.of(context).colorScheme;

    final isBuy = a.isBuy;

    final bg = a.isMaster

        ? AppColors.positiveDim(context)

        : (isBuy ? AppColors.positiveDim(context) : AppColors.negativeDim(context));

    return Material(

      color: Colors.transparent,

      child: InkWell(

        onTap: () => context.push('/stocks/${a.symbol}'),

        borderRadius: BorderRadius.circular(12),

        child: Container(

          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),

          decoration: BoxDecoration(

            color: bg,

            borderRadius: BorderRadius.circular(12),

            border: Border(

              left: BorderSide(

                color: a.isMaster ? scheme.primary : (isBuy ? scheme.primary : scheme.error),

                width: a.isMaster ? 4 : 3,

              ),

            ),

          ),

          child: Column(

            crossAxisAlignment: CrossAxisAlignment.start,

            children: [

              Row(

                children: [

                  Text(a.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),

                  if (a.isMaster) ...[

                    const SizedBox(width: 6),

                    Container(

                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),

                      decoration: BoxDecoration(

                        color: scheme.primary.withValues(alpha: 0.2),

                        borderRadius: BorderRadius.circular(4),

                      ),

                      child: const Text('MASTER', style: TextStyle(fontSize: 9, fontWeight: FontWeight.bold)),

                    ),

                  ],

                  const Spacer(),

                  Text(formatAlertTime(a.createdAt), style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),

                ],

              ),

              Text(a.title, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),

              if (a.message.isNotEmpty)

                Text(a.message, maxLines: 3, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),

            ],

          ),

        ),

      ),

    );

  }

}


