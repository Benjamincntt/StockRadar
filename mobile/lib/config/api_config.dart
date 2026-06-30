import 'package:flutter/foundation.dart';

/// API base — web dùng relative path qua nginx; native trỏ production.
class ApiConfig {
  static const String productionHost = 'https://stock.baobiantea.com';

  static String get baseUrl {
    const fromEnv = String.fromEnvironment('API_BASE');
    if (fromEnv.isNotEmpty) return fromEnv;
    if (kIsWeb) return '/api/v1';
    return '$productionHost/api/v1';
  }
}
