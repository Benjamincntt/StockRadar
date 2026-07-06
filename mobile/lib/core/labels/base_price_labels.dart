class BasePriceLabels {
  static const String base = 'Nền giá';
  static const String breakout = 'Phá vỡ nền giá';
  static const String breakUp = 'Nổ hướng lên';
  static const String breakDown = 'Gãy nền';
  static const double breakUpMinPercent = 3;

  static String resolveEventLabel(
    Map<String, dynamic> box,
    double latestPrice,
  ) {
    final eventLabel = box['eventLabel'] as String?;
    if (eventLabel != null && eventLabel.isNotEmpty) return eventLabel;

    final boxLow = (box['boxLow'] as num?)?.toDouble() ?? 0;
    final confirmed = box['isBreakoutConfirmed'] as bool? ?? false;
    final priceGain = (box['priceGainPercent'] as num?)?.toDouble() ?? 0;

    if (latestPrice < boxLow) return breakDown;
    if (confirmed && priceGain >= breakUpMinPercent) return breakUp;
    if (confirmed) return breakout;
    return base;
  }

  static String cardSubtitle(Map<String, dynamic> box, double latestPrice) {
    final confirmed = box['isBreakoutConfirmed'] as bool? ?? false;
    if (!confirmed) {
      final refPeriod = box['refBoxPeriod'] as String? ?? '';
      return 'Đang tích lũy — $refPeriod';
    }
    return resolveEventLabel(box, latestPrice);
  }
}
