/// Giỏ "Mùa Tết — mã khỏe" đã mua, từ GET /v3/mua-tet/theo-doi. Giá nghìn đồng, tiền đồng.
/// Ý nghĩa từng trường: specs/020-mua-tet/spec.md.
class MuaTetTheoDoi {
  const MuaTetTheoDoi({
    required this.vonDaMua,
    required this.giaTriHienTai,
    required this.laiLoDong,
    required this.cacMa,
    this.ngayMua,
    this.ngayPhien,
    this.laiLoTrongNgayDong,
    this.thayDoiTrongNgayPhanTram,
    this.maMua,
    this.nguonGio = 'CauHinh',
  });

  /// Nguồn của lệnh giỏ trong Nhật ký lệnh.
  static const nguonNhatKy = 'MUATET';

  final double vonDaMua;
  final double giaTriHienTai;
  final double laiLoDong;
  final List<MuaTetMaDaMua> cacMa;
  final String? ngayMua;
  final String? ngayPhien;
  final double? laiLoTrongNgayDong;
  final double? thayDoiTrongNgayPhanTram;

  /// Mã mùa gắn vào lệnh Nhật ký (ví dụ 2026-27); null khi server cũ.
  final String? maMua;

  /// 'NhatKy' = giỏ đọc từ Nhật ký lệnh; 'CauHinh' = giỏ ghi tay trên server.
  final String nguonGio;

  /// % lãi/lỗ cả giỏ so vốn; null khi giỏ trống.
  double? get laiLoPhanTram => vonDaMua > 0 ? laiLoDong / vonDaMua * 100 : null;

  factory MuaTetTheoDoi.fromJson(Map<String, dynamic> json) => MuaTetTheoDoi(
        vonDaMua: _toDouble(json['vonDaMua']) ?? 0,
        giaTriHienTai: _toDouble(json['giaTriHienTai']) ?? 0,
        laiLoDong: _toDouble(json['laiLoDong']) ?? 0,
        cacMa: (json['cacMa'] as List<dynamic>? ?? [])
            .map((e) => MuaTetMaDaMua.fromJson(e as Map<String, dynamic>))
            .toList(),
        ngayMua: json['ngayMua'] as String?,
        ngayPhien: json['ngayPhien'] as String?,
        laiLoTrongNgayDong: _toDouble(json['laiLoTrongNgayDong']),
        thayDoiTrongNgayPhanTram: _toDouble(json['thayDoiTrongNgayPhanTram']),
        maMua: json['maMua'] as String?,
        nguonGio: json['nguonGio'] as String? ?? 'CauHinh',
      );
}

/// Một mã trong giỏ Mùa Tết đã mua.
class MuaTetMaDaMua {
  const MuaTetMaDaMua({
    required this.symbol,
    required this.giaVao,
    required this.soCo,
    required this.giaHienTai,
    required this.thieuGia,
    required this.laiLoPhanTram,
    required this.laiLoDong,
    this.thayDoiTrongNgayPhanTram,
    this.laiLoTrongNgayDong,
  });

  final String symbol;
  final double giaVao;
  final int soCo;
  final double giaHienTai;
  final bool thieuGia;
  final double laiLoPhanTram;
  final double laiLoDong;
  final double? thayDoiTrongNgayPhanTram;
  final double? laiLoTrongNgayDong;

  factory MuaTetMaDaMua.fromJson(Map<String, dynamic> json) => MuaTetMaDaMua(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        giaVao: _toDouble(json['giaVao']) ?? 0,
        soCo: (_toDouble(json['soCo']) ?? 0).toInt(),
        giaHienTai: _toDouble(json['giaHienTai']) ?? 0,
        thieuGia: json['thieuGia'] as bool? ?? false,
        laiLoPhanTram: _toDouble(json['laiLoPhanTram']) ?? 0,
        laiLoDong: _toDouble(json['laiLoDong']) ?? 0,
        thayDoiTrongNgayPhanTram: _toDouble(json['thayDoiTrongNgayPhanTram']),
        laiLoTrongNgayDong: _toDouble(json['laiLoTrongNgayDong']),
      );
}

double? _toDouble(Object? v) {
  if (v == null) return null;
  if (v is num) return v.toDouble();
  return double.tryParse(v.toString());
}
