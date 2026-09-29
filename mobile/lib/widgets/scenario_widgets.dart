import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/time/api_date.dart';
import 'glass_card.dart';
import 'score_pill.dart' show formatPrice;

/// Khối UI + model cho dữ liệu Máy nhận kịch bản V2 trên màn chi tiết mã.
/// Nguồn dữ liệu: GET /stocks/{symbol}/kich-ban (KichBanTheoSymbolDto).
/// Quy ước màu trạng thái: đã kích hoạt = xanh, đang theo dõi = cam,
/// đang hình thành = xám, hủy lệnh = đỏ, chốt lời = xanh dương.

// ─────────────────────────────────────────────────────────────────────────────
// Model — parse cục bộ từ JSON camelCase của backend
// ─────────────────────────────────────────────────────────────────────────────

class KichBanTheoSymbol {
  const KichBanTheoSymbol({required this.symbol, required this.danhSach});

  final String symbol;
  final List<KichBanChiTiet> danhSach;

  factory KichBanTheoSymbol.fromJson(Map<String, dynamic> json) => KichBanTheoSymbol(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        danhSach: (json['danhSachKichBan'] as List<dynamic>? ?? [])
            .map((e) => KichBanChiTiet.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  /// Bản sao đã sắp theo thứ tự ưu tiên hiển thị: kịch bản hành động được trước.
  List<KichBanChiTiet> get danhSachUuTien {
    final ds = [...danhSach];
    ds.sort((a, b) {
      final theoTrangThai =
          KichBanMau.uuTien(a.trangThai).compareTo(KichBanMau.uuTien(b.trangThai));
      if (theoTrangThai != 0) return theoTrangThai;
      return b.mucHoanThien.compareTo(a.mucHoanThien);
    });
    return ds;
  }
}

class KichBanChiTiet {
  const KichBanChiTiet({
    required this.loai,
    required this.ten,
    required this.trangThai,
    required this.mucHoanThien,
    required this.datBoiCanh,
    required this.datHinhThai,
    required this.datCoKichHoat,
    required this.ngayDanhGia,
    this.thoiGianKichHoat,
    this.bangChung = const [],
    this.keHoach,
    this.diemXepHang,
  });

  final String loai;
  final String ten;
  final String trangThai;
  final int mucHoanThien;
  final bool datBoiCanh;
  final bool datHinhThai;
  final bool datCoKichHoat;
  final String ngayDanhGia;
  final String? thoiGianKichHoat;
  final List<BangChungKichBan> bangChung;
  final KeHoachKichBan? keHoach;
  final double? diemXepHang;

  factory KichBanChiTiet.fromJson(Map<String, dynamic> json) => KichBanChiTiet(
        loai: json['loaiKichBan']?.toString() ?? '',
        ten: json['tenKichBan'] as String? ?? '',
        trangThai: json['trangThai'] as String? ?? '',
        mucHoanThien: _soNguyen(json['mucHoanThien']),
        datBoiCanh: json['datBoiCanh'] as bool? ?? false,
        datHinhThai: json['datHinhThai'] as bool? ?? false,
        datCoKichHoat: json['datCoKichHoat'] as bool? ?? false,
        ngayDanhGia: json['ngayDanhGia']?.toString() ?? '',
        thoiGianKichHoat: json['thoiGianKichHoat']?.toString(),
        bangChung: (json['bangChung'] as List<dynamic>? ?? [])
            .map((e) => BangChungKichBan.fromJson(e as Map<String, dynamic>))
            .toList(),
        keHoach: json['keHoachGiaoDich'] == null
            ? null
            : KeHoachKichBan.fromJson(json['keHoachGiaoDich'] as Map<String, dynamic>),
        diemXepHang: _soThuc(json['diemXepHang']),
      );

  /// Tên hiển thị tiếng Việt — ưu tiên tenKichBan từ server, dự phòng theo loaiKichBan.
  String get tenHienThi {
    if (ten.isNotEmpty) return ten;
    const tenTheoLoai = {
      'NoHuongLen': 'Nổ hướng lên',
      'HoiHoTro': 'Hồi hỗ trợ',
      'QuetThanhKhoan': 'Quét thanh khoản',
      'KietSuc': 'Kiệt sức',
      'GayNen': 'Gãy nền',
    };
    return tenTheoLoai[loai] ?? loai;
  }

  /// Nhãn huy hiệu, ví dụ "Nổ hướng lên — Đang theo dõi 65%" hoặc "Hồi hỗ trợ — Đã kích hoạt".
  String get nhanHuyHieu {
    final nhanTrangThai = KichBanMau.nhan(trangThai);
    return KichBanMau.hienThiHoanThien(trangThai)
        ? '$tenHienThi — $nhanTrangThai $mucHoanThien%'
        : '$tenHienThi — $nhanTrangThai';
  }

  /// Chỉ hiện kế hoạch giao dịch khi kịch bản đã kích hoạt (hoặc đang giữ vị thế) và có kế hoạch.
  bool get hienKeHoach =>
      keHoach != null && (trangThai == 'DaKichHoat' || trangThai == 'DangGiu');
}

class BangChungKichBan {
  const BangChungKichBan({
    required this.vaiTro,
    required this.moTa,
    required this.giaTriThucTe,
    required this.nguong,
    required this.dat,
  });

  final String vaiTro;
  final String moTa;
  final String giaTriThucTe;
  final String nguong;
  final bool dat;

  factory BangChungKichBan.fromJson(Map<String, dynamic> json) => BangChungKichBan(
        vaiTro: json['vaiTro'] as String? ?? '',
        moTa: json['moTa'] as String? ?? '',
        giaTriThucTe: json['giaTriThucTe']?.toString() ?? '',
        nguong: json['nguong']?.toString() ?? '',
        dat: json['dat'] as bool? ?? false,
      );

  /// Thứ tự hiển thị nhóm bằng chứng: 3 giai đoạn rồi tới rủi ro.
  static const thuTuVaiTro = ['BoiCanh', 'HinhThai', 'CoKichHoat', 'RuiRo', 'XacNhan'];

  static const _tenTheoVaiTro = {
    'BoiCanh': 'Bối cảnh',
    'HinhThai': 'Hình thái',
    'CoKichHoat': 'Cò kích hoạt',
    'RuiRo': 'Rủi ro / Thoát',
    'XacNhan': 'Xác nhận',
  };

  static String tenVaiTro(String vaiTro) => _tenTheoVaiTro[vaiTro] ?? vaiTro;
}

class KeHoachKichBan {
  const KeHoachKichBan({
    required this.giaVaoLenhMin,
    required this.giaVaoLenhMax,
    required this.giaDungLo,
    required this.giaChotLoi1,
    required this.giaChotLoi2,
    required this.tyLeLaiLo,
    required this.dieuKienHuy,
  });

  final double giaVaoLenhMin;
  final double giaVaoLenhMax;
  final double giaDungLo;
  final double giaChotLoi1;
  final double giaChotLoi2;
  final double tyLeLaiLo;
  final String dieuKienHuy;

  factory KeHoachKichBan.fromJson(Map<String, dynamic> json) => KeHoachKichBan(
        giaVaoLenhMin: _soThuc(json['giaVaoLenhMin']) ?? 0,
        giaVaoLenhMax: _soThuc(json['giaVaoLenhMax']) ?? 0,
        giaDungLo: _soThuc(json['giaDungLo']) ?? 0,
        giaChotLoi1: _soThuc(json['giaChotLoi1']) ?? 0,
        giaChotLoi2: _soThuc(json['giaChotLoi2']) ?? 0,
        tyLeLaiLo: _soThuc(json['tyLeLaiLo']) ?? 0,
        dieuKienHuy: json['dieuKienHuy'] as String? ?? '',
      );

  /// Vùng vào lệnh "min – max" (nếu min == max chỉ hiện một giá).
  String get vungVaoLenh {
    if (giaVaoLenhMin <= 0 && giaVaoLenhMax <= 0) return '—';
    if (giaVaoLenhMax > giaVaoLenhMin) {
      return '${formatPrice(giaVaoLenhMin)} – ${formatPrice(giaVaoLenhMax)}';
    }
    return formatPrice(giaVaoLenhMax > 0 ? giaVaoLenhMax : giaVaoLenhMin);
  }
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
// Màu sắc + nhãn trạng thái kịch bản
// ─────────────────────────────────────────────────────────────────────────────

/// Màu cố định cho từng trạng thái kịch bản (đọc rõ ở cả dark/light).
class KichBanMau {
  static const mauXanh = Color(0xFF22C55E); // đã kích hoạt / đang giữ
  static const mauCam = Color(0xFFF59E0B); // đang theo dõi
  static const mauXam = Color(0xFF9CA3AF); // đang hình thành / thoát lệnh
  static const mauDo = Color(0xFFEF4444); // hủy lệnh
  static const mauXanhDuong = Color(0xFF3B82F6); // chốt lời

  static Color cua(String trangThai) {
    switch (trangThai) {
      case 'DaKichHoat':
      case 'DangGiu':
        return mauXanh;
      case 'DangTheoDoi':
        return mauCam;
      case 'DangHinhThanh':
        return mauXam;
      case 'ChotLoi':
        return mauXanhDuong;
      case 'HuyLenh':
        return mauDo;
      case 'ThoatLenh':
        return mauXam;
      default:
        return mauXam;
    }
  }

  static String nhan(String trangThai) {
    switch (trangThai) {
      case 'DangTheoDoi':
        return 'Đang theo dõi';
      case 'DangHinhThanh':
        return 'Đang hình thành';
      case 'DaKichHoat':
        return 'Đã kích hoạt';
      case 'DangGiu':
        return 'Đang giữ';
      case 'ChotLoi':
        return 'Chốt lời';
      case 'HuyLenh':
        return 'Hủy lệnh';
      case 'ThoatLenh':
        return 'Thoát lệnh';
      default:
        return trangThai;
    }
  }

  /// Chỉ hiện % hoàn thiện trên huy hiệu khi kịch bản chưa kích hoạt.
  static bool hienThiHoanThien(String trangThai) =>
      trangThai == 'DangTheoDoi' || trangThai == 'DangHinhThanh';

  /// Thứ tự ưu tiên hiển thị: kịch bản cần hành động trước, kịch bản cũ sau.
  static int uuTien(String trangThai) {
    const thuTu = {
      'DaKichHoat': 0,
      'DangGiu': 1,
      'DangHinhThanh': 2,
      'DangTheoDoi': 3,
      'ChotLoi': 4,
      'HuyLenh': 5,
      'ThoatLenh': 6,
    };
    return thuTu[trangThai] ?? 99;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Huy hiệu trạng thái kịch bản (thay cho ô điểm mua V1 ở khu vực header)
// ─────────────────────────────────────────────────────────────────────────────

class ScenarioStatusBadges extends StatelessWidget {
  const ScenarioStatusBadges({super.key, required this.kichBan});

  /// Danh sách kịch bản đã sắp theo thứ tự ưu tiên (xem [KichBanTheoSymbol.danhSachUuTien]).
  final List<KichBanChiTiet> kichBan;

  @override
  Widget build(BuildContext context) {
    if (kichBan.isEmpty) return const SizedBox.shrink();
    return SizedBox(
      height: 30,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: kichBan.length,
        separatorBuilder: (_, __) => const SizedBox(width: 6),
        itemBuilder: (context, i) => _HuyHieuKichBan(kichBan: kichBan[i]),
      ),
    );
  }
}

class _HuyHieuKichBan extends StatelessWidget {
  const _HuyHieuKichBan({required this.kichBan});

  final KichBanChiTiet kichBan;

  @override
  Widget build(BuildContext context) {
    final mau = KichBanMau.cua(kichBan.trangThai);
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
            kichBan.nhanHuyHieu,
            style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: mau),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Kế hoạch giao dịch (thay cho khối mức giá tĩnh V1)
// ─────────────────────────────────────────────────────────────────────────────

class TradePlanCard extends StatelessWidget {
  const TradePlanCard({super.key, required this.kichBan});

  final KichBanChiTiet kichBan;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    // Card chỉ được dựng khi kichBan.hienKeHoach == true nên keHoach != null.
    final keHoach = kichBan.keHoach!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionTitle(
          'Kế hoạch giao dịch',
          subtitle: '${kichBan.tenHienThi} · ${KichBanMau.nhan(kichBan.trangThai)}',
        ),
        const SizedBox(height: 12),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: AppColors.surfaceLow(context),
            borderRadius: BorderRadius.circular(14),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('GIÁ VÀO LỆNH', style: labelCaps(context)),
              const SizedBox(height: 4),
              FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text(
                  keHoach.vungVaoLenh,
                  style: dataFont(context, size: 22, weight: FontWeight.w700, color: scheme.primary),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(child: _HopGia(nhan: 'DỪNG LỖ', giaTri: _gia(keHoach.giaDungLo), mau: scheme.error)),
            const SizedBox(width: 8),
            Expanded(child: _HopGia(nhan: 'CHỐT LỜI 1', giaTri: _gia(keHoach.giaChotLoi1), mau: scheme.primary)),
            const SizedBox(width: 8),
            Expanded(child: _HopGia(nhan: 'CHỐT LỜI 2', giaTri: _gia(keHoach.giaChotLoi2), mau: scheme.primary)),
          ],
        ),
        if (keHoach.tyLeLaiLo > 0) ...[
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text('R:R ', style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
              Text(
                '1 : ${keHoach.tyLeLaiLo.toStringAsFixed(1)}',
                style: dataFont(context, size: 13, weight: FontWeight.w700),
              ),
            ],
          ),
        ],
        if (keHoach.dieuKienHuy.isNotEmpty) ...[
          const SizedBox(height: 10),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: scheme.outlineVariant),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Padding(
                  padding: EdgeInsets.only(top: 1),
                  child: Icon(Icons.gpp_maybe_outlined, size: 16, color: KichBanMau.mauCam),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('ĐIỀU KIỆN HỦY', style: labelCaps(context).copyWith(fontSize: 9)),
                      const SizedBox(height: 3),
                      Text(
                        keHoach.dieuKienHuy,
                        style: TextStyle(fontSize: 12, height: 1.4, color: scheme.onSurface),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }

  static String _gia(double v) => v > 0 ? formatPrice(v) : '—';
}

class _HopGia extends StatelessWidget {
  const _HopGia({required this.nhan, required this.giaTri, this.mau});

  final String nhan;
  final String giaTri;
  final Color? mau;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
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
            child: Text(nhan, style: labelCaps(context).copyWith(fontSize: 9)),
          ),
          const SizedBox(height: 4),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              giaTri,
              style: dataFont(context, size: 15, weight: FontWeight.w700, color: mau ?? scheme.onSurface),
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Bằng chứng kịch bản (thay cho bảng điểm tiêu chí V1)
// ─────────────────────────────────────────────────────────────────────────────

class ScenarioEvidenceCard extends StatelessWidget {
  const ScenarioEvidenceCard({super.key, required this.kichBan});

  /// Danh sách kịch bản đã sắp theo thứ tự ưu tiên.
  final List<KichBanChiTiet> kichBan;

  @override
  Widget build(BuildContext context) {
    if (kichBan.isEmpty) return const SizedBox.shrink();
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SectionTitle(
          'Bằng chứng kịch bản',
          subtitle: 'Đối chiếu điều kiện từng kịch bản — nhấn để mở rộng',
        ),
        const SizedBox(height: 4),
        ...List.generate(kichBan.length, (i) => Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (i > 0)
                  Divider(height: 24, color: scheme.outlineVariant.withValues(alpha: 0.5)),
                _KichBanSection(kichBan: kichBan[i], moMacDinh: i == 0),
              ],
            )),
      ],
    );
  }
}

class _KichBanSection extends StatefulWidget {
  const _KichBanSection({required this.kichBan, required this.moMacDinh});

  final KichBanChiTiet kichBan;
  final bool moMacDinh;

  @override
  State<_KichBanSection> createState() => _KichBanSectionState();
}

class _KichBanSectionState extends State<_KichBanSection> {
  late var _mo = widget.moMacDinh;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final kb = widget.kichBan;
    final mau = KichBanMau.cua(kb.trangThai);
    final dongThoiGian = _thoiGianDanhGia(kb);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        InkWell(
          onTap: () => setState(() => _mo = !_mo),
          borderRadius: BorderRadius.circular(12),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(color: mau, shape: BoxShape.circle),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        kb.tenHienThi,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                      ),
                    ),
                    if (kb.diemXepHang != null && kb.diemXepHang! > 0) ...[
                      const SizedBox(width: 6),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
                        decoration: BoxDecoration(
                          color: AppColors.neutralBg(context),
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: Text(
                          'Xếp hạng ${kb.diemXepHang!.toStringAsFixed(0)}',
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight: FontWeight.w600,
                            color: scheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(width: 6),
                    _HuyHieuTrangThai(nhan: KichBanMau.nhan(kb.trangThai), mau: mau),
                    Icon(
                      _mo ? Icons.expand_less : Icons.expand_more,
                      size: 20,
                      color: scheme.onSurfaceVariant,
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(4),
                        child: LinearProgressIndicator(
                          value: (kb.mucHoanThien / 100).clamp(0.0, 1.0),
                          minHeight: 5,
                          backgroundColor: scheme.surfaceContainerHighest,
                          valueColor: AlwaysStoppedAnimation(mau),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      '${kb.mucHoanThien}%',
                      style: dataFont(context, size: 11, weight: FontWeight.w700, color: mau),
                    ),
                  ],
                ),
                if (dongThoiGian.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Text(dongThoiGian, style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
                ],
                const SizedBox(height: 6),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    _GiaiDoanChip(nhan: 'Bối cảnh', dat: kb.datBoiCanh),
                    _GiaiDoanChip(nhan: 'Hình thái', dat: kb.datHinhThai),
                    _GiaiDoanChip(nhan: 'Cò kích hoạt', dat: kb.datCoKichHoat),
                  ],
                ),
              ],
            ),
          ),
        ),
        if (_mo) ...[
          const SizedBox(height: 4),
          _DanhSachBangChung(bangChung: kb.bangChung),
        ],
      ],
    );
  }

  /// Dòng "Đánh giá 29/09/2026 · Kích hoạt 29/09/2026 09:15" (bỏ phần rỗng).
  static String _thoiGianDanhGia(KichBanChiTiet kb) {
    final phan = <String>[];
    if (kb.ngayDanhGia.isNotEmpty) {
      phan.add('Đánh giá ${formatApiDateVietnam(kb.ngayDanhGia)}');
    }
    final thoiGianKichHoat = kb.thoiGianKichHoat;
    if (thoiGianKichHoat != null && thoiGianKichHoat.isNotEmpty) {
      phan.add('Kích hoạt ${formatApiDateTime(thoiGianKichHoat)}');
    }
    return phan.join(' · ');
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

class _GiaiDoanChip extends StatelessWidget {
  const _GiaiDoanChip({required this.nhan, required this.dat});

  final String nhan;
  final bool dat;

  @override
  Widget build(BuildContext context) {
    // Giai đoạn chưa đạt chỉ là "chờ" — dùng màu xám, không dùng đỏ để tránh gây cảm giác lỗi.
    final mau = dat ? KichBanMau.mauXanh : Theme.of(context).colorScheme.onSurfaceVariant;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
      decoration: BoxDecoration(
        color: mau.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(dat ? Icons.check_circle : Icons.radio_button_unchecked, size: 12, color: mau),
          const SizedBox(width: 4),
          Text(nhan, style: TextStyle(fontSize: 10, fontWeight: FontWeight.w600, color: mau)),
        ],
      ),
    );
  }
}

class _DanhSachBangChung extends StatelessWidget {
  const _DanhSachBangChung({required this.bangChung});

  final List<BangChungKichBan> bangChung;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (bangChung.isEmpty) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Text(
          'Không có bằng chứng chi tiết cho kịch bản này.',
          style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
        ),
      );
    }

    // Nhóm theo vai trò, giữ đúng thứ tự 3 giai đoạn rồi tới rủi ro; vai trò lạ xếp cuối.
    final nhom = <String, List<BangChungKichBan>>{};
    for (final bc in bangChung) {
      nhom.putIfAbsent(bc.vaiTro, () => []).add(bc);
    }
    final thuTu = [
      ...BangChungKichBan.thuTuVaiTro.where(nhom.containsKey),
      ...nhom.keys.where((k) => !BangChungKichBan.thuTuVaiTro.contains(k)),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final vaiTro in thuTu) ...[
          Padding(
            padding: const EdgeInsets.only(top: 4, bottom: 6),
            child: Text(
              BangChungKichBan.tenVaiTro(vaiTro),
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                letterSpacing: 0.4,
                color: scheme.onSurfaceVariant,
              ),
            ),
          ),
          ...nhom[vaiTro]!.map((bc) => _DongBangChung(bangChung: bc)),
        ],
      ],
    );
  }
}

class _DongBangChung extends StatelessWidget {
  const _DongBangChung({required this.bangChung});

  final BangChungKichBan bangChung;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final mau = bangChung.dat ? KichBanMau.mauXanh : KichBanMau.mauDo;
    final doiChieu = [
      if (bangChung.giaTriThucTe.isNotEmpty) 'Thực tế: ${bangChung.giaTriThucTe}',
      if (bangChung.nguong.isNotEmpty) 'Ngưỡng: ${bangChung.nguong}',
    ].join(' · ');

    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.surfaceLow(context),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(top: 1),
            child: Icon(bangChung.dat ? Icons.check_circle : Icons.cancel, size: 14, color: mau),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  bangChung.moTa,
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, height: 1.35),
                ),
                if (doiChieu.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.only(top: 3),
                    child: Text(
                      doiChieu,
                      style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Trạng thái trống — chưa có đánh giá kịch bản
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
