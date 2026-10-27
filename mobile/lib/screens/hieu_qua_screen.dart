import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/theme/app_colors.dart';
import '../core/time/api_date.dart';
import '../widgets/glass_card.dart';

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
      final json = await _api.getV3HieuQuaLichSu(page: nextPage, size: _pageSize);
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
  const _PeriodChip({required this.label, required this.active, required this.onTap});

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
            color: active ? scheme.primary.withValues(alpha: 0.16) : AppColors.surfaceLow(context),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(
              color: active ? scheme.primary : scheme.outline.withValues(alpha: 0.25),
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
            color: _KetQuaColor.ngang,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniCard(
            value: _fmtTy(d?.tienMat),
            label: 'Tiền mặt',
            color: _KetQuaColor.thang,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniCard(
            value: '${d?.soViTheDangGiu ?? 0}',
            label: 'Đang giữ',
            color: _KetQuaColor.rr,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniCard(
            value: '${(d?.sutGiamToiDaPhanTram ?? 0).toStringAsFixed(1)}%',
            label: 'Sụt giảm TBĐ',
            color: _KetQuaColor.thua,
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
  const _MiniCard({required this.value, required this.label, required this.color});

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
    final barColor = _rateColor(stats.tyLeThangPhanTram);

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
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
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

  Color _rateColor(double rate) {
    if (rate >= 60) return _KetQuaColor.thang;
    if (rate >= 40) return _KetQuaColor.choDo;
    return _KetQuaColor.thua;
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
              style: TextStyle(fontSize: 12, height: 1.4, color: scheme.onSurfaceVariant),
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
                        style: TextButton.styleFrom(foregroundColor: scheme.primary),
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
    final ketQuaColor = _KetQuaColor.of(viThe.ketQua);
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
                style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
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
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
              ),
              const SizedBox(width: 8),
              _KetQuaBadge(label: _ketQuaLabel(viThe.ketQua), color: ketQuaColor),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              _Metric(label: 'Mã kịch bản', value: viThe.loai),
              const Spacer(),
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
                    color: phanTram >= 0 ? _KetQuaColor.thang : _KetQuaColor.thua,
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
                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
              ),
            ],
          ),
        ],
      ),
    );
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
    final start = viThe.ngayTinHieu.isNotEmpty ? formatApiDate(viThe.ngayTinHieu) : '';
    if (start.isEmpty) return '';
    final end = viThe.ngayThoat != null && viThe.ngayThoat!.isNotEmpty
        ? formatApiDate(viThe.ngayThoat!)
        : null;
    return end == null ? start : '$start → $end';
  }

  static String _fmtPrice(double? v) => v == null || v == 0 ? '—' : v.toStringAsFixed(1);
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
        Text('$label: ', style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
        Text(
          value,
          style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
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
        style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: color),
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

/// Màu sắc cố định cho từng trạng thái kết quả (đọc rõ ở cả dark/light).
class _KetQuaColor {
  static const thang = Color(0xFF22C55E); // xanh lá — Thắng
  static const thua = Color(0xFFEF4444); // đỏ — Thua
  static const ngang = Color(0xFF9CA3AF); // xám — trung tính / hòa vốn
  static const choDo = Color(0xFFF59E0B); // hổ phách — Chờ đo
  static const rr = Color(0xFF3B82F6); // xanh dương — đang giữ

  static Color of(String ketQua) {
    final k = ketQua.toLowerCase();
    if (k == 'thang') return thang;
    if (k == 'thua') return thua;
    if (k == 'hoavon') return ngang;
    if (k.isEmpty) return choDo;
    return ngang;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Models (parse cục bộ từ JSON camelCase của backend V3)
// ─────────────────────────────────────────────────────────────────────────────

class _TomTat {
  const _TomTat({required this.danhMuc, required this.theoKichBan, required this.denNgay});

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

  factory _ViThe.fromJson(Map<String, dynamic> json) => _ViThe(
        id: _asInt(json['id']),
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        loai: json['loai'] as String? ?? '',
        trangThai: json['trangThai'] as String? ?? '',
        soPhienDaGiu: _asInt(json['soPhienDaGiu']),
        giaVao: _asDoubleOrNull(json['giaVao']),
        giaThoat: _asDoubleOrNull(json['giaThoat']),
        phanTramLoiNhuan: _asDoubleOrNull(json['phanTramLoiNhuan']),
        ketQua: json['ketQua'] as String? ?? '',
        ngayTinHieu: json['ngayTinHieu']?.toString() ?? '',
        ngayThoat: json['ngayThoat']?.toString(),
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
