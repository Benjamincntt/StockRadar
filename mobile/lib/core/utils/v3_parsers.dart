// Hàm thuần cho UI V3/nhật ký: đọc số theo quy ước VN và bản đồ lý do thoát V3.

/// Đọc số theo cả hai quy ước: "25.5"/"25,5" → 25.5; nhóm nghìn "25.500" → 25500.
/// Giá trị không rõ ràng trả null để nơi gọi báo lỗi, không âm thầm làm tròn sai.
double? parseSoVn(String raw) {
  var text = raw.trim().replaceAll(' ', '');
  if (text.isEmpty) return null;
  var sign = 1.0;
  if (text.startsWith('-')) {
    sign = -1;
    text = text.substring(1);
  } else if (text.startsWith('+')) {
    text = text.substring(1);
  }
  final hasComma = text.contains(',');
  final hasDot = text.contains('.');
  final String normalized;
  if (hasComma && hasDot) {
    // '.' nhóm nghìn, ',' thập phân (quy ước VN).
    normalized = text.replaceAll('.', '').replaceAll(',', '.');
  } else if (hasComma) {
    normalized = text.replaceAll(',', '.');
  } else if (hasDot) {
    final lastDot = text.lastIndexOf('.');
    final decimals = text.substring(lastDot + 1);
    final phanNguyen = text.substring(0, lastDot).replaceAll('.', '');
    normalized = (decimals.length == 3 && phanNguyen.isNotEmpty)
        ? text.replaceAll('.', '')
        : text;
  } else {
    normalized = text;
  }
  final value = double.tryParse(normalized);
  return value == null ? null : sign * value;
}

/// Nhãn tiếng Việt cho lý do thoát V3 (enum LyDoThoat). Chưa thoát → rỗng; giá trị lạ → "Khác".
String v3LyDoThoatLabel(String? lyDo) => switch (lyDo) {
      'ChamMA' => 'Chạm MA',
      'ChotLoiHoangLoan' => 'Chốt +15% hoảng loạn sâu',
      'HetHanATC' => 'Hết hạn giữ (ATC)',
      'AtoTheoLich' => 'ATO đầu tháng sau',
      null => '',
      '' => '',
      'Khong' => '',
      _ => 'Khác',
    };
