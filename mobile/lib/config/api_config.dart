import 'package:flutter/foundation.dart';

/// API / Hub / Sync — khớp mobile web.
class ApiConfig {
  static const String productionHost = 'http://103.226.248.6';
  static const String defaultSyncKey = 'dev-sync-key-change-me';

  static String get baseUrl {
    const fromEnv = String.fromEnvironment('API_BASE');
    if (fromEnv.isNotEmpty) return fromEnv;
    if (kIsWeb) return '/api/v1';
    return '$productionHost/api/v1';
  }

  static String get syncApiKey =>
      const String.fromEnvironment('SYNC_API_KEY', defaultValue: defaultSyncKey);

  /// Origin từ API_BASE hoặc productionHost.
  static String get host {
    const fromEnv = String.fromEnvironment('API_BASE');
    final raw = fromEnv.isNotEmpty ? fromEnv : '$productionHost/api/v1';
    if (!raw.startsWith('http')) return productionHost;
    final uri = Uri.parse(raw);
    return '${uri.scheme}://${uri.host}${uri.hasPort ? ':${uri.port}' : ''}';
  }

  static String get hubUrl => '$host/hubs/market';
}
