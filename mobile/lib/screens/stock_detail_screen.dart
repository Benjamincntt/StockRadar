import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/labels/base_price_labels.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../core/services/market_hub_service.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../widgets/app_bottom_nav.dart';
import '../widgets/buy_decision_card.dart';
import '../widgets/chart_widgets.dart';
import '../widgets/glass_card.dart';
import '../widgets/live_quote.dart';
import '../widgets/score_pill.dart';
import '../widgets/stock_detail_widgets.dart';
import '../widgets/wave_background.dart';

class StockDetailScreen extends StatefulWidget {
  const StockDetailScreen({super.key, required this.symbol});

  final String symbol;

  @override
  State<StockDetailScreen> createState() => _StockDetailScreenState();
}

class _StockDetailScreenState extends State<StockDetailScreen> {
  ApiClient get _api => context.read<ApiClient>();
  MarketHubService get _hub => context.read<MarketHubService>();

  StockDetail? _detail;
  StockChart? _chart;
  var _loadingDetail = true;
  var _chartLoading = false;
  String? _error;
  var _interval = '1D';
  var _watchlistAdded = false;

  @override
  void initState() {
    super.initState();
    _hub.subscribeSymbols([widget.symbol]);
    _load();
  }

  Future<void> _load({bool refresh = false}) async {
    final firstLoad = _detail == null;
    setState(() {
      if (firstLoad) _loadingDetail = true;
      if (!refresh || firstLoad) _error = null;
      if (!firstLoad && _interval == '1D' && (_detail?.history.isNotEmpty ?? false)) {
        _chartLoading = false;
      } else if (!firstLoad) {
        _chartLoading = true;
      } else {
        _chartLoading = true;
      }
    });
    try {
      final detail = await _api.getStockDetail(widget.symbol);
      if (!mounted) return;

      StockChart? chart;
      var chartLoading = false;

      if (_interval == '1D' && detail.history.isNotEmpty) {
        chart = StockChart(
          symbol: detail.symbol,
          interval: '1D',
          bars: chartBarsFromHistory(detail.history),
        );
      } else {
        chartLoading = true;
      }

      setState(() {
        _detail = detail;
        _chart = chart;
        _loadingDetail = false;
        _chartLoading = chartLoading;
        _error = null;
      });

      if (chartLoading) {
        await _loadChartOnly();
      }
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.statusCode == 404
            ? 'Không tìm thấy mã ${widget.symbol} trên server.'
            : ApiClient.friendlyMessage(e.message, e.statusCode);
        _loadingDetail = false;
        _chartLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Không tải được chi tiết mã. Thử lại sau.';
        _loadingDetail = false;
        _chartLoading = false;
      });
    }
  }

  Future<void> _loadChartOnly() async {
    if (!mounted) return;
    setState(() => _chartLoading = true);
    try {
      if (_interval == '1D' && (_detail?.history.isNotEmpty ?? false)) {
        final detail = _detail!;
        if (mounted) {
          setState(() {
            _chart = StockChart(
              symbol: detail.symbol,
              interval: '1D',
              bars: chartBarsFromHistory(detail.history),
            );
          });
        }
        return;
      }
      final chart = await _api.getStockChart(widget.symbol, interval: _interval);
      if (mounted) setState(() => _chart = chart);
    } on ApiException catch (_) {
      if (mounted) setState(() => _chart = null);
    } finally {
      if (mounted) setState(() => _chartLoading = false);
    }
  }

  Future<void> _changeInterval(String iv) async {
    if (iv == _interval) return;
    setState(() {
      _interval = iv;
      _chartLoading = true;
      _chart = null;
    });
    await _loadChartOnly();
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
        setState(() => _watchlistAdded = true);
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
    final box = d?.flatBox;

    return Scaffold(
      backgroundColor: AppColors.darkBackground,
      body: WaveBackground(
        child: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(8, 4, 16, 8),
                child: Row(
                  children: [
                    IconButton(
                      onPressed: () {
                        if (context.canPop()) {
                          context.pop();
                        } else {
                          context.go('/');
                        }
                      },
                      icon: const Icon(Icons.chevron_left),
                      style: IconButton.styleFrom(
                        backgroundColor: AppColors.surfaceHigh(context),
                        shape: const CircleBorder(),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            widget.symbol,
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700),
                          ),
                          if (d != null)
                            Text(
                              d.name,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                            ),
                        ],
                      ),
                    ),
                    if (d != null) ScorePill(d.score),
                  ],
                ),
              ),
              Expanded(
                child: _loadingDetail
                    ? const LoadingView()
                    : RefreshIndicator(
                        onRefresh: () => _load(refresh: true),
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                          children: [
                            if (_error != null && !_loadingDetail)
                              Padding(
                                padding: const EdgeInsets.only(bottom: 12),
                                child: ErrorBanner(message: _error!, onRetry: () => _load(refresh: true)),
                              ),
                            if (d != null) ...[
                              _sectionCard(
                                context,
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(d.sector, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                                    const SizedBox(height: 12),
                                    Row(
                                      crossAxisAlignment: CrossAxisAlignment.end,
                                      children: [
                                        Expanded(
                                          child: LivePriceText(
                                            symbol: widget.symbol,
                                            fallbackPrice: d.price,
                                            style: dataFont(context, size: 30, weight: FontWeight.w700),
                                          ),
                                        ),
                                        LiveChangePill(symbol: widget.symbol, fallback: d.changePercent),
                                      ],
                                    ),
                                    const SizedBox(height: 16),
                                    Row(
                                      children: [
                                        Expanded(
                                          child: _MetricTile(
                                            label: 'Volume Ratio',
                                            value: '${d.volumeRatio.toStringAsFixed(2)}x',
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        Expanded(
                                          child: _MetricTile(
                                            label: 'RS',
                                            value: formatPercent(d.relativeStrength),
                                            valueColor: d.relativeStrength >= 0 ? scheme.primary : scheme.error,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                              _sectionCard(
                                context,
                                padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    SectionTitle(
                                      'Biểu đồ giá & khối lượng',
                                      subtitle: (box?['periods'] as List?)?.isNotEmpty == true
                                          ? 'Khung Ngày — vùng tích lũy'
                                          : 'MA10 / MA50 · Volume',
                                    ),
                                    const SizedBox(height: 8),
                                    ChartTimeframeBar(value: _interval, onChanged: _changeInterval),
                                    const SizedBox(height: 8),
                                    Builder(
                                      builder: (context) {
                                        final live = context.watch<MarketHubService>().quote(widget.symbol);
                                        return PriceVolumeChart(
                                          bars: _chart?.bars ?? const [],
                                          interval: _interval,
                                          symbol: widget.symbol,
                                          name: d.name,
                                          loading: _chartLoading,
                                          livePrice: live?.price,
                                          liveChangePercent: live?.changePercent,
                                        );
                                      },
                                    ),
                                    if (box != null &&
                                        (box['periods'] as List?)?.isNotEmpty == true &&
                                        _interval != '1D')
                                      Padding(
                                        padding: const EdgeInsets.only(top: 8),
                                        child: Text(
                                          'Chuyển khung D để xem vùng tích lũy trên biểu đồ',
                                          textAlign: TextAlign.center,
                                          style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                              if (box != null) ...[
                                _sectionCard(
                                  context,
                                  child: _FlatBoxCard(box: box, latestPrice: d.price),
                                ),
                              ],
                              if (d.buyDecision.swingDecision != null &&
                                  d.buyDecision.swingDecision!.headline.isNotEmpty)
                                _sectionCard(
                                  context,
                                  child: SwingDecisionCard(swing: d.buyDecision.swingDecision!),
                                ),
                              _sectionCard(
                                context,
                                child: TradeJournalCard(symbol: widget.symbol, buyDecision: d.buyDecision),
                              ),
                              BuyDecisionCard(decision: d.buyDecision),
                              _sectionCard(
                                context,
                                child: PriceLevelsCard(
                                  entry: d.entryPoint,
                                  buyZone: d.buyZone,
                                  stopLoss: d.stopLoss,
                                  resistance: d.resistance,
                                  target: d.target,
                                ),
                              ),
                              if (d.patternScores.isNotEmpty)
                                _sectionCard(
                                  context,
                                  child: AdvancedIndicatorsCard(scores: d.patternScores),
                                ),
                              _sectionCard(
                                context,
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const SectionTitle('Tóm tắt phân tích'),
                                    const SizedBox(height: 8),
                                    Text(
                                      d.summary.isNotEmpty ? d.summary : 'Không có tóm tắt.',
                                      style: TextStyle(fontSize: 13, height: 1.5, color: scheme.onSurfaceVariant),
                                    ),
                                  ],
                                ),
                              ),
                              if (d.activeSignals.isNotEmpty)
                                _sectionCard(
                                  context,
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      const SectionTitle('Các tín hiệu'),
                                      const SizedBox(height: 8),
                                      SignalChips(signals: d.activeSignals),
                                    ],
                                  ),
                                ),
                            ],
                          ],
                        ),
                      ),
              ),
            ],
          ),
        ),
      ),
      bottomNavigationBar: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (d != null)
            SafeArea(
              bottom: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
                child: FilledButton(
                  onPressed: _watchlistAdded ? null : _addWatchlist,
                  child: Text(_watchlistAdded ? 'Đã thêm Watchlist' : '+ Thêm vào Watchlist'),
                ),
              ),
            ),
          const AppBottomNav(currentIndex: -1),
        ],
      ),
    );
  }
}

Widget _sectionCard(BuildContext context, {required Widget child, EdgeInsetsGeometry? padding}) {
  return Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: GlassCard(solid: true, padding: padding, child: child),
  );
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({
    required this.label,
    required this.value,
    this.valueColor,
  });

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        children: [
          Text(
            label.toUpperCase(),
            style: TextStyle(
              fontSize: 9,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.6,
              color: scheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            value,
            style: dataFont(
              context,
              size: 13,
              weight: FontWeight.w700,
              color: valueColor ?? scheme.onSurface,
            ),
          ),
        ],
      ),
    );
  }
}

class _FlatBoxCard extends StatelessWidget {
  const _FlatBoxCard({required this.box, required this.latestPrice});

  final Map<String, dynamic> box;
  final double latestPrice;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final boxLow = (box['boxLow'] as num?)?.toDouble() ?? 0;
    final boxHigh = (box['boxHigh'] as num?)?.toDouble() ?? 0;
    final sessionDays = (box['sessionDays'] as num?)?.toInt() ?? 0;
    final confirmed = box['isBreakoutConfirmed'] as bool? ?? false;
    final refPeriod = box['refBoxPeriod'] as String? ?? '';
    final volMult = (box['volumeMultiplier'] as num?)?.toDouble();
    final priceGain = (box['priceGainPercent'] as num?)?.toDouble();
    final stopLoss = (box['suggestedStopLoss'] as num?)?.toDouble() ?? boxLow;
    final filterGain = (box['filterGainFromBoxTopPercent'] as num?)?.toDouble() ?? 0;
    final exceedsFilter = box['exceedsRunupFilter'] as bool? ?? false;
    final filterTop = (box['filterBoxTop'] as num?)?.toDouble() ?? boxHigh;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionTitle(
          BasePriceLabels.base,
          subtitle: BasePriceLabels.cardSubtitle(box, latestPrice),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: _MetricTile(
                label: 'Vùng nền',
                value: '${formatPrice(boxLow)} – ${formatPrice(boxHigh)}',
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MetricTile(
                label: 'Số phiên',
                value: '$sessionDays phiên',
                valueColor: scheme.primary,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MetricTile(
                label: confirmed ? 'KL / nền' : 'Cắt lỗ',
                value: confirmed && volMult != null
                    ? '×${volMult.toStringAsFixed(1)}'
                    : formatPrice(stopLoss),
                valueColor: scheme.onSurface,
              ),
            ),
          ],
        ),
        if (confirmed && priceGain != null) ...[
          const SizedBox(height: 8),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: scheme.primary.withValues(alpha: 0.3)),
              color: scheme.primary.withValues(alpha: 0.08),
            ),
            child: Text(
              'Phiên kích hoạt +${priceGain.toStringAsFixed(1)}%',
              textAlign: TextAlign.center,
              style: dataFont(context, size: 12, weight: FontWeight.w700, color: scheme.primary),
            ),
          ),
        ],
        const SizedBox(height: 8),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: scheme.outlineVariant),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Lọc FOMO: so với đỉnh nền ${formatPrice(filterTop)}',
                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
              ),
              Text(
                '${filterGain >= 0 ? '+' : ''}${filterGain.toStringAsFixed(2)}%',
                style: dataFont(
                  context,
                  size: 13,
                  weight: FontWeight.w700,
                  color: exceedsFilter
                      ? scheme.error
                      : filterGain > 0
                          ? scheme.primary
                          : scheme.onSurface,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
