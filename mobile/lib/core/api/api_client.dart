import 'dart:convert';
import 'dart:io';

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

  static String friendlyMessage(String? raw, int? statusCode) {
    final text = raw?.trim() ?? '';
    if (text.isEmpty) {
      return _fallbackForStatus(statusCode);
    }
    final lower = text.toLowerCase();
    if (lower.contains('an unexpected error occurred') ||
        lower == 'internal server error' ||
        lower.contains('connection timeout') ||
        lower.contains('sql')) {
      return 'Server hoặc database tạm thời không phản hồi. Thử lại sau vài giây.';
    }
    return text;
  }

  static String _fallbackForStatus(int? statusCode) {
    final code = statusCode;
    if (code == null) return 'Không tải được dữ liệu từ server.';
    if (code == 404) return 'Không tìm thấy dữ liệu. Kiểm tra mã cổ phiếu hoặc API.';
    if (code == 401 || code == 403) {
      return 'Phiên đăng nhập hết hạn hoặc không có quyền truy cập.';
    }
    if (code >= 500) return 'Server lỗi tạm thời. Thử lại sau.';
    return 'Không tải được dữ liệu từ server.';
  }

  ApiException _apiError(http.Response response) {
    final code = response.statusCode;
    if (response.body.isNotEmpty) {
      try {
        final body = jsonDecode(response.body);
        if (body is Map<String, dynamic>) {
          final detail = body['detail'] as String?;
          final title = body['title'] as String?;
          final message = body['message'] as String?;
          if (detail != null && detail.isNotEmpty) {
            return ApiException(friendlyMessage(detail, code), code);
          }
          if (title != null && title.isNotEmpty && title != 'Not Found') {
            return ApiException(friendlyMessage(title, code), code);
          }
          if (message != null && message.isNotEmpty) {
            return ApiException(friendlyMessage(message, code), code);
          }
        }
      } catch (_) {}
    }
    if (code == 404) {
      return ApiException(
        'Không tìm thấy dữ liệu (404). Kiểm tra mã cổ phiếu hoặc API ${ApiConfig.baseUrl}',
        code,
      );
    }
    return ApiException(_fallbackForStatus(code), code);
  }

  Future<Map<String, dynamic>> _decode(http.Response response) async {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.statusCode == 204 || response.body.isEmpty) return {};
      return jsonDecode(response.body) as Map<String, dynamic>;
    }
    throw _apiError(response);
  }

  Future<List<dynamic>> _decodeList(http.Response response) async {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return jsonDecode(response.body) as List<dynamic>;
    }
    throw _apiError(response);
  }

  Future<T> _request<T>(
    String method,
    String path, {
    Map<String, dynamic>? body,
    Map<String, String>? query,
    Map<String, String>? headers,
    T Function(Map<String, dynamic> json)? map,
    T Function(List<dynamic> json)? mapList,
  }) async {
    final reqHeaders = <String, String>{'Accept': 'application/json'};
    if (_token != null && _token!.isNotEmpty) {
      reqHeaders['Authorization'] = 'Bearer $_token';
    }
    if (headers != null) reqHeaders.addAll(headers);
    if (body != null) reqHeaders['Content-Type'] = 'application/json';

    final uri = _uri(path, query);
    late final http.Response streamed;
    try {
      final response = await _client.send(
        http.Request(method, uri)
          ..headers.addAll(reqHeaders)
          ..body = body == null ? '' : jsonEncode(body),
      );
      streamed = await http.Response.fromStream(response);
    } on SocketException catch (e) {
      throw ApiException('Không kết nối được server (${ApiConfig.baseUrl}): ${e.message}');
    } on HttpException catch (e) {
      throw ApiException('Lỗi mạng: ${e.message}');
    }

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

  Future<RadarLiveSnapshot> getRadarLive({
    int minSessionVolume = 1000000,
    double minAbsChangePercent = 3,
    String direction = 'All',
  }) =>
      _request(
        'GET',
        '/radar-items/live',
        query: {
          'minSessionVolume': minSessionVolume.toString(),
          'minAbsChangePercent': minAbsChangePercent.toString(),
          'direction': direction,
        },
        map: RadarLiveSnapshot.fromJson,
      );

  Future<IntradayMonitorStatus> getIntradayMonitorStatus() => _request(
        'GET',
        '/market/intraday-monitor',
        map: IntradayMonitorStatus.fromJson,
      );

  Future<List<TradeEvent>> getTradeEvents({int limit = 40, String? label}) => _request(
        'GET',
        '/market/trades',
        query: {
          'limit': limit.toString(),
          if (label != null && label.isNotEmpty) 'label': label,
        },
        mapList: (list) =>
            list.map((e) => TradeEvent.fromJson(e as Map<String, dynamic>)).toList(),
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

  Future<MarketRegimeInfo> getReversalMarketRegime() => _request(
        'GET',
        '/reversal-bounce/market-regime',
        map: MarketRegimeInfo.fromJson,
      );

  Future<ReversalCandidateList> getReversalCandidates({
    String? date,
    String? stage,
    bool? actionableOnly,
    int page = 1,
    int pageSize = 40,
  }) =>
      _request(
        'GET',
        '/reversal-bounce/candidates',
        query: {
          if (date != null && date.isNotEmpty) 'date': date,
          if (stage != null && stage.isNotEmpty) 'stage': stage,
          if (actionableOnly != null) 'actionableOnly': actionableOnly.toString(),
          'page': page.toString(),
          'pageSize': pageSize.toString(),
        },
        map: ReversalCandidateList.fromJson,
      );

  Future<ReversalCandidateDetail> getReversalCandidateDetail(
    String symbol, {
    int lookback = 30,
  }) =>
      _request(
        'GET',
        '/reversal-bounce/candidates/${Uri.encodeComponent(symbol)}',
        query: {'lookback': lookback.toString()},
        map: ReversalCandidateDetail.fromJson,
      );

  Future<StockDetail> getStockDetail(String symbol) => _request(
        'GET',
        '/stocks/$symbol',
        map: StockDetail.fromJson,
      );

  Future<List<StockSearchHit>> searchStocks(String query, {int limit = 10}) async {
    final q = query.trim();
    if (q.isEmpty) return [];
    return _request(
      'GET',
      '/market/stock-search',
      query: {'q': q, 'limit': limit.toString()},
      mapList: (list) => list.map((e) => StockSearchHit.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }

  Future<StockChart> getStockChart(String symbol, {String interval = '1D'}) => _request(
        'GET',
        '/stocks/$symbol/chart',
        query: {'interval': interval},
        map: StockChart.fromJson,
      );

  Future<VnIndexChartSnapshot> getVnIndexChart({int sessions = 90}) => _request(
        'GET',
        '/market/vnindex/chart',
        query: {'sessions': sessions.toString()},
        map: VnIndexChartSnapshot.fromJson,
      );

  Future<DailyAnalysisResult> runOpportunityAnalysis() => _request(
        'POST',
        '/opportunities/run-analysis',
        map: DailyAnalysisResult.fromJson,
      );

  Future<List<QuoteTick>> getQuoteSnapshot() => _request(
        'GET',
        '/market/quotes',
        mapList: (list) => list.map((e) => QuoteTick.fromJson(e as Map<String, dynamic>)).toList(),
      );

  Future<List<SparklineSeries>> getSparklines(List<String> symbols) async {
    if (symbols.isEmpty) return [];
    return _request(
      'GET',
      '/market/sparklines',
      query: {'symbols': symbols.join(',')},
      mapList: (list) => list.map((e) => SparklineSeries.fromJson(e as Map<String, dynamic>)).toList(),
    );
  }

  Future<Job1Status> getJob1Status() => _request(
        'GET',
        '/market/jobs/history/status',
        map: Job1Status.fromJson,
      );

  Future<Job1Result> runJob1Fast() => _request(
        'POST',
        '/market/jobs/history',
        body: {'mode': 'fast'},
        headers: {'X-Sync-Key': ApiConfig.syncApiKey},
        map: Job1Result.fromJson,
      );

  Future<Job1Result> runJob1Night() => _request(
        'POST',
        '/market/jobs/history/night',
        body: {'mode': 'night'},
        headers: {'X-Sync-Key': ApiConfig.syncApiKey},
        map: Job1Result.fromJson,
      );

  Future<OpportunityPerformanceSummary> getPerformanceSummary() => _request(
        'GET',
        '/performance/summary',
        map: OpportunityPerformanceSummary.fromJson,
      );

  Future<AlertHistoryResponse> getAlertHistory({
    int limit = 50,
    int skip = 0,
    String? status,
    String? alertType,
    String kind = 'buy',
    String? from,
    String? to,
  }) {
    final query = <String, String>{
      'limit': limit.toString(),
      'skip': skip.toString(),
      'kind': kind,
    };
    if (status != null && status.isNotEmpty) query['status'] = status;
    if (alertType != null && alertType.isNotEmpty) query['alertType'] = alertType;
    if (from != null && from.isNotEmpty) query['from'] = from;
    if (to != null && to.isNotEmpty) query['to'] = to;

    return _request(
      'GET',
      '/performance/alert-history',
      query: query,
      map: AlertHistoryResponse.fromJson,
    );
  }

  Future<AlertHistoryTrendsResponse> getAlertHistoryTrends({
    String period = 'week',
    String kind = 'buy',
    int limit = 12,
    String? selectedPeriodStart,
  }) {
    final query = <String, String>{
      'period': period,
      'kind': kind,
      'limit': limit.toString(),
    };
    if (selectedPeriodStart != null && selectedPeriodStart.isNotEmpty) {
      query['selectedPeriodStart'] = selectedPeriodStart;
    }

    return _request(
      'GET',
      '/performance/alert-history/trends',
      query: query,
      map: AlertHistoryTrendsResponse.fromJson,
    );
  }

  Future<SmartMoneyBacktestResult> runSmartMoneyBacktest({
    int days = 90,
    int maxPicksPerDay = 10,
    int holdSessions = 5,
    String mode = 'relaxed',
    int? minScore,
  }) {
    final query = <String, String>{
      'days': days.toString(),
      'maxPicksPerDay': maxPicksPerDay.toString(),
      'holdSessions': holdSessions.toString(),
      'mode': mode,
    };
    if (minScore != null) query['minScore'] = minScore.toString();

    return _request(
      'GET',
      '/backtest/smartmoney',
      query: query,
      map: SmartMoneyBacktestResult.fromJson,
    ).timeout(
      const Duration(minutes: 5),
      onTimeout: () => throw ApiException('Backtest quá thời gian chờ (5 phút).'),
    );
  }

  Future<List<String>> getSectorCatalog() => _request(
        'GET',
        '/sectors/catalog',
        mapList: (rows) => rows
            .map((e) => (e as Map<String, dynamic>)['name'] as String? ?? '')
            .where((s) => s.isNotEmpty)
            .toList(),
      );

  Future<void> updateStockSector(String symbol, String sector) async {
    await _request<Map<String, dynamic>>(
      'PATCH',
      '/stocks/$symbol/sector',
      body: {'sector': sector},
    );
  }

  Future<void> addTradeJournalEntry({
    required String symbol,
    required String action,
    double? sizePercent,
    String? engineVerdict,
    double? buyScore,
    double? predictedHit,
    String? setupDna,
  }) async {
    await _request<Map<String, dynamic>>(
      'POST',
      '/trade-journal',
      body: {
        'symbol': symbol,
        'action': action,
        if (sizePercent != null) 'sizePercent': sizePercent,
        if (engineVerdict != null) 'engineVerdict': engineVerdict,
        if (buyScore != null) 'buyScore': buyScore,
        if (predictedHit != null) 'predictedHit': predictedHit,
        if (setupDna != null) 'setupDna': setupDna,
      },
    );
  }
}
