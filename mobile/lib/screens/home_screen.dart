import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/time/api_date.dart';
import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/market_hub_service.dart';
import '../core/theme/app_colors.dart';
import '../core/labels/trade_state_labels.dart';
import '../widgets/chart_widgets.dart';
import '../widgets/glass_card.dart';
import '../widgets/live_quote.dart';
import '../widgets/score_pill.dart';
import '../widgets/stock_search_bar.dart';
import '../widgets/trade_state_badge.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  ApiClient get _api => context.read<ApiClient>();
  MarketHubService get _hub => context.read<MarketHubService>();

  OpportunitiesList? _opportunities;
  RadarLiveSnapshot? _radarSnapshot;
  Map<String, List<double>> _sparklines = {};
  var _loading = true;
  String? _error;
  var _analysisRunning = false;
  String? _analysisError;
  String? _analysisSuccess;
  Timer? _cooldownTimer;

  @override
  void initState() {
    super.initState();
    _cooldownTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted && _inCooldown) setState(() {});
    });
    _load();
  }

  @override
  void dispose() {
    _cooldownTimer?.cancel();
    super.dispose();
  }

  bool get _inCooldown {
    final at = _opportunities?.analysisAvailableAt;
    if (at == null) return false;
    final until = parseApiDateUtc(at);
    return DateTime.now().toUtc().isBefore(until);
  }

  String? get _cooldownHint {
    final at = _opportunities?.analysisAvailableAt;
    if (at == null || !_inCooldown) return null;
    final until = parseApiDateUtc(at);
    final diff = until.difference(DateTime.now().toUtc());
    if (diff.isNegative) return null;
    final m = diff.inMinutes;
    final s = diff.inSeconds % 60;
    return m > 0 ? '${m}p ${s}s' : '${s}s';
  }

  String? _lastScanLabel(OpportunitiesList? opps) {
    final generated = opps?.generatedAt;
    if (generated == null) return null;
    return 'Lần quét cuối: ${formatApiDateTime(generated)}';
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _api.getOpportunities(),
        _api.getRadarLive(),
      ]);
      final opps = results[0] as OpportunitiesList;
      final radar = results[1] as RadarLiveSnapshot;
      final symbols = {
        ...opps.items.map((o) => o.symbol),
        ...radar.items.map((i) => i.symbol),
      }.toList();
      _hub.subscribeSymbols(symbols);
      Map<String, List<double>> sparks = {};
      if (symbols.isNotEmpty) {
        try {
          final series = await _api.getSparklines(symbols);
          sparks = {for (final s in series) s.symbol: s.closes};
        } catch (_) {}
      }
      setState(() {
        _opportunities = opps;
        _radarSnapshot = radar;
        _sparklines = sparks;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Không thể tải dữ liệu. Hãy chạy backend trước.');
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _runAnalysis() async {
    if (_analysisRunning || _inCooldown) return;
    setState(() {
      _analysisRunning = true;
      _analysisError = null;
      _analysisSuccess = null;
    });
    try {
      final result = await _api.runOpportunityAnalysis();
      await _load();
      setState(() {
        _analysisSuccess = result.opportunitiesSaved > 0
            ? 'Phân tích xong: ${result.opportunitiesSaved} mã trong top (quét ${result.stocksScored} mã).'
            : 'Phân tích xong: không có mã đạt SmartMoney (quét ${result.stocksScored} mã).';
      });
    } on ApiException catch (e) {
      setState(() => _analysisError = e.message);
      await _load();
    } catch (_) {
      setState(() => _analysisError = 'Không chạy được phân tích. Kiểm tra backend và thử lại.');
      await _load();
    } finally {
      setState(() => _analysisRunning = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final opps = _opportunities;
    final lastScan = _lastScanLabel(opps);
    final canPress =
        !_analysisRunning && !_inCooldown && (opps?.canRunAnalysis ?? true);
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return GestureDetector(
      onTap: () => FocusScope.of(context).unfocus(),
      behavior: HitTestBehavior.translucent,
      child: RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: EdgeInsets.fromLTRB(16, 12, 16, 96 + bottomInset),
        children: [
          const StockSearchBar(),
          const SizedBox(height: 12),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else ...[
            if (_error != null && !_loading) ...[
              ErrorBanner(message: _error!, onRetry: _load),
              const SizedBox(height: 12),
            ],
            GlassCard(
              wave: true,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SectionTitle('Cơ hội tốt nhất', subtitle: lastScan),
                  const SizedBox(height: 10),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: OutlinedButton(
                      onPressed: canPress ? _runAnalysis : null,
                      style: OutlinedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                        visualDensity: VisualDensity.compact,
                      ),
                      child: Text(
                        _analysisRunning
                            ? 'Đang phân tích...'
                            : _inCooldown && _cooldownHint != null
                                ? 'Chạy lại sau $_cooldownHint'
                                : opps?.hasFreshData == true
                                    ? 'Chạy lại phân tích'
                                    : 'Chạy phân tích',
                        style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
                      ),
                    ),
                  ),
                  if (_inCooldown && _cooldownHint != null && !_analysisRunning) ...[
                    const SizedBox(height: 8),
                    Text('Phân tích gần đây — chờ thêm $_cooldownHint để chạy lại.', style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                  ],
                  if (_analysisSuccess != null) ...[
                    const SizedBox(height: 8),
                    Text(_analysisSuccess!, style: TextStyle(fontSize: 13, color: Theme.of(context).colorScheme.primary)),
                  ],
                  if (_analysisError != null) ...[
                    const SizedBox(height: 8),
                    Text(_analysisError!, style: TextStyle(fontSize: 13, color: Theme.of(context).colorScheme.error)),
                  ],
                  const SizedBox(height: 12),
                  if (opps?.statusMessage != null && opps!.statusMessage!.isNotEmpty) ...[
                    Text(
                      opps.statusMessage!,
                      style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant),
                    ),
                    const SizedBox(height: 8),
                  ],
                  if ((opps?.items ?? []).isEmpty)
                    Text('Chưa có cơ hội.', style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant))
                  else
                    ...opps!.items.asMap().entries.map((e) => Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: _oppTile(e.value, e.key + 1),
                        )),
                ],
              ),
            ),
            const SizedBox(height: 16),
            GlassCard(
              wave: true,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const SectionTitle('Tín hiệu mới nhất'),
                  if (_radarSnapshot != null) ...[
                    const SizedBox(height: 4),
                    _SessionRadarStatusLine(snapshot: _radarSnapshot!),
                  ],
                  const SizedBox(height: 12),
                  if (_radarSnapshot == null || _radarSnapshot!.items.isEmpty)
                    Text(
                      'Chưa có mã đột biến trong phiên (|±3%|, KL≥1M).',
                      style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
                    )
                  else
                    ..._radarSnapshot!.items.take(8).map(_radarTile),
                ],
              ),
            ),
          ],
        ],
      ),
      ),
    );
  }

  Widget _oppTile(Opportunity o, int rank) {
    final scheme = Theme.of(context).colorScheme;
    final sparks = _sparklines[o.symbol] ?? const [];
    return SurfaceRow(
      onTap: () => context.push('/stocks/${o.symbol}'),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 18,
            child: Text('$rank', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: scheme.onSurfaceVariant)),
          ),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(o.symbol, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                const SizedBox(height: 4),
                Wrap(
                  spacing: 4,
                  runSpacing: 4,
                  children: [
                    ScorePill(o.score),
                    PredictedHitPill(percent: o.predictedHitPercent, sampleCount: o.predictedSampleCount),
                  ],
                ),
                if (o.setupDna != null && o.setupDna!.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(o.setupDna!, maxLines: 2, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 9, color: scheme.onSurfaceVariant, height: 1.3)),
                ],
                const SizedBox(height: 4),
                Wrap(
                  spacing: 4,
                  children: [
                    TradeStateBadge(
                      trade: resolveOpportunityTradeState(o),
                      showReason: true,
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 6),
          SparklineMini(closes: sparks, fallbackChange: o.changePercent),
          const SizedBox(width: 6),
          LiveQuoteColumn(symbol: o.symbol, fallbackPrice: o.price, fallbackChange: o.changePercent),
        ],
      ),
    );
  }

  Widget _radarTile(RadarLiveItem item) {
    final scheme = Theme.of(context).colorScheme;
    final isUp = item.changePercent >= 0;
    final tint = isUp ? AppColors.positiveDim(context) : AppColors.negativeDim(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () => context.push('/stocks/${item.symbol}'),
          borderRadius: BorderRadius.circular(12),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: tint,
              borderRadius: BorderRadius.circular(12),
              border: Border(
                left: BorderSide(color: isUp ? scheme.primary : scheme.error, width: 3),
              ),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Text(item.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                          if (item.sector.isNotEmpty) ...[
                            const SizedBox(width: 6),
                            Flexible(
                              child: Text(
                                item.sector,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                              ),
                            ),
                          ],
                        ],
                      ),
                      if (item.name.isNotEmpty)
                        Text(
                          item.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                        ),
                      if (item.signals.isNotEmpty) ...[
                        const SizedBox(height: 4),
                        Wrap(
                          spacing: 4,
                          runSpacing: 4,
                          children: item.signals
                              .map(
                                (s) => Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                  decoration: BoxDecoration(
                                    color: scheme.surfaceContainerHighest,
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  child: Text(
                                    _signalLabelVi(s),
                                    style: const TextStyle(fontSize: 9, fontWeight: FontWeight.w600),
                                  ),
                                ),
                              )
                              .toList(),
                        ),
                      ],
                      const SizedBox(height: 4),
                      Text(
                        'KL ${item.volumeRatio.toStringAsFixed(1)}×'
                        '${item.relativeStrength != 0 ? ' · RS ${item.relativeStrength > 0 ? '+' : ''}${item.relativeStrength.toStringAsFixed(1)}%' : ''}',
                        style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    LiveQuoteColumn(
                      symbol: item.symbol,
                      fallbackPrice: item.price,
                      fallbackChange: item.changePercent,
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

  String _signalLabelVi(String type) {
    const map = {
      'Breakout': 'Vượt đỉnh',
      'DarvasBreakout': 'Phá vỡ nền giá',
      'VolumeSpike': 'Bùng nổ khối lượng',
      'Accumulation': 'Tích lũy',
      'Shakeout': 'Rũ hàng',
      'Distribution': 'Phân phối',
      'RelativeStrength': 'Mạnh hơn thị trường',
    };
    return map[type] ?? type;
  }
}

class _SessionRadarStatusLine extends StatelessWidget {
  const _SessionRadarStatusLine({required this.snapshot});

  final RadarLiveSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final scanTime = snapshot.scannedAt.isNotEmpty ? formatApiDateTime(snapshot.scannedAt) : '—';
    final detail = snapshot.matchCount > 0
        ? '${snapshot.matchCount} mã đột biến (|±3%|, KL≥1M)'
        : '0 mã đột biến';

    return Row(
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(shape: BoxShape.circle, color: scheme.primary),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            'SessionRadar · Quét gần nhất: $scanTime · $detail',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
        ),
      ],
    );
  }
}
