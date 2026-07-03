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

/// Hiển thị ngày ISO (yyyy-MM-dd hoặc datetime).
String formatApiDate(String iso) {
  try {
    final trimmed = iso.trim();
    if (RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(trimmed)) {
      final parts = trimmed.split('-');
      return '${parts[2]}/${parts[1]}/${parts[0]}';
    }
    final local = parseApiDateUtc(trimmed).toLocal();
    final d = local.day.toString().padLeft(2, '0');
    final m = local.month.toString().padLeft(2, '0');
    return '$d/$m/${local.year}';
  } catch (_) {
    return iso;
  }
}

/// Hiển thị thời gian API theo giờ máy (VN khi user ở VN).
String formatApiDateTime(String iso) {
  try {
    final local = parseApiDateUtc(iso).toLocal();
    final d = local.day.toString().padLeft(2, '0');
    final m = local.month.toString().padLeft(2, '0');
    final h = local.hour.toString().padLeft(2, '0');
    final min = local.minute.toString().padLeft(2, '0');
    return '$d/$m/${local.year} $h:$min';
  } catch (_) {
    return iso;
  }
}
