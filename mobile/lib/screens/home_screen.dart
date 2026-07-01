import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/market_hub_service.dart';
import '../core/theme/app_colors.dart';
import '../widgets/chart_widgets.dart';
import '../widgets/glass_card.dart';
import '../widgets/live_quote.dart';
import '../widgets/score_pill.dart';
import '../widgets/stock_search_bar.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  ApiClient get _api => context.read<ApiClient>();
  MarketHubService get _hub => context.read<MarketHubService>();

  OpportunitiesList? _opportunities;
  List<AlertItem> _universeAlerts = [];
  IntradayMonitorStatus? _monitor;
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
    final until = DateTime.tryParse(at);
    if (until == null) return false;
    return DateTime.now().isBefore(until);
  }

  String? get _cooldownHint {
    final at = _opportunities?.analysisAvailableAt;
    if (at == null || !_inCooldown) return null;
    final until = DateTime.tryParse(at);
    if (until == null) return null;
    final diff = until.difference(DateTime.now());
    if (diff.isNegative) return null;
    final m = diff.inMinutes;
    final s = diff.inSeconds % 60;
    return m > 0 ? '${m}p ${s}s' : '${s}s';
  }

  String? _lastScanLabel(OpportunitiesList? opps) {
    final generated = opps?.generatedAt;
    if (generated == null) return null;
    final dt = DateTime.tryParse(generated);
    if (dt == null) return null;
    final d = dt.day.toString().padLeft(2, '0');
    final m = dt.month.toString().padLeft(2, '0');
    final h = dt.hour.toString().padLeft(2, '0');
    final min = dt.minute.toString().padLeft(2, '0');
    return 'Lần quét cuối: $d/$m/${dt.year} $h:$min';
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _api.getOpportunities(),
        _api.getUniverseAlerts(),
        _api.getIntradayMonitorStatus(),
      ]);
      final opps = results[0] as OpportunitiesList;
      final symbols = opps.items.map((o) => o.symbol).toList();
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
        _universeAlerts = results[1] as List<AlertItem>;
        _monitor = results[2] as IntradayMonitorStatus;
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
    final canPress = !_analysisRunning && !_inCooldown;
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
            if (_monitor != null) _MonitorLine(status: _monitor!),
            const SizedBox(height: 12),
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
                  const SizedBox(height: 12),
                  if (_universeAlerts.isEmpty)
                    Text('Chưa có tín hiệu.', style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant))
                  else
                    ..._universeAlerts.take(8).map(_alertTile),
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
                    RecommendationBadge(o.recommendation),
                    if (o.entryPointStatus != null && o.entryPointStatus!.isNotEmpty)
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                        decoration: BoxDecoration(color: AppColors.positiveDim(context), borderRadius: BorderRadius.circular(6)),
                        child: Text(o.entryPointStatus!, style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w700)),
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

  Widget _alertTile(AlertItem a) {
    final scheme = Theme.of(context).colorScheme;
    final isBuy = a.isBuy;
    final tint = a.isMaster
        ? AppColors.positiveDim(context)
        : (isBuy ? AppColors.positiveDim(context) : AppColors.negativeDim(context));
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: () => context.push('/stocks/${a.symbol}'),
          borderRadius: BorderRadius.circular(12),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: tint,
              borderRadius: BorderRadius.circular(12),
              border: Border(
                left: BorderSide(
                  color: a.isMaster ? scheme.primary : (isBuy ? scheme.primary : scheme.error),
                  width: a.isMaster ? 4 : 3,
                ),
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
                        Text(a.message, maxLines: 2, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _MonitorLine extends StatelessWidget {
  const _MonitorLine({required this.status});

  final IntradayMonitorStatus status;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GlassCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: status.isStale ? scheme.error : scheme.primary,
            ),
          ),
          const SizedBox(width: 8),
          Icon(
            status.marketOpen ? Icons.radar : Icons.radar_outlined,
            size: 18,
            color: status.isStale ? scheme.error : scheme.primary,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              status.status.isNotEmpty ? status.status : 'Quét lệnh đột biến',
              style: const TextStyle(fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }
}
