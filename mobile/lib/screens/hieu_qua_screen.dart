import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/theme/app_colors.dart';
import '../core/time/api_date.dart';
import '../core/utils/v3_parsers.dart';
import '../widgets/glass_card.dart';
import '../widgets/scenario_widgets.dart';
import '../widgets/score_pill.dart';

/// Màn "Hiệu quả" V3 — đo lường kết quả bộ kịch bản P1++ (bán realtime).
///
/// Dữ liệu lấy từ 2 endpoint (spec 012, Phụ lục H):
/// - `/v3/hieu-qua/tom-tat?tu=&den=` → khối danh mục + thống kê theo kịch bản.
/// - `/v3/hieu-qua/lich-su?page=&size=&status=` → danh sách vị thế (phân trang).
class HieuQuaScreen extends StatefulWidget {
  const HieuQuaScreen({super.key});

  @override
  State<HieuQuaScreen> createState() => _HieuQuaScreenState();
}

class _HieuQuaScreenState extends State<HieuQuaScreen> {
  static const _pageSize = 20;

  ApiClient get _api => context.read<ApiClient>();

  // Bộ lọc kỳ: week/month/quarter/all — quy đổi sang mốc từ–đén phía server.
  String _period = 'month';

  _TomTat? _tomTat;
  final List<_ViThe> _viThe = [];
  int _totalCount = 0;
  int _page = 1;

  var _loading = true;
  var _loadingMore = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  bool get _hasMore => _viThe.length < _totalCount;

  /// Quy đổi bộ lọc kỳ sang cặp {tu, den} dạng YYYY-MM-DD (bỏ trống cho 'all').
  Map<String, String> _rangeQuery() {
    if (_period == 'all') return const {};
    final now = DateTime.now().toUtc();
    final tu = switch (_period) {
      'week' => now.subtract(const Duration(days: 7)),
      'quarter' => DateTime(now.year, now.month - 3, now.day),
      _ => DateTime(now.year, now.month - 1, now.day),
    };
    String iso(DateTime d) =>
        '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
    return {'tu': iso(tu), 'den': iso(now)};
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _page = 1;
    });
    try {
      final range = _rangeQuery();
      final results = await Future.wait([
        _api.getV3HieuQuaTomTat(tu: range['tu'], den: range['den']),
        _api.getV3HieuQuaLichSu(page: 1, size: _pageSize),
      ]);
      if (!mounted) return;
      final tomTat = _TomTat.fromJson(results[0]);
      final lichSu = results[1];
      final items = (lichSu['items'] as List<dynamic>? ?? [])
          .map((e) => _ViThe.fromJson(e as Map<String, dynamic>))
          .toList();
      setState(() {
        _tomTat = tomTat;
        _viThe
          ..clear()
          ..addAll(items);
        _totalCount = (lichSu['total'] as num?)?.toInt() ?? items.length;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'Không tải được dữ liệu hiệu quả V3. Thử lại sau.';
        _loading = false;
      });
    }
  }

  Future<void> _loadMore() async {
    if (_loadingMore || !_hasMore) return;
    setState(() => _loadingMore = true);
    try {
      final nextPage = _page + 1;
      final json =
          await _api.getV3HieuQuaLichSu(page: nextPage, size: _pageSize);
      if (!mounted) return;
      final items = (json['items'] as List<dynamic>? ?? [])
          .map((e) => _ViThe.fromJson(e as Map<String, dynamic>))
          .toList();
      setState(() {
        _viThe.addAll(items);
        _totalCount = (json['total'] as num?)?.toInt() ?? _totalCount;
        _page = nextPage;
        _loadingMore = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingMore = false);
    }
  }

  void _onPeriodChanged(String period) {
    if (period == _period) return;
    setState(() => _period = period);
    _load();
  }

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.fromLTRB(16, 12, 16, 96 + bottomInset),
        children: [
          _PeriodSelector(selected: _period, onChanged: _onPeriodChanged),
          const SizedBox(height: 16),
          if (_loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 64),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (_error != null)
            ErrorBanner(message: _error!, onRetry: _load)
          else ...[
            _DanhMucCards(danhMuc: _tomTat?.danhMuc),
            const SizedBox(height: 16),
            _ScenarioBreakdown(stats: _tomTat?.theoKichBan ?? const []),
            const SizedBox(height: 16),
            _TradeHistory(
              viThe: _viThe,
              hasMore: _hasMore,
              loadingMore: _loadingMore,
              onLoadMore: _loadMore,
            ),
          ],
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Bộ lọc kỳ
// ─────────────────────────────────────────────────────────────────────────────

class _PeriodSelector extends StatelessWidget {
  const _PeriodSelector({required this.selected, required this.onChanged});

  final String selected;
  final ValueChanged<String> onChanged;

  static const _options = <String, String>{
    'week': 'Tuần',
    'month': 'Tháng',
    'quarter': 'Quý',
    'all': 'Tất cả',
  };

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: _options.entries.map((e) {
          final active = e.key == selected;
          return Padding(
            padding: const EdgeInsets.only(right: 8),
            child: _PeriodChip(
              label: e.value,
              active: active,
              onTap: () => onChanged(e.key),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class _PeriodChip extends StatelessWidget {
  const _PeriodChip(
      {required this.label, required this.active, required this.onTap});

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(20),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          decoration: BoxDecoration(
            color: active
                ? scheme.primary.withValues(alpha: 0.16)
                : AppColors.surfaceLow(context),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(
              color: active
                  ? scheme.primary
                  : scheme.outline.withValues(alpha: 0.25),
            ),
          ),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 13,
              fontWeight: active ? FontWeight.w700 : FontWeight.w500,
              color: active ? scheme.primary : scheme.onSurfaceVariant,
            ),
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Khối danh mục V3
// ─────────────────────────────────────────────────────────────────────────────

class _DanhMucCards extends StatelessWidget {
  const _DanhMucCards({required this.danhMuc});

  final _DanhMuc? danhMuc;

  @override
  Widget build(BuildContext context) {
    final d = danhMuc;
    return Row(
      children: [
        Expanded(
          child: _MiniCard(
            value: _fmtTy(d?.giaTriHienTai),
            label: 'Giá trị DM',
            color: _KetQuaColor.ngang(context),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniCard(
            value: _fmtTy(d?.tienMat),
            label: 'Tiền mặt',
            color: _KetQuaColor.thang(context),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniCard(
            value: '${d?.soViTheDangGiu ?? 0}',
            label: 'Đang giữ',
            color: _KetQuaColor.rr(context),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniCard(
            value: '${(d?.sutGiamToiDaPhanTram ?? 0).toStringAsFixed(1)}%',
            label: 'Sụt giảm TBĐ',
            color: _KetQuaColor.thua(context),
          ),
        ),
      ],
    );
  }

  static String _fmtTy(double? nghinDong) {
    if (nghinDong == null) return '—';
    return '${(nghinDong / 1000000).toStringAsFixed(2)} tỷ';
  }
}

class _MiniCard extends StatelessWidget {
  const _MiniCard(
      {required this.value, required this.label, required this.color});

  final String value;
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GlassCard(
      padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 6),
      child: Column(
        children: [
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: color,
              height: 1.1,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w600,
              color: scheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Thống kê theo kịch bản V3
// ─────────────────────────────────────────────────────────────────────────────

class _ScenarioBreakdown extends StatelessWidget {
  const _ScenarioBreakdown({required this.stats});

  final List<_KichBanStats> stats;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SectionTitle('Theo kịch bản'),
          const SizedBox(height: 12),
          if (stats.isEmpty)
            Text(
              'Chưa có kịch bản nào được đo trong kỳ này.',
              style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
            )
          else
            ...stats.map((s) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _ScenarioRow(stats: s),
                )),
        ],
      ),
    );
  }
}

class _ScenarioRow extends StatelessWidget {
  const _ScenarioRow({required this.stats});

  final _KichBanStats stats;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final rate = (stats.tyLeThangPhanTram / 100).clamp(0.0, 1.0);
    final barColor = _rateColor(context, stats.tyLeThangPhanTram);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                stats.ten,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style:
                    const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
              ),
            ),
            const SizedBox(width: 8),
            Text(
              stats.soLenhDong > 0
                  ? '${stats.tyLeThangPhanTram.toStringAsFixed(0)}%'
                  : '—',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: barColor,
              ),
            ),
            const SizedBox(width: 8),
            Text(
              '(${stats.soThang}/${stats.soLenhDong})',
              style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
            ),
          ],
        ),
        const SizedBox(height: 6),
        ClipRRect(
          borderRadius: BorderRadius.circular(4),
          child: LinearProgressIndicator(
            value: rate,
            minHeight: 6,
            backgroundColor: scheme.surfaceContainerHighest,
            valueColor: AlwaysStoppedAnimation(barColor),
          ),
        ),
        const SizedBox(height: 4),
        Text(
          'Lãi TB ${_signed(stats.laiTBPhanTram)} · Lỗ TB ${_signed(stats.loTBPhanTram)}'
          ' · Tệ nhất ${_signed(stats.teNhatPhanTram)} · Giữ TB ${stats.giuTBPhien.toStringAsFixed(1)} phiên',
          style: TextStyle(fontSize: 10.5, color: scheme.onSurfaceVariant),
        ),
      ],
    );
  }

  static String _signed(double v) =>
      '${v >= 0 ? '+' : ''}${v.toStringAsFixed(1)}%';

  Color _rateColor(BuildContext context, double rate) {
    if (rate >= 60) return _KetQuaColor.thang(context);
    if (rate >= 40) return _KetQuaColor.choDo(context);
    return _KetQuaColor.thua(context);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Lịch sử vị thế V3
// ─────────────────────────────────────────────────────────────────────────────

class _TradeHistory extends StatelessWidget {
  const _TradeHistory({
    required this.viThe,
    required this.hasMore,
    required this.loadingMore,
    required this.onLoadMore,
  });

  final List<_ViThe> viThe;
  final bool hasMore;
  final bool loadingMore;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GlassCard(
      wave: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SectionTitle('Lịch sử vị thế'),
          const SizedBox(height: 12),
          if (viThe.isEmpty)
            Text(
              'Chưa có vị thế V3. Dữ liệu xuất hiện sau khi bộ kịch bản P1++ bắn tín hiệu và khớp lệnh.',
              style: TextStyle(
                  fontSize: 12, height: 1.4, color: scheme.onSurfaceVariant),
            )
          else ...[
            ...viThe.map((v) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: _TradeCard(viThe: v),
                )),
            if (hasMore)
              Center(
                child: loadingMore
                    ? const Padding(
                        padding: EdgeInsets.symmetric(vertical: 8),
                        child: SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                      )
                    : TextButton(
                        onPressed: onLoadMore,
                        style: TextButton.styleFrom(
                            foregroundColor: scheme.primary),
                        child: const Text('Xem thêm'),
                      ),
              ),
          ],
        ],
      ),
    );
  }
}

class _TradeCard extends StatelessWidget {
  const _TradeCard({required this.viThe});

  final _ViThe viThe;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final ketQuaColor = _KetQuaColor.of(context, viThe.ketQua);
    final phanTram = viThe.phanTramLoiNhuan;

    return SurfaceRow(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      onTap: () => context.push('/stocks/${viThe.symbol}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                viThe.symbol,
                style:
                    const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
              ),
              const SizedBox(width: 6),
              Text(
                '·',
                style: TextStyle(color: scheme.onSurfaceVariant),
              ),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  _statusLabel,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style:
                      TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
              ),
              const SizedBox(width: 8),
              _KetQuaBadge(
                  label: _ketQuaLabel(viThe.ketQua), color: ketQuaColor),
            ],
          ),
          const SizedBox(height: 8),
          // Tên Việt đủ của kịch bản (HQ-12) ở hàng riêng: để chung hàng với
          // Vào/Thoát thì tên dài bị cắt 'N1 · Giải c…'.
          _Metric(
              label: 'Mã kịch bản', value: TinHieuV3Mau.tenLoai(viThe.loai)),
          const SizedBox(height: 6),
          Row(
            children: [
              _Metric(label: 'Vào', value: _fmtPrice(viThe.giaVao)),
              const SizedBox(width: 16),
              _Metric(label: 'Thoát', value: _fmtPrice(viThe.giaThoat)),
            ],
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              if (phanTram != null) ...[
                Text(
                  '${phanTram >= 0 ? '+' : ''}${phanTram.toStringAsFixed(1)}%',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: phanTram >= 0
                        ? _KetQuaColor.thang(context)
                        : _KetQuaColor.thua(context),
                  ),
                ),
                const SizedBox(width: 12),
              ],
              Text(
                'Giữ ${viThe.soPhienDaGiu} phiên',
                style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  _dateRange,
                  textAlign: TextAlign.end,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style:
                      TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
              ),
            ],
          ),
          if (_lyDoThoat != null || viThe.laHoangLoanSau) ...[
            const SizedBox(height: 6),
            Row(
              children: [
                if (viThe.laHoangLoanSau) ...[
                  const _HoangLoanSauBadge(),
                  const SizedBox(width: 8),
                ],
                if (_lyDoThoat != null)
                  Expanded(
                    child: Text(
                      'Thoát: $_lyDoThoat',
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                          fontSize: 11, color: scheme.onSurfaceVariant),
                    ),
                  ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  /// Nhãn lý do thoát đã bản đồ tiếng Việt; null khi chưa thoát.
  String? get _lyDoThoat {
    final label = _lyDoThoatLabel(viThe.lyDoThoat);
    return label.isEmpty ? null : label;
  }

  String get _statusLabel {
    const labels = {
      'DanhDau': 'Đánh dấu',
      'ChoMua': 'Chờ mua',
      'DangGiu': 'Đang giữ',
      'DaThoat': 'Đã thoát',
      'BiHuy': 'Bị hủy',
    };
    return labels[viThe.trangThai] ?? viThe.trangThai;
  }

  String get _dateRange {
    final start =
        viThe.ngayTinHieu.isNotEmpty ? formatApiDate(viThe.ngayTinHieu) : '';
    if (start.isEmpty) return '';
    final end = viThe.ngayThoat != null && viThe.ngayThoat!.isNotEmpty
        ? formatApiDate(viThe.ngayThoat!)
        : null;
    return end == null ? start : '$start → $end';
  }

  static String _fmtPrice(double? v) {
    if (v == null || v == 0) return '—';
    return formatPrice(v);
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('$label: ',
            style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
        Flexible(
          child: Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          ),
        ),
      ],
    );
  }
}

class _KetQuaBadge extends StatelessWidget {
  const _KetQuaBadge({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.16),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(
        label,
        style:
            TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: color),
      ),
    );
  }
}

/// Dấu nhận biết nhỏ cho lệnh hoảng loạn sâu (FR-015a) — màu theo token cảnh báo.
class _HoangLoanSauBadge extends StatelessWidget {
  const _HoangLoanSauBadge();

  @override
  Widget build(BuildContext context) {
    final mau = Theme.of(context).brightness == Brightness.dark
        ? AppColors.darkWarning
        : AppColors.lightWarning;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: mau.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(5),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.warning_amber_rounded, size: 12, color: mau),
          const SizedBox(width: 3),
          Text(
            'Hoảng loạn sâu',
            style: TextStyle(
                fontSize: 10, fontWeight: FontWeight.w700, color: mau),
          ),
        ],
      ),
    );
  }
}

/// Nhãn tiếng Việt cho kết quả đo V3 (giá trị ASCII: Thang/Thua/HoaVon).
String _ketQuaLabel(String ketQua) {
  switch (ketQua) {
    case 'Thang':
      return 'Thắng';
    case 'Thua':
      return 'Thua';
    case 'HoaVon':
      return 'Hoà vốn';
    case '':
      return 'Chờ đo';
    default:
      return ketQua;
  }
}

/// Nhãn tiếng Việt cho lý do thoát V3 — tiện ích thuần tách riêng để unit test.
String _lyDoThoatLabel(String? lyDo) => v3LyDoThoatLabel(lyDo);

/// Màu trạng thái kết quả — theo token chủ đề (scheme/AppColors), không gõ cứng hex,
/// để đọc rõ ở cả dark/light và đổi theo theme một chỗ.
class _KetQuaColor {
  static Color thang(BuildContext context) => AppColors.positive(context);

  static Color thua(BuildContext context) =>
      Theme.of(context).colorScheme.error;

  static Color ngang(BuildContext context) =>
      Theme.of(context).colorScheme.onSurfaceVariant;

  static Color choDo(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark
          ? AppColors.darkWarning
          : AppColors.lightWarning;

  static Color rr(BuildContext context) =>
      Theme.of(context).colorScheme.primary;

  static Color of(BuildContext context, String ketQua) {
    final k = ketQua.toLowerCase();
    if (k == 'thang') return thang(context);
    if (k == 'thua') return thua(context);
    if (k == 'hoavon') return ngang(context);
    if (k.isEmpty) return choDo(context);
    return ngang(context);
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Models (parse cục bộ từ JSON camelCase của backend V3)
// ─────────────────────────────────────────────────────────────────────────────

class _TomTat {
  const _TomTat(
      {required this.danhMuc,
      required this.theoKichBan,
      required this.denNgay});

  final _DanhMuc? danhMuc;
  final List<_KichBanStats> theoKichBan;
  final String? denNgay;

  factory _TomTat.fromJson(Map<String, dynamic> json) => _TomTat(
        danhMuc: json['danhMuc'] == null
            ? null
            : _DanhMuc.fromJson(json['danhMuc'] as Map<String, dynamic>),
        theoKichBan: (json['theoKichBan'] as List<dynamic>? ?? [])
            .map((e) => _KichBanStats.fromJson(e as Map<String, dynamic>))
            .toList(),
        denNgay: json['denNgay']?.toString(),
      );
}

class _DanhMuc {
  const _DanhMuc({
    required this.giaTriHienTai,
    required this.tienMat,
    required this.soViTheDangGiu,
    required this.sutGiamToiDaPhanTram,
  });

  final double giaTriHienTai;
  final double tienMat;
  final int soViTheDangGiu;
  final double sutGiamToiDaPhanTram;

  factory _DanhMuc.fromJson(Map<String, dynamic> json) => _DanhMuc(
        giaTriHienTai: _asDouble(json['giaTriHienTai']),
        tienMat: _asDouble(json['tienMat']),
        soViTheDangGiu: _asInt(json['soViTheDangGiu']),
        sutGiamToiDaPhanTram: _asDouble(json['sutGiamToiDaPhanTram']),
      );
}

class _KichBanStats {
  const _KichBanStats({
    required this.loai,
    required this.ten,
    required this.soLenhDong,
    required this.soThang,
    required this.soThua,
    required this.soHoaVon,
    required this.tyLeThangPhanTram,
    required this.laiTBPhanTram,
    required this.loTBPhanTram,
    required this.teNhatPhanTram,
    required this.giuTBPhien,
  });

  final String loai;
  final String ten;
  final int soLenhDong;
  final int soThang;
  final int soThua;
  final int soHoaVon;
  final double tyLeThangPhanTram;
  final double laiTBPhanTram;
  final double loTBPhanTram;
  final double teNhatPhanTram;
  final double giuTBPhien;

  factory _KichBanStats.fromJson(Map<String, dynamic> json) => _KichBanStats(
        loai: json['loai'] as String? ?? '',
        ten: json['ten'] as String? ?? '',
        soLenhDong: _asInt(json['soLenhDong']),
        soThang: _asInt(json['soThang']),
        soThua: _asInt(json['soThua']),
        soHoaVon: _asInt(json['soHoaVon']),
        tyLeThangPhanTram: _asDouble(json['tyLeThangPhanTram']),
        laiTBPhanTram: _asDouble(json['laiTBPhanTram']),
        loTBPhanTram: _asDouble(json['loTBPhanTram']),
        teNhatPhanTram: _asDouble(json['teNhatPhanTram']),
        giuTBPhien: _asDouble(json['giuTBPhien']),
      );
}

class _ViThe {
  const _ViThe({
    required this.id,
    required this.symbol,
    required this.loai,
    required this.trangThai,
    required this.soPhienDaGiu,
    required this.giaVao,
    required this.giaThoat,
    required this.phanTramLoiNhuan,
    required this.ketQua,
    required this.ngayTinHieu,
    required this.ngayThoat,
    required this.lyDoThoat,
    required this.laHoangLoanSau,
  });

  final int id;
  final String symbol;
  final String loai;
  final String trangThai;
  final int soPhienDaGiu;
  final double? giaVao;
  final double? giaThoat;
  final double? phanTramLoiNhuan;
  final String ketQua;
  final String ngayTinHieu;
  final String? ngayThoat;
  final String? lyDoThoat;
  final bool laHoangLoanSau;

  factory _ViThe.fromJson(Map<String, dynamic> json) => _ViThe(
        id: _asInt(json['id']),
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        loai: json['loai'] as String? ?? '',
        trangThai: json['trangThai'] as String? ?? '',
        soPhienDaGiu: _asInt(json['soPhienDaGi']),
        giaVao: _asDoubleOrNull(json['giaVao']),
        giaThoat: _asDoubleOrNull(json['giaThoat']),
        phanTramLoiNhuan: _asDoubleOrNull(json['phanTramLoiNhuan']),
        ketQua: json['ketQua'] as String? ?? '',
        ngayTinHieu: json['ngayTinHieu']?.toString() ?? '',
        ngayThoat: json['ngayThoat']?.toString(),
        lyDoThoat: json['lyDoThoat']?.toString(),
        laHoangLoanSau: json['laHoangLoanSau'] as bool? ?? false,
      );
}

int _asInt(Object? v) {
  if (v == null) return 0;
  if (v is num) return v.toInt();
  return int.tryParse(v.toString()) ?? 0;
}

double _asDouble(Object? v) {
  if (v == null) return 0;
  if (v is num) return v.toDouble();
  return double.tryParse(v.toString()) ?? 0;
}

double? _asDoubleOrNull(Object? v) {
  if (v == null) return null;
  if (v is num) return v.toDouble();
  return double.tryParse(v.toString());
}
