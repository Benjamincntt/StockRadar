import 'dart:convert';

import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';
import 'glass_card.dart';
import 'score_pill.dart' show formatPrice;

/// Khối UI + model cho tín hiệu bộ kịch bản P1++ (V3) trên màn chi tiết mã.
/// Nguồn dữ liệu: GET /stocks/{symbol}/v3 (TinHieuV3Dto[]).
/// Quy ước màu trạng thái tín hiệu: đã vào lệnh = xanh, đã báo = cam,
/// bị cắt danh mục = xám, bị hủy = đỏ.

// ─────────────────────────────────────────────────────────────────────────────
// Model — parse cục bộ từ JSON camelCase của backend
// ─────────────────────────────────────────────────────────────────────────────

class TinHieuV3 {
  const TinHieuV3({
    required this.id,
    required this.symbol,
    required this.loai,
    required this.ngayTinHieu,
    required this.giaTinHieu,
    required this.hang,
    this.giaTriXepHang,
    required this.muaMoCuaPhienSau,
    required this.trangThai,
    this.snapshotJson,
    this.giaVao,
    this.giaThoat,
    this.ketQua,
  });

  final int id;
  final String symbol;
  final String loai;
  final String ngayTinHieu;
  final double giaTinHieu;
  final int hang;
  final double? giaTriXepHang;
  final bool muaMoCuaPhienSau;
  final String trangThai;
  final String? snapshotJson;
  final double? giaVao;
  final double? giaThoat;
  final String? ketQua;

  factory TinHieuV3.fromJson(Map<String, dynamic> json) => TinHieuV3(
        id: _soNguyen(json['id']),
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        loai: json['loai'] as String? ?? '',
        ngayTinHieu: json['ngayTinHieu']?.toString() ?? '',
        giaTinHieu: _soThuc(json['giaTinHieu']) ?? 0,
        hang: _soNguyen(json['hang']),
        giaTriXepHang: _soThuc(json['giaTriXepHang']),
        muaMoCuaPhienSau: json['muaMoCuaPhienSau'] as bool? ?? false,
        trangThai: json['trangThai'] as String? ?? '',
        snapshotJson: json['snapshotJson'] as String?,
        giaVao: _soThuc(json['giaVao']),
        giaThoat: _soThuc(json['giaThoat']),
        ketQua: json['ketQua'] as String?,
      );

  /// Snapshot chỉ báo at ngày tín hiệu (FR-024) — lỗi parse coi như trống.
  Map<String, dynamic> get snapshot {
    final raw = snapshotJson;
    if (raw == null || raw.isEmpty) return const {};
    try {
      final decoded = jsonDecode(raw);
      return decoded is Map<String, dynamic> ? decoded : const {};
    } catch (_) {
      return const {};
    }
  }

  /// Kế hoạch mua theo quy tắc V3: ATO mở cửa phiên sau hoặc ATC cuối phiên.
  String get mua => muaMoCuaPhienSau ? 'ATO mở cửa phiên sau' : 'ATC cuối phiên tín hiệu';

  /// Luật bán realtime từng kịch bản (Phụ lục A); N3 bán ATO tại TDM.
  String? get ban {
    final luat = TinHieuV3Mau.luotBan(loai);
    if (luat == null) return null;
    return loai == 'N3'
        ? 'Bán lệnh ATO phiên TDM=2 tháng sau · Giữ tối đa ${luat.giuToiDa} phiên'
        : 'Bán realtime: giá khớp ≥ max(chạm ${luat.ma}, giá vào +0,4%) · Giữ tối đa ${luat.giuToiDa} phiên';
  }

  /// Kết quả đo (ASCII từ backend): Thang/Thua/HoaVon — chưa đo thì bỏ trống.
  String get ketQuaHienThi => TinHieuV3Mau.nhanKetQua(ketQua);

  /// Thứ tự ưu tiên hiển thị: ưu tiên hành động (đã vào lệnh, đã báo) trước,
  /// cùng trạng thái thì hạng nhỏ hơn (tốt hơn) hiển thị trước.
  static int uuTien(TinHieuV3 a, TinHieuV3 b) {
    final theoTrangThai =
        TinHieuV3Mau.uuTien(a.trangThai).compareTo(TinHieuV3Mau.uuTien(b.trangThai));
    if (theoTrangThai != 0) return theoTrangThai;
    return a.hang.compareTo(b.hang);
  }
}

/// Sắp xếp danh sách tín hiệu theo thứ tự ưu tiên hiển thị.
List<TinHieuV3> sapXepTinHieuV3(Iterable<TinHieuV3> danhSach) {
  final ds = [...danhSach]..sort(TinHieuV3.uuTien);
  return ds;
}

int _soNguyen(Object? v) {
  if (v == null) return 0;
  if (v is num) return v.toInt();
  return int.tryParse(v.toString()) ?? 0;
}

double? _soThuc(Object? v) {
  if (v == null) return null;
  if (v is num) return v.toDouble();
  return double.tryParse(v.toString());
}

// ─────────────────────────────────────────────────────────────────────────────
// Màu sắc + nhãn trạng thái tín hiệu V3
// ─────────────────────────────────────────────────────────────────────────────

/// Luật bán theo kịch bản (Phụ lục A): MA chốt lời động + số phiên giữ tối đa.
class LuotBanV3 {
  const LuotBanV3({required this.ma, required this.giuToiDa});

  final String ma;
  final int giuToiDa;
}

/// Màu cố định cho từng trạng thái tín hiệu V3 (đọc rõ ở cả dark/light).
class TinHieuV3Mau {
  static const mauXanh = Color(0xFF22C55E); // đã vào lệnh
  static const mauCam = Color(0xFFF59E0B); // đã báo
  static const mauXam = Color(0xFF9CA3AF); // bị cắt danh mục / chờ trạng thái
  static const mauDo = Color(0xFFEF4444); // bị hủy

  static const _nhanTheoLoai = {
    'KB1': 'KB1 · Hoảng loạn',
    'KB2': 'KB2 · Chỉ số thủng MA50',
    'KB4': 'KB4 · Ngành bán quá mức',
    'N1': 'N1 · Giải chấp cạn kiệt',
    'N3': 'N3 · Đón dòng tiền đầu tháng',
  };

  static const _luotBanTheoLoai = {
    'KB1': LuotBanV3(ma: 'MA10', giuToiDa: 15),
    'KB2': LuotBanV3(ma: 'MA10', giuToiDa: 15),
    'KB4': LuotBanV3(ma: 'MA10', giuToiDa: 10),
    'N1': LuotBanV3(ma: 'MA20', giuToiDa: 15),
    'N3': LuotBanV3(ma: 'ATO TDM=2 tháng sau', giuToiDa: 12),
  };

  static const _nhanTheoTrangThai = {
    'DaBao': 'Đã báo',
    'BiCatDanhMuc': 'Bị cắt danh mục',
    'DaVaoLenh': 'Đã vào lệnh',
    'BiHuy': 'Bị hủy',
  };

  static String tenLoai(String loai) => _nhanTheoLoai[loai] ?? loai;

  static LuotBanV3? luotBan(String loai) => _luotBanTheoLoai[loai];

  static Color cua(String trangThai) {
    switch (trangThai) {
      case 'DaVaoLenh':
        return mauXanh;
      case 'DaBao':
        return mauCam;
      case 'BiHuy':
        return mauDo;
      case 'BiCatDanhMuc':
        return mauXam;
      default:
        return mauXam;
    }
  }

  static String nhan(String trangThai) => _nhanTheoTrangThai[trangThai] ?? trangThai;

  /// Nhãn kết quả đo — so khớp ASCII chính xác vì backend ghi 'Thang'.
  static String nhanKetQua(String? ketQua) {
    switch (ketQua) {
      case 'Thang':
        return 'Thắng';
      case 'Thua':
        return 'Thua';
      case 'HoaVon':
        return 'Hoà vốn';
      case null:
      case '':
        return 'Chờ đo';
      default:
        return ketQua;
    }
  }

  static Color cuaKetQua(String? ketQua) {
    switch (ketQua) {
      case 'Thang':
        return mauXanh;
      case 'Thua':
        return mauDo;
      case 'HoaVon':
        return mauCam;
      default:
        return mauXam;
    }
  }

  /// Thứ tự ưu tiên hiển thị: tín hiệu cần hành động trước, tín hiệu đã đóng sau.
  static int uuTien(String trangThai) {
    const thuTu = {
      'DaVaoLenh': 0,
      'DaBao': 1,
      'BiCatDanhMuc': 2,
      'BiHuy': 3,
    };
    return thuTu[trangThai] ?? 99;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Nhãn đọc được cho các khóa trong snapshotJson (FR-024)
// ─────────────────────────────────────────────────────────────────────────────

const _snapshotLabels = {
  'tv20': 'TV20 (tỷ)',
  'close': 'Giá đóng cửa',
  'rsi14': 'RSI14',
  'ret20': 'RET20',
  'secr20': 'SECR20',
  'rs63': 'RS63',
  'rs126': 'RS126',
  'floor20': 'FLOOR20',
  'laSan': 'Phiên sàn',
  'breadth50': 'BREADTH50',
  'chiSoMa50Lech': 'Chỉ số lệch MA50',
  'left': 'LEFT',
  'tdm': 'TDM',
  'soMaUni': 'UNI',
  'sector': 'Ngành',
};

const _snapshotThuTu = [
  'breadth50',
  'chiSoMa50Lech',
  'tv20',
  'rsi14',
  'ret20',
  'secr20',
  'rs63',
  'rs126',
  'floor20',
  'left',
  'tdm',
  'soMaUni',
  'laSan',
  'close',
];

String _formatSnapshotValue(Object? value) {
  if (value is bool) return value ? 'Có' : 'Không';
  if (value is num) {
    return value.toStringAsFixed(value == value.roundToDouble() ? 0 : 2);
  }
  if (value == null) return '—';
  return value.toString();
}

// ─────────────────────────────────────────────────────────────────────────────
// Huy hiệu trạng thái tín hiệu V3 (khu vực header màn chi tiết mã)
// ─────────────────────────────────────────────────────────────────────────────

class ScenarioStatusBadges extends StatelessWidget {
  const ScenarioStatusBadges({super.key, required this.tinHieu});

  /// Danh sách tín hiệu V3 đã sắp theo thứ tự ưu tiên.
  final List<TinHieuV3> tinHieu;

  @override
  Widget build(BuildContext context) {
    if (tinHieu.isEmpty) return const SizedBox.shrink();
    return SizedBox(
      height: 30,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: tinHieu.length,
        separatorBuilder: (_, __) => const SizedBox(width: 6),
        itemBuilder: (context, i) => _HuyHieuTinHieu(tinHieu: tinHieu[i]),
      ),
    );
  }
}

class _HuyHieuTinHieu extends StatelessWidget {
  const _HuyHieuTinHieu({required this.tinHieu});

  final TinHieuV3 tinHieu;

  @override
  Widget build(BuildContext context) {
    final mau = TinHieuV3Mau.cua(tinHieu.trangThai);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: mau.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: mau.withValues(alpha: 0.4)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(color: mau, shape: BoxShape.circle),
          ),
          const SizedBox(width: 6),
          Text(
            '${TinHieuV3Mau.tenLoai(tinHieu.loai)} — ${TinHieuV3Mau.nhan(tinHieu.trangThai)} · Hạng ${tinHieu.hang}',
            style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: mau),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Thẻ tín hiệu V3 — kịch bản, kế hoạch mua/bán, snapshot chỉ báo, kết quả
// ─────────────────────────────────────────────────────────────────────────────

class TinHieuV3Card extends StatelessWidget {
  const TinHieuV3Card({super.key, required this.tinHieu});

  final TinHieuV3 tinHieu;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final mau = TinHieuV3Mau.cua(tinHieu.trangThai);
    final ketQuaMau = TinHieuV3Mau.cuaKetQua(tinHieu.ketQua);
    final snapshot = tinHieu.snapshot;
    final keys = snapshot.keys.where((k) => k != 'symbol').toList()
      ..sort((a, b) {
        final ia = _snapshotThuTu.indexOf(a);
        final ib = _snapshotThuTu.indexOf(b);
        return (ia == -1 ? 99 : ia).compareTo(ib == -1 ? 99 : ib);
      });
    final ban = tinHieu.ban;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                TinHieuV3Mau.tenLoai(tinHieu.loai),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
              ),
            ),
            _HuyHieuTrangThai(nhan: TinHieuV3Mau.nhan(tinHieu.trangThai), mau: mau),
            const SizedBox(width: 6),
            _HuyHieuTrangThai(nhan: tinHieu.ketQuaHienThi, mau: ketQuaMau),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          'Ngày tín hiệu ${formatApiDateVietnam(tinHieu.ngayTinHieu)} · Hạng ${tinHieu.hang}',
          style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
        ),
        const SizedBox(height: 10),
        Row(
          children: [
            Expanded(
              child: _MetricTile(
                label: 'GIÁ TÍN HIỆU',
                value: formatPrice(tinHieu.giaTinHieu),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MetricTile(
                label: 'GIÁ VÀO',
                value: tinHieu.giaVao != null ? formatPrice(tinHieu.giaVao!) : '—',
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MetricTile(
                label: 'GIÁ THOÁT',
                value: tinHieu.giaThoat != null ? formatPrice(tinHieu.giaThoat!) : '—',
              ),
            ),
          ],
        ),
        if (tinHieu.giaTriXepHang != null) ...[
          const SizedBox(height: 8),
          Text(
            'Thước xếp hạng ${_formatSnapshotValue(tinHieu.giaTriXepHang)}',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
        ],
        const SizedBox(height: 8),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            color: AppColors.surfaceLow(context),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('MUA', style: labelCaps(context).copyWith(fontSize: 9)),
              const SizedBox(height: 3),
              Text(
                tinHieu.mua,
                style: TextStyle(fontSize: 12, height: 1.4, color: scheme.onSurface),
              ),
              if (ban != null) ...[
                const SizedBox(height: 6),
                Text('BÁN', style: labelCaps(context).copyWith(fontSize: 9)),
                const SizedBox(height: 3),
                Text(
                  ban,
                  style: TextStyle(fontSize: 12, height: 1.4, color: scheme.onSurface),
                ),
              ],
            ],
          ),
        ),
        if (keys.isNotEmpty) ...[
          const SizedBox(height: 10),
          Text(
            'CHỈ BÁO TẠI NGÀY TÍN HIỆU',
            style: labelCaps(context).copyWith(fontSize: 9),
          ),
          const SizedBox(height: 6),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: keys
                .map(
                  (k) => _SnapshotChip(
                    nhan: _snapshotLabels[k] ?? k,
                    giaTri: _formatSnapshotValue(snapshot[k]),
                  ),
                )
                .toList(),
          ),
        ],
      ],
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(label, style: labelCaps(context).copyWith(fontSize: 9)),
          ),
          const SizedBox(height: 4),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              value,
              style: dataFont(context, size: 15, weight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }
}

class _HuyHieuTrangThai extends StatelessWidget {
  const _HuyHieuTrangThai({required this.nhan, required this.mau});

  final String nhan;
  final Color mau;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
      decoration: BoxDecoration(
        color: mau.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(nhan, style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: mau)),
    );
  }
}

class _SnapshotChip extends StatelessWidget {
  const _SnapshotChip({required this.nhan, required this.giaTri});

  final String nhan;
  final String giaTri;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.6)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            nhan,
            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
          ),
          const SizedBox(width: 4),
          Text(
            giaTri,
            style: dataFont(context, size: 10, weight: FontWeight.w700),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Danh sách tín hiệu V3 của mã (thay cho khối bằng chứng kịch bản V2)
// ─────────────────────────────────────────────────────────────────────────────

class ScenarioV3ListCard extends StatelessWidget {
  const ScenarioV3ListCard({super.key, required this.tinHieu});

  /// Danh sách tín hiệu V3 đã sắp theo thứ tự ưu tiên.
  final List<TinHieuV3> tinHieu;

  @override
  Widget build(BuildContext context) {
    if (tinHieu.isEmpty) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SectionTitle(
          'Tín hiệu kịch bản V3',
          subtitle: 'Bộ kịch bản P1++ (bán realtime) — mới nhất trước',
        ),
        const SizedBox(height: 4),
        ...List.generate(tinHieu.length, (i) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (i > 0)
                  Divider(height: 24, color: scheme.outlineVariant.withValues(alpha: 0.5)),
                TinHieuV3Card(tinHieu: tinHieu[i]),
              ],
            )),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Trạng thái trống — chưa có tín hiệu V3
// ─────────────────────────────────────────────────────────────────────────────

class ScenarioEmptyState extends StatelessWidget {
  const ScenarioEmptyState({super.key, required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 28, horizontal: 12),
      child: Column(
        children: [
          Icon(Icons.radar, size: 30, color: scheme.onSurfaceVariant),
          const SizedBox(height: 10),
          Text(
            message,
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 12.5, height: 1.5, color: scheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }
}
