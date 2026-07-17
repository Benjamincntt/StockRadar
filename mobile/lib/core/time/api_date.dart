/// Parse ISO từ API (UTC, có hoặc không có hậu tố Z).
DateTime parseApiDateUtc(String iso) {
  final trimmed = iso.trim();
  if (trimmed.isEmpty) {
    return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
  }
  final hasZone = trimmed.endsWith('Z') ||
      RegExp(r'[+-]\d{2}:?\d{2}$').hasMatch(trimmed);
  return DateTime.parse(hasZone ? trimmed : '${trimmed}Z');
}

/// Parse ISO và trả về thời điểm UTC — hỗ trợ offset +07:00 từ API.
DateTime parseApiInstant(String iso) => parseApiDateUtc(iso).toUtc();

/// Hiển thị theo giờ tường VN (+07:00), không phụ thuộc timezone máy user.
DateTime toVietnamWallClock(DateTime instant) {
  final utc = instant.toUtc();
  final vn = utc.add(const Duration(hours: 7));
  return DateTime(vn.year, vn.month, vn.day, vn.hour, vn.minute, vn.second);
}

/// Hiển thị ngày ISO (yyyy-MM-dd hoặc datetime) theo lịch VN.
String formatApiDateVietnam(String iso) {
  try {
    final trimmed = iso.trim();
    if (RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(trimmed)) {
      final parts = trimmed.split('-');
      return '${parts[2]}/${parts[1]}/${parts[0]}';
    }
    final vn = toVietnamWallClock(parseApiInstant(trimmed));
    final d = vn.day.toString().padLeft(2, '0');
    final m = vn.month.toString().padLeft(2, '0');
    return '$d/$m/${vn.year}';
  } catch (_) {
    return iso;
  }
}

/// Hiển thị ngày ISO (yyyy-MM-dd hoặc datetime).
String formatApiDate(String iso) => formatApiDateVietnam(iso);

/// Hiển thị thời gian API theo giờ VN (+07:00).
String formatApiDateTimeVietnam(String iso) {
  try {
    final vn = toVietnamWallClock(parseApiInstant(iso.trim()));
    final d = vn.day.toString().padLeft(2, '0');
    final m = vn.month.toString().padLeft(2, '0');
    final h = vn.hour.toString().padLeft(2, '0');
    final min = vn.minute.toString().padLeft(2, '0');
    return '$d/$m/${vn.year} $h:$min';
  } catch (_) {
    return iso;
  }
}

/// Hiển thị thời gian API theo giờ VN.
String formatApiDateTime(String iso) => formatApiDateTimeVietnam(iso);
