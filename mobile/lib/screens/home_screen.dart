import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/models/vi_the_dang_giu.dart';
import '../core/services/app_services.dart';
import '../core/services/market_hub_service.dart';
import '../core/theme/app_theme.dart';
import '../widgets/chart_widgets.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';
import '../widgets/stock_search_bar.dart';
import '../widgets/vnindex_market_card.dart';
import 'nhat_ky_lenh_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> with WidgetsBindingObserver {
  /// Làm mới nền theo [vnIndexRefreshInterval] để thẻ VNINDEX và danh sách đang giữ không đứng im.
  Timer? _autoRefresh;

  ApiClient get _api => context.read<ApiClient>();
  MarketHubService get _hub => context.read<MarketHubService>();

  List<ViTheDangGiu>? _holdings;
  String? _holdingsError;
  Map<String, List<double>> _sparklines = {};
  var _loading = true;

  VnIndexChartSnapshot? _vnIndex;
  var _vnIndexLoading = false;
  var _vnIndexUsingCache = false;
  DateTime? _vnIndexReceivedAt;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
    _startAutoRefresh();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _autoRefresh?.cancel();
    super.dispose();
  }

  /// Không poll khi app ở background (pin + request vô ích); quay lại thì nạp ngay.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _load(silent: true);
      _startAutoRefresh();
    } else {
      _autoRefresh?.cancel();
    }
  }

  void _startAutoRefresh() {
    _autoRefresh?.cancel();
    _autoRefresh = Timer.periodic(
      vnIndexRefreshInterval,
      (_) => _load(silent: true),
    );
  }

  /// Tải thẻ VNINDEX. Lỗi thì giữ dữ liệu cũ và đánh dấu đang dùng snapshot.
  Future<void> _loadVnIndex() async {
    try {
      final next = await _api.getVnIndexChart(sessions: 90);
      if (!mounted) return;
      final prev = _vnIndex;
      final accept = next.isNewerOrSameAs(prev);
      setState(() {
        if (accept) {
          _vnIndex = next;
          _vnIndexReceivedAt = DateTime.now();
          _vnIndexUsingCache = false;
        } else {
          _vnIndexUsingCache = prev != null;
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _vnIndexUsingCache = _vnIndex != null);
    }
  }

  /// Tải danh sách vị thế V3 đang giữ và sparkline của các mã đó.
  /// Lỗi chỉ ảnh hưởng thẻ này: giữ danh sách cũ nếu có, không làm hỏng cả Home.
  Future<void> _loadHoldings({required bool silent}) async {
    try {
      final holdings = await _api.getViTheV3DangGiu();
      final symbols = holdings.map((h) => h.symbol).toList();
      _hub.subscribeSymbols(symbols);
      var sparks = <String, List<double>>{};
      if (symbols.isNotEmpty) {
        try {
          final series = await _api.getSparklines(symbols);
          sparks = {for (final s in series) s.symbol: s.closes};
        } catch (_) {}
      }
      if (!mounted) return;
      setState(() {
        _holdings = holdings;
        _sparklines = sparks;
        _holdingsError = null;
      });
    } on ApiException catch (e) {
      if (!mounted || (silent && _holdings != null)) return;
      setState(() => _holdingsError = e.message);
    } catch (_) {
      if (!mounted || (silent && _holdings != null)) return;
      setState(() => _holdingsError = 'Không thể tải danh sách đang giữ.');
    }
  }

  /// Bút lông: mở Nhật ký để sửa dữ liệu theo lệnh khớp thật.
  /// Đã có lệnh Bán gắn tín hiệu V3 của vị thế này thì mở sửa lệnh gần nhất đó;
  /// chưa có thì mở form ghi lệnh Bán điền sẵn (mã, tín hiệu V3, khối lượng đang giữ, giá chốt lời).
  Future<void> _openNhatKy(ViTheDangGiu h) async {
    if (!context.read<AuthService>().isLoggedIn) {
      context.push('/login');
      return;
    }
    final messenger = ScaffoldMessenger.of(context);
    NhatKyLenh? existing;
    try {
      final ds = await _api.getNhatKyLenh(page: 1, size: 200);
      final lenh = (ds['items'] as List<dynamic>? ?? [])
          .map((e) => NhatKyLenh.fromJson(e as Map<String, dynamic>))
          .where((l) =>
              l.loaiLenh == 'Ban' &&
              l.symbol.toUpperCase() == h.symbol &&
              (l.nguonTinHieu ?? '').toUpperCase() == 'V3' &&
              h.tinHieuId != null &&
              l.maTinHieu == h.tinHieuId)
          .toList()
        ..sort((a, b) => b.thoiDiemKhop.compareTo(a.thoiDiemKhop));
      existing = lenh.isEmpty ? null : lenh.first;
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
      return;
    } catch (_) {
      messenger.showSnackBar(
          const SnackBar(content: Text('Không tải được Nhật ký. Thử lại sau.')));
      return;
    }
    if (!mounted) return;

    final saved = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => NhatKyLenhScreen(
          existing: existing,
          prefill: existing != null
              ? null
              : NhatKyLenhPrefill(
                  symbol: h.symbol,
                  loaiLenh: 'Ban',
                  nguonTinHieu: h.tinHieuId == null ? null : 'V3',
                  maTinHieu: h.tinHieuId,
                  giaKhop: h.giaChotLoi,
                  khoiLuong: h.khoiLuong,
                ),
        ),
      ),
    );
    if (saved == true && mounted) {
      messenger.showSnackBar(const SnackBar(
        content: Text('Đã lưu. Danh sách Đang giữ cập nhật sau lần đối chiếu Nhật ký lúc 16:00.'),
      ));
      await _loadHoldings(silent: false);
    }
  }

  /// [silent] = refresh nền: không hiện spinner.
  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = true;
        _vnIndexLoading = _vnIndex == null;
      });
    }
    // Hai nguồn độc lập: nguồn nào lỗi cũng không kéo nguồn kia xuống.
    await Future.wait([
      _loadVnIndex(),
      _loadHoldings(silent: silent),
    ]);
    if (!mounted) return;
    setState(() {
      _loading = false;
      _vnIndexLoading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
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
            if (_loading && _vnIndex == null && _holdings == null)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 48),
                child: Center(child: CircularProgressIndicator()),
              )
            else ...[
              VnIndexMarketCard(
                snapshot: _vnIndex,
                loading: _vnIndexLoading && _vnIndex == null,
                usingCachedSnapshot: _vnIndexUsingCache,
                receivedAt: _vnIndexReceivedAt,
              ),
              const SizedBox(height: 16),
              _buildHoldingsCard(context),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildHoldingsCard(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final holdings = _holdings;

    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SectionTitle(
            holdings == null || holdings.isEmpty
                ? 'Đang giữ'
                : 'Đang giữ (${holdings.length})',
          ),
          const SizedBox(height: 12),
          if (_holdingsError != null && holdings == null)
            ErrorBanner(
              message: _holdingsError!,
              onRetry: () => _loadHoldings(silent: false),
            )
          else if (holdings == null || holdings.isEmpty)
            Text(
              'Chưa giữ mã nào.',
              style: TextStyle(color: scheme.onSurfaceVariant),
            )
          else
            ...holdings.map((h) => Padding(
                  padding: const EdgeInsets.only(bottom: 6),
                  child: _ViTheTile(
                    viThe: h,
                    sparkline: _sparklines[h.symbol] ?? const [],
                    onTap: () => context.push('/stocks/${h.symbol}'),
                    onEdit: () => _openNhatKy(h),
                  ),
                )),
        ],
      ),
    );
  }
}

/// Một dòng vị thế đang giữ: giá mua, giá hiện tại (trực tiếp), % lãi/lỗ so giá mua, giá chốt lời, số phiên còn lại.
class _ViTheTile extends StatelessWidget {
  const _ViTheTile({
    required this.viThe,
    required this.sparkline,
    required this.onTap,
    required this.onEdit,
  });

  final ViTheDangGiu viThe;
  final List<double> sparkline;
  final VoidCallback onTap;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final live = context.watch<MarketHubService>().quote(viThe.symbol);
    final giaHienTai = live?.price;
    final laiLo = viThe.laiLoPhanTram(giaHienTai);
    final subStyle = TextStyle(fontSize: 11, height: 1.25, color: scheme.onSurfaceVariant);

    final giaChotLoi = viThe.banTheoLich
        ? 'Bán ATO theo lịch'
        : (viThe.giaChotLoi != null ? 'Chốt lời ${formatPrice(viThe.giaChotLoi!)}' : null);
    final conLai = viThe.soPhienConLai != null ? 'Còn ${viThe.soPhienConLai} phiên' : null;
    final dongPhu = [
      if (viThe.giaVao != null) 'Mua ${formatPrice(viThe.giaVao!)}',
      if (giaChotLoi != null) giaChotLoi,
      if (conLai != null) conLai,
    ].join(' · ');

    return SurfaceRow(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text(
                      viThe.symbol,
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      viThe.loai,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: scheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
                if (dongPhu.isNotEmpty) ...[
                  const SizedBox(height: 3),
                  Text(dongPhu, maxLines: 2, overflow: TextOverflow.ellipsis, style: subStyle),
                ],
              ],
            ),
          ),
          if (sparkline.length >= 2) ...[
            SparklineMini(
              closes: sparkline,
              fallbackChange: laiLo ?? 0,
              width: 48,
              height: 24,
            ),
            const SizedBox(width: 8),
          ],
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                giaHienTai != null ? formatPrice(giaHienTai) : '—',
                style: dataFont(context, weight: FontWeight.w700),
              ),
              if (laiLo != null) ChangePill(laiLo),
            ],
          ),
          IconButton(
            tooltip: 'Sửa trong Nhật ký',
            visualDensity: VisualDensity.compact,
            onPressed: onEdit,
            icon: Icon(Icons.draw_outlined, size: 20, color: scheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }
}
