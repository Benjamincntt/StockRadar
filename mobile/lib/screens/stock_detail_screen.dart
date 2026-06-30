import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class StockDetailScreen extends StatefulWidget {
  const StockDetailScreen({super.key, required this.symbol});

  final String symbol;

  @override
  State<StockDetailScreen> createState() => _StockDetailScreenState();
}

class _StockDetailScreenState extends State<StockDetailScreen> {
  ApiClient get _api => context.read<ApiClient>();
  StockDetail? _detail;
  StockChart? _chart;
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
      final results = await Future.wait([
        _api.getStockDetail(widget.symbol),
        _api.getStockChart(widget.symbol),
      ]);
      setState(() {
        _detail = results[0] as StockDetail;
        _chart = results[1] as StockChart;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _addWatchlist() async {
    final auth = context.read<AuthService>();
    if (!auth.isLoggedIn) {
      if (mounted) context.push('/login');
      return;
    }
    try {
      await _api.addToWatchlist(widget.symbol);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Đã thêm vào watchlist')));
      }
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final d = _detail;

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: () => context.pop()),
        title: Text(widget.symbol),
      ),
      floatingActionButton: d == null
          ? null
          : Padding(
              padding: const EdgeInsets.only(bottom: 72),
              child: FilledButton.icon(
                onPressed: _addWatchlist,
                icon: const Icon(Icons.star_outline),
                label: const Text('Thêm Watchlist'),
              ),
            ),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerFloat,
      body: _loading
          ? const LoadingView()
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 120),
                children: [
                  if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
                  if (d != null) ...[
                    GlassCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Text(d.name, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18)),
                              const Spacer(),
                              ScorePill(d.score),
                            ],
                          ),
                          Text(d.sector, style: TextStyle(color: scheme.onSurfaceVariant)),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Text(formatPrice(d.price), style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w700)),
                              const SizedBox(width: 12),
                              ChangePill(d.changePercent),
                            ],
                          ),
                          if (d.recommendation != null) ...[
                            const SizedBox(height: 8),
                            RecommendationBadge(d.recommendation),
                          ],
                          if (d.buyScore != null)
                            Padding(
                              padding: const EdgeInsets.only(top: 8),
                              child: Text('Buy score: ${d.buyScore!.toStringAsFixed(0)}'),
                            ),
                          if (d.predictedHitPercent != null)
                            Text('Predicted hit: ${d.predictedHitPercent!.toStringAsFixed(1)}%'),
                        ],
                      ),
                    ),
                    const SizedBox(height: 12),
                    if (_chart != null && _chart!.bars.isNotEmpty) _PriceChart(chart: _chart!),
                    const SizedBox(height: 12),
                    GlassCard(
                      child: Text(d.summary.isNotEmpty ? d.summary : 'Không có tóm tắt.'),
                    ),
                  ],
                ],
              ),
            ),
    );
  }
}

class _PriceChart extends StatelessWidget {
  const _PriceChart({required this.chart});

  final StockChart chart;

  @override
  Widget build(BuildContext context) {
    final bars = chart.bars;
    final spots = <FlSpot>[];
    for (var i = 0; i < bars.length; i++) {
      spots.add(FlSpot(i.toDouble(), bars[i].close));
    }
    final scheme = Theme.of(context).colorScheme;

    return GlassCard(
      child: SizedBox(
        height: 200,
        child: LineChart(
          LineChartData(
            gridData: const FlGridData(show: false),
            titlesData: const FlTitlesData(show: false),
            borderData: FlBorderData(show: false),
            lineBarsData: [
              LineChartBarData(
                spots: spots,
                isCurved: true,
                color: scheme.primary,
                barWidth: 2,
                dotData: const FlDotData(show: false),
                belowBarData: BarAreaData(
                  color: scheme.primary.withValues(alpha: 0.12),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
