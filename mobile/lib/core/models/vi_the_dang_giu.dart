/// Vị thế V3 đang giữ hiển thị trên Home (từ GET /v3/hieu-qua/vi-the-dang-giu).
class ViTheDangGiu {
  const ViTheDangGiu({
    required this.symbol,
    required this.loai,
    required this.giaVao,
    required this.giaChotLoi,
    required this.soPhienConLai,
    required this.soPhienDaGiu,
  });

  final String symbol;

  /// Kịch bản: KB1, KB2, KB4, N1, N3.
  final String loai;

  /// Giá mua (cùng đơn vị với giá trực tiếp, nghìn đồng); null nếu server chưa trả.
  final double? giaVao;

  /// Giá chốt lời; null với N3 (bán ATO theo lịch) hoặc khi server chưa có trường này.
  final double? giaChotLoi;

  /// Số phiên còn lại tới hạn bán; null khi server chưa có trường này.
  final int? soPhienConLai;

  final int soPhienDaGiu;

  /// Kịch bản N3 không có giá chốt lời, chỉ bán ATO theo lịch.
  bool get banTheoLich => loai == 'N3';

  /// Lãi/lỗ theo % so với giá mua; null khi thiếu giá mua hoặc giá hiện tại.
  double? laiLoPhanTram(double? giaHienTai) {
    final vao = giaVao;
    if (vao == null || vao <= 0 || giaHienTai == null || giaHienTai <= 0) return null;
    return (giaHienTai - vao) / vao * 100;
  }

  factory ViTheDangGiu.fromJson(Map<String, dynamic> json) => ViTheDangGiu(
        symbol: (json['symbol'] as String? ?? '').toUpperCase(),
        loai: json['loai'] as String? ?? '',
        giaVao: _toDouble(json['giaVao']),
        giaChotLoi: _toDouble(json['giaChotLoi']),
        soPhienConLai: _toInt(json['soPhienConLai']),
        soPhienDaGiu: _toInt(json['soPhienDaGi']) ?? 0,
      );

  static double? _toDouble(Object? v) {
    if (v == null) return null;
    if (v is num) return v.toDouble();
    return double.tryParse(v.toString());
  }

  static int? _toInt(Object? v) {
    if (v == null) return null;
    if (v is num) return v.toInt();
    return int.tryParse(v.toString());
  }
}
