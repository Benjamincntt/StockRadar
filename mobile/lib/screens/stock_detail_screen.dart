import 'dart:async';

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
import '../core/time/api_date.dart';
import '../widgets/app_bottom_nav.dart';
import '../widgets/chart_widgets.dart';
import '../widgets/glass_card.dart';
import '../widgets/live_quote.dart';
import '../widgets/scenario_widgets.dart';
import '../widgets/score_pill.dart';
import '../widgets/wave_background.dart';

class StockDetailScreen extends StatefulWidget {
  const StockDetailScreen({super.key, required this.symbol});

  final String symbol;

  @override
  State<StockDetailScreen> createState() => _StockDetailScreenState();
}

class _StockDetailScreenState extends State<StockDetailScreen> {
  /// Cache chi tiết trong phiên: mở lại mã đã xem render ngay dữ liệu cũ,
  /// nền làm tươi bằng getStockDetail (endpoint nặng, prod có thể mất giây).
  static final Map<String, StockDetail> _detailCache = {};

  static const _detailTimeout = Duration(seconds: 20);

  ApiClient get _api => context.read<ApiClient>();
  MarketHubService get _hub => context.read<MarketHubService>();

  StockDetail? _detail;
  StockChart? _chart;
  KichBanTheoSymbol? _kichBan;
  var _loadingDetail = true;
  var _chartLoading = false;
  var _kichBanLoading = true;
  var _kichBanFailed = false;
  String? _error;
  var _interval = '1D';
  var _watchlistAdded = false;
  var _showIchimoku = true;

  @override
  void initState() {
    super.initState();
    _hub.subscribeSymbols([widget.symbol]);
    _load();
    _loadKichBan();
  }

  Future<void> _load({bool refresh = false}) async {
    // Có cache cho mã này → render tức thì thay vì quay LoadingView chờ endpoint nặng.
    if (_detail == null) {
      final cached = _detailCache[widget.symbol.toUpperCase()];
      if (cached != null) {
        _detail = cached;
        _loadingDetail = false;
        if (_interval == '1D' && cached.history.isNotEmpty) {
          _chart = StockChart(
            symbol: cached.symbol,
            interval: '1D',
            bars: chartBarsFromHistory(cached.history),
          );
          _chartLoading = false;
        }
      }
    }
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
      final detail = await _api.getStockDetail(widget.symbol).timeout(
        _detailTimeout,
        onTimeout: () => throw ApiException(
          'Máy chủ phản hồi chậm (quá ${_detailTimeout.inSeconds}s). Thử lại.',
        ),
      );
      if (!mounted) return;
      _detailCache[detail.symbol] = detail;

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

  /// Tải dữ liệu kịch bản V2 cho mã — lỗi tải không làm hỏng màn chi tiết.
  /// 404 (chưa có đánh giá) trả null; lỗi khác đánh dấu thất bại để UI nhắc kéo xuống tải lại.
  Future<void> _loadKichBan() async {
    try {
      final json = await _api.getKichBanTheoSymbol(widget.symbol);
      if (!mounted) return;
      setState(() {
        _kichBan = json == null ? null : KichBanTheoSymbol.fromJson(json);
        _kichBanFailed = false;
        _kichBanLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _kichBan = null;
        _kichBanFailed = true;
        _kichBanLoading = false;
      });
    }
  }

  /// Danh sách kịch bản đã sắp theo thứ tự ưu tiên hiển thị.
  List<KichBanChiTiet> get _dsKichBan => _kichBan?.danhSachUuTien ?? const [];

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
      final chart = await _api
          .getStockChart(widget.symbol, interval: _interval)
          .timeout(_detailTimeout);
      if (mounted) setState(() => _chart = chart);
    } on ApiException catch (_) {
      if (mounted) setState(() => _chart = null);
    } on TimeoutException catch (_) {
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

  void _openFullscreenChart(String name) {
    final live = context.read<MarketHubService>().quote(widget.symbol);
    Navigator.of(context, rootNavigator: true).push(
      MaterialPageRoute(
        builder: (_) => FullscreenChartPage(
          bars: _chart?.bars ?? const [],
          interval: _interval,
          symbol: widget.symbol,
          name: name,
          livePrice: live?.price,
          liveChangePercent: live?.changePercent,
          showIchimoku: _showIchimoku,
        ),
      ),
    );
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
                    IconButton(
                      tooltip: 'Sự kiện quyền',
                      onPressed: () => context.push('/stocks/${widget.symbol}/su-kien-quyen'),
                      icon: const Icon(Icons.event_note_outlined),
                    ),
                  ],
                ),
              ),
              if (_kichBan?.danhSach.isNotEmpty ?? false)
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: ScenarioStatusBadges(kichBan: _dsKichBan),
                ),
              Expanded(
                child: _growthPane(scheme, d, box),
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

  Widget _growthPane(ColorScheme scheme, StockDetail? d, Map<String, dynamic>? box) {
    return _loadingDetail
                    ? const LoadingView()
                    : RefreshIndicator(
                        onRefresh: () async {
                          await Future.wait([_load(refresh: true), _loadKichBan()]);
                        },
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                          children: [
                            if (_error != null && !_loadingDetail)
                              Padding(
                                padding: const EdgeInsets.only(bottom: 12),
                                child: ErrorBanner(message: _error!, onRetry: () => _load(refresh: true)),
                              ),
                            if (d != null) ...[
                              if (!d.isUniverseActive)
                                Padding(
                                  padding: const EdgeInsets.only(bottom: 12),
                                  child: ErrorBanner(
                                    message: 'Mã đã ngưng theo dõi'
                                        '${d.universeUpdatedAt != null ? ' từ ${formatApiDateVietnam(d.universeUpdatedAt!)}' : ''}'
                                        '${d.universeStatusReason?.isNotEmpty == true ? ' (${d.universeStatusReason})' : ''}'
                                        ' — giá dưới đây là giá đóng cửa phiên cuối, không phải giá hiện tại.',
                                  ),
                                ),
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
                                            label: 'Tỷ lệ khối lượng',
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
                                    Row(
                                      children: [
                                        Expanded(
                                          child: SectionTitle(
                                            'Biểu đồ giá & khối lượng',
                                            subtitle: (box?['periods'] as List?)?.isNotEmpty == true
                                                ? 'Khung Ngày — vùng tích lũy'
                                                : (_showIchimoku ? 'Ichimoku · Khối lượng' : 'MA10 / MA50 · Khối lượng'),
                                          ),
                                        ),
                                        _IndicatorToggleButton(
                                          icon: Icons.layers_outlined,
                                          active: _showIchimoku,
                                          tooltip: _showIchimoku ? 'Tắt Ichimoku' : 'Bật Ichimoku',
                                          onPressed: () => setState(() => _showIchimoku = !_showIchimoku),
                                        ),
                                        _IndicatorToggleButton(
                                          icon: Icons.fullscreen_outlined,
                                          active: false,
                                          tooltip: 'Toàn màn hình (xoay ngang)',
                                          onPressed: () => _openFullscreenChart(d.name),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 8),
                                    ChartTimeframeBar(value: _interval, onChanged: _changeInterval),
                                    if (_interval == '1D')
                                      Padding(
                                        padding: const EdgeInsets.fromLTRB(0, 6, 0, 0),
                                        child: Row(
                                          children: [
                                            _LegendDot(color: ChartColors.of(context).tran, label: 'Tăng trần'),
                                            const SizedBox(width: 12),
                                            _LegendDot(color: ChartColors.of(context).san, label: 'Giảm sàn'),
                                          ],
                                        ),
                                      ),
                                    const SizedBox(height: 8),
                                    Builder(
                                      builder: (context) {
                                        final live = context.watch<MarketHubService>().quote(widget.symbol);
                                        return PriceVolumeChart(
                                          key: ValueKey('$_interval-$_showIchimoku'),
                                          bars: _chart?.bars ?? const [],
                                          interval: _interval,
                                          symbol: widget.symbol,
                                          name: d.name,
                                          loading: _chartLoading,
                                          livePrice: live?.price,
                                          liveChangePercent: live?.changePercent,
                                          showIchimoku: _showIchimoku,
                                          height: 360,
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
                              // V2 — kế hoạch giao dịch: chỉ hiện khi kịch bản đã kích hoạt/đang giữ có kế hoạch.
                              for (final kb in _dsKichBan.where((k) => k.hienKeHoach))
                                _sectionCard(
                                  context,
                                  child: TradePlanCard(kichBan: kb),
                                ),
                              // V2 — bằng chứng kịch bản thay bảng điểm tiêu chí V1.
                              if (!_kichBanLoading && _dsKichBan.isNotEmpty)
                                _sectionCard(
                                  context,
                                  child: ScenarioEvidenceCard(kichBan: _dsKichBan),
                                ),
                              // Trạng thái trống: chưa có đánh giá (404) hoặc lỗi tải kịch bản.
                              if (!_kichBanLoading && _dsKichBan.isEmpty)
                                _sectionCard(
                                  context,
                                  child: ScenarioEmptyState(
                                    message: _kichBanFailed
                                        ? 'Không tải được dữ liệu kịch bản. Kéo xuống để tải lại.'
                                        : 'Chưa có đánh giá kịch bản. Dữ liệu sẽ cập nhật sau phiên giao dịch tiếp theo.',
                                  ),
                                ),
                            ],
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

class _LegendDot extends StatelessWidget {
  const _LegendDot({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 9,
          height: 9,
          decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(2)),
        ),
        const SizedBox(width: 4),
        Text(label, style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
      ],
    );
  }
}

class _IndicatorToggleButton extends StatelessWidget {
  const _IndicatorToggleButton({
    required this.icon,
    required this.active,
    required this.tooltip,
    required this.onPressed,
  });

  final IconData icon;
  final bool active;
  final String tooltip;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = active ? scheme.primary : scheme.onSurfaceVariant;
    return Padding(
      padding: const EdgeInsets.only(left: 2),
      child: IconButton(
        tooltip: tooltip,
        onPressed: onPressed,
        visualDensity: VisualDensity.compact,
        constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
        padding: EdgeInsets.zero,
        icon: Icon(icon, size: 20, color: color),
        style: IconButton.styleFrom(
          backgroundColor: active ? scheme.primary.withValues(alpha: 0.12) : Colors.transparent,
        ),
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
