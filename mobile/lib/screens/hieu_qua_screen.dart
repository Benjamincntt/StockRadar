import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/theme/app_colors.dart';
import '../core/time/api_date.dart';
import '../widgets/glass_card.dart';

/// Màn "Hiệu quả" — đo lường kết quả các kịch bản đã kích hoạt.
///
/// Dữ liệu lấy từ 2 endpoint:
/// - `/hieu-qua/tom-tat?period=` → thẻ tóm tắt + thống kê theo loại kịch bản.
/// - `/hieu-qua/lich-su?page=&size=` → danh sách lệnh (phân trang).
class HieuQuaScreen extends StatefulWidget {
  const HieuQuaScreen({super.key});

  @override
  State<HieuQuaScreen> createState() => _HieuQuaScreenState();
}

class _HieuQuaScreenState extends State<HieuQuaScreen> {
  static const _pageSize = 20;

  ApiClient get _api => context.read<ApiClient>();

  // Bộ lọc kỳ: week/month/quarter/all.
  String _period = 'month';

  _TomTat? _tomTat;
  final List<_Lenh> _lenh = [];
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

  bool get _hasMore => _lenh.length < _totalCount;

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _page = 1;
    });
    try {
      final results = await Future.wait([
        _api.getHieuQuaTomTat(period: _period),
        _api.getHieuQuaLichSu(page: 1, size: _pageSize),
      ]);
      if (!mounted) return;
      final tomTat = _TomTat.fromJson(results[0]);
      final lichSu = results[1];
      final items = (lichSu['items'] as List<dynamic>? ?? [])
          .map((e) => _Lenh.fromJson(e as Map<String, dynamic>))
          .toList();
      setState(() {
        _tomTat = tomTat;
        _lenh
          ..clear()
          ..addAll(items);
        _totalCount = (lichSu['totalCount'] as num?)?.toInt() ?? items.length;
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
        _error = 'Không tải được dữ liệu hiệu quả. Thử lại sau.';
        _loading = false;
      });
    }
  }

  Future<void> _loadMore() async {
    if (_loadingMore || !_hasMore) return;
    setState(() => _loadingMore = true);
    try {
      final nextPage = _page + 1;
      final json = await _api.getHieuQuaLichSu(page: nextPage, size: _pageSize);
      if (!mounted) return;
      final items = (json['items'] as List<dynamic>? ?? [])
          .map((e) => _Lenh.fromJson(e as Map<String, dynamic>))
          .toList();
      setState(() {
        _lenh.addAll(items);
        _totalCount = (json['totalCount'] as num?)?.toInt() ?? _totalCount;
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
            _SummaryCards(tomTat: _tomTat),
            const SizedBox(height: 16),
            _ScenarioBreakdown(stats: _tomTat?.theoLoaiKichBan ?? const []),
            const SizedBox(height: 16),
            _TradeHistory(
              lenh: _lenh,
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
// Thẻ tóm tắt
// ─────────────────────────────────────────────────────────────────────────────

class _SummaryCards extends StatelessWidget {
  const _SummaryCards({required this.tomTat});

  final _TomTat? tomTat;

  @override
  Widget build(BuildContext context) {
    final t = tomTat;
    final winRate = t?.tyLeThang ?? 0;
    final rr = t?.tbRR ?? 0;
    final total = t?.tongKichHoat ?? 0;

    return Row(
      children: [
        Expanded(
          child: _SummaryCard(
            value: '${winRate.toStringAsFixed(0)}%',
            label: 'Thắng',
            color: _KetQuaColor.thang,
            icon: Icons.check_circle_outline,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _SummaryCard(
            value: rr.toStringAsFixed(1),
            label: 'R:R TB',
            color: _KetQuaColor.rr,
            icon: Icons.balance_outlined,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _SummaryCard(
            value: '$total',
            label: 'Tổng',
            color: _KetQuaColor.ngang,
            icon: Icons.layers_outlined,
          ),
        ),
      ],
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.value,
    required this.label,
    required this.color,
    required this.icon,
  });

  final String value;
  final String label;
  final Color color;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GlassCard(
      padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 8),
      child: Column(
        children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(height: 8),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 22,
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
              fontSize: 11,
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
// Thống kê theo loại kịch bản
// ─────────────────────────────────────────────────────────────────────────────

class _ScenarioBreakdown extends StatelessWidget {
  const _ScenarioBreakdown({required this.stats});

  final List<_LoaiStats> stats;

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

  final _LoaiStats stats;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final rate = (stats.tyLeThang / 100).clamp(0.0, 1.0);
    final barColor = _rateColor(stats.tyLeThang);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                stats.tenKichBan,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
              ),
            ),
            const SizedBox(width: 8),
            Text(
              '${stats.tyLeThang.toStringAsFixed(0)}%',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: barColor,
              ),
            ),
            const SizedBox(width: 8),
            Text(
              '(${stats.thang}/${stats.tong})',
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
      ],
    );
  }

  Color _rateColor(double rate) {
    if (rate >= 60) return _KetQuaColor.thang;
    if (rate >= 40) return _KetQuaColor.choDo;
    return _KetQuaColor.thua;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Lịch sử lệnh
// ─────────────────────────────────────────────────────────────────────────────

class _TradeHistory extends StatelessWidget {
  const _TradeHistory({
    required this.lenh,
    required this.hasMore,
    required this.loadingMore,
    required this.onLoadMore,
  });

  final List<_Lenh> lenh;
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
          const SectionTitle('Lịch sử lệnh'),
          const SizedBox(height: 12),
          if (lenh.isEmpty)
            Text(
              'Chưa có dữ liệu. Hệ thống sẽ tự động đo lường sau T+2.5 phiên.',
              style: TextStyle(fontSize: 12, height: 1.4, color: scheme.onSurfaceVariant),
            )
          else ...[
            ...lenh.map((l) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: _TradeCard(lenh: l),
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
  const _TradeCard({required this.lenh});

  final _Lenh lenh;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final ketQuaColor = _KetQuaColor.of(lenh.ketQua);
    final phanTram = lenh.phanTram;

    return SurfaceRow(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      onTap: () => context.push('/stocks/${lenh.symbol}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                lenh.symbol,
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
                  lenh.loaiKichBan,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
              ),
              const SizedBox(width: 8),
              _KetQuaBadge(label: lenh.ketQua, color: ketQuaColor),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              _Metric(label: 'Vào', value: _fmtPrice(lenh.giaVao)),
              const SizedBox(width: 16),
              _Metric(label: 'Thoát', value: _fmtPrice(lenh.giaThoat)),
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
              if (lenh.rrThucTe != null) ...[
                Text(
                  'R:R ${lenh.rrThucTe!.toStringAsFixed(1)}',
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
                const SizedBox(width: 12),
              ],
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

  String get _dateRange {
    final start = lenh.ngayKichHoat.isNotEmpty ? formatApiDate(lenh.ngayKichHoat) : '';
    if (start.isEmpty) return '';
    final end = lenh.ngayThoat != null && lenh.ngayThoat!.isNotEmpty
        ? formatApiDate(lenh.ngayThoat!)
        : null;
    return end == null ? start : '$start → $end';
  }

  static String _fmtPrice(double? v) =>
      v == null || v == 0 ? '—' : v.toStringAsFixed(1);
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

/// Màu sắc cố định cho từng trạng thái kết quả (đọc rõ ở cả dark/light).
class _KetQuaColor {
  static const thang = Color(0xFF22C55E); // xanh lá — Thắng
  static const thua = Color(0xFFEF4444); // đỏ — Thua
  static const ngang = Color(0xFF9CA3AF); // xám — Ngang / trung tính
  static const choDo = Color(0xFFF59E0B); // hổ phách — Chờ đo
  static const rr = Color(0xFF3B82F6); // xanh dương — R:R

  static Color of(String ketQua) {
    final k = ketQua.toLowerCase();
    if (k.contains('thắng') || k.contains('thang')) return thang;
    if (k.contains('thua')) return thua;
    if (k.contains('chờ') || k.contains('cho')) return choDo;
    return ngang;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Models (parse cục bộ từ JSON camelCase của backend)
// ─────────────────────────────────────────────────────────────────────────────

class _TomTat {
  const _TomTat({
    required this.tongKichHoat,
    required this.thang,
    required this.thua,
    required this.ngang,
    required this.choDo,
    required this.tyLeThang,
    required this.tbRR,
    required this.tbPhanTram,
    required this.theoLoaiKichBan,
  });

  final int tongKichHoat;
  final int thang;
  final int thua;
  final int ngang;
  final int choDo;
  final double tyLeThang;
  final double tbRR;
  final double tbPhanTram;
  final List<_LoaiStats> theoLoaiKichBan;

  factory _TomTat.fromJson(Map<String, dynamic> json) => _TomTat(
        tongKichHoat: _asInt(json['tongKichHoat']),
        thang: _asInt(json['thang']),
        thua: _asInt(json['thua']),
        ngang: _asInt(json['ngang']),
        choDo: _asInt(json['choDo']),
        tyLeThang: _asDouble(json['tyLeThang']),
        tbRR: _asDouble(json['tbRR'] ?? json['tbRr']),
        tbPhanTram: _asDouble(json['tbPhanTram']),
        theoLoaiKichBan: (json['theoLoaiKichBan'] as List<dynamic>? ?? [])
            .map((e) => _LoaiStats.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class _LoaiStats {
  const _LoaiStats({
    required this.tenKichBan,
    required this.tong,
    required this.thang,
    required this.thua,
    required this.ngang,
    required this.tyLeThang,
    required this.tbRR,
  });

  final String tenKichBan;
  final int tong;
  final int thang;
  final int thua;
  final int ngang;
  final double tyLeThang;
  final double tbRR;

  factory _LoaiStats.fromJson(Map<String, dynamic> json) => _LoaiStats(
        tenKichBan: json['tenKichBan'] as String? ?? '',
        tong: _asInt(json['tong']),
        thang: _asInt(json['thang']),
        thua: _asInt(json['thua']),
        ngang: _asInt(json['ngang']),
        tyLeThang: _asDouble(json['tyLeThang']),
        tbRR: _asDouble(json['tbRR'] ?? json['tbRr']),
      );
}

class _Lenh {
  const _Lenh({
    required this.id,
    required this.symbol,
    required this.loaiKichBan,
    required this.ketQua,
    required this.giaVao,
    required this.giaThoat,
    required this.phanTram,
    required this.rrThucTe,
    required this.ngayKichHoat,
    required this.ngayThoat,
  });

  final int id;
  final String symbol;
  final String loaiKichBan;
  final String ketQua;
  final double? giaVao;
  final double? giaThoat;
  final double? phanTram;
  final double? rrThucTe;
  final String ngayKichHoat;
  final String? ngayThoat;

  factory _Lenh.fromJson(Map<String, dynamic> json) => _Lenh(
        id: _asInt(json['id']),
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        loaiKichBan: json['loaiKichBan'] as String? ?? '',
        ketQua: json['ketQua'] as String? ?? '',
        giaVao: _asDoubleOrNull(json['giaVao']),
        giaThoat: _asDoubleOrNull(json['giaThoat']),
        phanTram: _asDoubleOrNull(json['phanTram']),
        rrThucTe: _asDoubleOrNull(json['rrThucTe']),
        ngayKichHoat: json['ngayKichHoat']?.toString() ?? '',
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
