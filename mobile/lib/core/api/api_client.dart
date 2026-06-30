import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../config/api_config.dart';
import '../models/models.dart';

class ApiException implements Exception {
  ApiException(this.message, [this.statusCode]);
  final String message;
  final int? statusCode;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({http.Client? client, String? token}) : _client = client ?? http.Client() {
    _token = token;
  }

  final http.Client _client;
  String? _token;

  void setToken(String? token) => _token = token;

  Uri _uri(String path, [Map<String, String>? query]) {
    final base = ApiConfig.baseUrl.endsWith('/')
        ? ApiConfig.baseUrl.substring(0, ApiConfig.baseUrl.length - 1)
        : ApiConfig.baseUrl;
    final normalized = path.startsWith('/') ? path : '/$path';
    return Uri.parse('$base$normalized').replace(queryParameters: query);
  }

  Future<Map<String, dynamic>> _decode(http.Response response) async {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.statusCode == 204 || response.body.isEmpty) return {};
      return jsonDecode(response.body) as Map<String, dynamic>;
    }
    try {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      final message = body['detail'] as String? ?? body['message'] as String? ?? 'API error ${response.statusCode}';
      throw ApiException(message, response.statusCode);
    } catch (_) {
      throw ApiException('API error ${response.statusCode}', response.statusCode);
    }
  }

  Future<List<dynamic>> _decodeList(http.Response response) async {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return jsonDecode(response.body) as List<dynamic>;
    }
    throw ApiException('API error ${response.statusCode}', response.statusCode);
  }

  Future<T> _request<T>(
    String method,
    String path, {
    Map<String, dynamic>? body,
    Map<String, String>? query,
    T Function(Map<String, dynamic> json)? map,
    T Function(List<dynamic> json)? mapList,
  }) async {
    final headers = <String, String>{'Accept': 'application/json'};
    if (_token != null && _token!.isNotEmpty) {
      headers['Authorization'] = 'Bearer $_token';
    }
    if (body != null) headers['Content-Type'] = 'application/json';

    final uri = _uri(path, query);
    final response = await _client.send(
      http.Request(method, uri)
        ..headers.addAll(headers)
        ..body = body == null ? '' : jsonEncode(body),
    );
    final streamed = await http.Response.fromStream(response);

    if (mapList != null) {
      final list = await _decodeList(streamed);
      return mapList(list);
    }
    final json = await _decode(streamed);
    if (map != null) return map(json);
    return json as T;
  }

  Future<AuthUser> login(String email, String password) => _request(
        'POST',
        '/auth/tokens',
        body: {'email': email, 'password': password},
        map: AuthUser.fromJson,
      );

  Future<AuthUser> register(String email, String password, String displayName) => _request(
        'POST',
        '/users',
        body: {'email': email, 'password': password, 'displayName': displayName},
        map: AuthUser.fromJson,
      );

  Future<OpportunitiesList> getOpportunities({int pageSize = 15}) => _request(
        'GET',
        '/opportunities',
        query: {'page': '1', 'pageSize': pageSize.toString()},
        map: OpportunitiesList.fromJson,
      );

  Future<List<AlertItem>> getAlerts({
    String category = 'All',
    String feed = 'opportunity',
    int pageSize = 30,
  }) async {
    final json = await _request<Map<String, dynamic>>(
      'GET',
      '/alerts',
      query: {
        'category': category,
        'feed': feed,
        'page': '1',
        'pageSize': pageSize.toString(),
      },
    );
    final items = json['items'] as List<dynamic>? ?? [];
    return items.map((e) => AlertItem.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<AlertItem>> getUniverseAlerts({int pageSize = 15}) =>
      getAlerts(category: 'All', feed: 'universe', pageSize: pageSize);

  Future<IntradayMonitorStatus> getIntradayMonitorStatus() => _request(
        'GET',
        '/market/intraday-monitor',
        map: IntradayMonitorStatus.fromJson,
      );

  Future<List<WatchlistItem>> getWatchlist() => _request(
        'GET',
        '/watchlist-items',
        mapList: (list) =>
            list.map((e) => WatchlistItem.fromJson(e as Map<String, dynamic>)).toList(),
      );

  Future<void> addToWatchlist(String symbol) => _request(
        'PUT',
        '/watchlist-items/$symbol',
      );

  Future<void> removeFromWatchlist(String symbol) => _request(
        'DELETE',
        '/watchlist-items/$symbol',
      );

  Future<CriteriaSummary> getCriteriaSummary() => _request(
        'GET',
        '/criteria/summary',
        map: CriteriaSummary.fromJson,
      );

  Future<StockDetail> getStockDetail(String symbol) => _request(
        'GET',
        '/stocks/$symbol',
        map: StockDetail.fromJson,
      );

  Future<StockChart> getStockChart(String symbol, {String interval = '1D'}) => _request(
        'GET',
        '/stocks/$symbol/chart',
        query: {'interval': interval},
        map: StockChart.fromJson,
      );
}
