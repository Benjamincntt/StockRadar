import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../config/api_config.dart';
import '../api/api_client.dart';
import '../models/models.dart';

enum LiveConnectionState { connecting, connected, reconnecting, disconnected }

/// SignalR + poll backup — khớp `LiveMarketContext` trên web.
class MarketHubService extends ChangeNotifier {
  MarketHubService(this._api);

  final ApiClient _api;
  HubConnection? _hub;
  final Map<String, QuoteTick> _quotes = {};
  final List<AlertItem> _recentAlerts = [];
  LiveConnectionState _state = LiveConnectionState.disconnected;
  String? _lastUpdated;
  final Set<String> _subscribed = {};
  Timer? _pollTimer;
  bool _started = false;

  LiveConnectionState get connectionState => _state;
  Map<String, QuoteTick> get quotes => Map.unmodifiable(_quotes);
  List<AlertItem> get recentAlerts => List.unmodifiable(_recentAlerts);
  String? get lastUpdated => _lastUpdated;

  QuoteTick? quote(String symbol) => _quotes[symbol.toUpperCase()];

  Future<void> start() async {
    if (_started) return;
    _started = true;
    await _connectHub();
    await refreshSnapshot();
    _pollTimer = Timer.periodic(const Duration(seconds: 25), (_) => refreshSnapshot());
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    _hub?.stop();
    super.dispose();
  }

  Future<void> _connectHub() async {
    _state = LiveConnectionState.connecting;
    notifyListeners();
    try {
      _hub = HubConnectionBuilder()
          .withUrl(
            ApiConfig.hubUrl,
            options: HttpConnectionOptions(
              skipNegotiation: false,
              transport: HttpTransportType.WebSockets,
            ),
          )
          .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000, 30000])
          .build();

      _hub!.on('QuotesUpdated', _onQuotes);
      _hub!.on('AlertCreated', _onAlert);

      _hub!.onreconnecting(({error}) {
        _state = LiveConnectionState.reconnecting;
        notifyListeners();
      });
      _hub!.onreconnected(({connectionId}) async {
        _state = LiveConnectionState.connected;
        notifyListeners();
        await refreshSnapshot();
        await _invokeSubscribe();
      });
      _hub!.onclose(({error}) {
        _state = LiveConnectionState.disconnected;
        notifyListeners();
      });

      await _hub!.start();
      _state = LiveConnectionState.connected;
      notifyListeners();
      await _invokeSubscribe();
    } catch (_) {
      _state = LiveConnectionState.disconnected;
      notifyListeners();
    }
  }

  void _onQuotes(List<Object?>? args) {
    if (args == null || args.isEmpty) return;
    final batch = args.first;
    if (batch is! List) return;
    for (final raw in batch) {
      if (raw is! Map) continue;
      final q = QuoteTick.fromJson(Map<String, dynamic>.from(raw));
      if (q.symbol.isEmpty || q.price <= 0) continue;
      _quotes[q.symbol] = q;
    }
    _lastUpdated = DateTime.now().toIso8601String();
    notifyListeners();
  }

  void _onAlert(List<Object?>? args) {
    if (args == null || args.isEmpty) return;
    final raw = args.first;
    if (raw is! Map) return;
    final alert = AlertItem.fromJson(Map<String, dynamic>.from(raw));
    if (_recentAlerts.any((a) => a.id == alert.id)) return;
    _recentAlerts.insert(0, alert);
    if (_recentAlerts.length > 30) _recentAlerts.removeLast();
    notifyListeners();
  }

  Future<void> refreshSnapshot() async {
    try {
      final snapshot = await _api.getQuoteSnapshot();
      for (final q in snapshot) {
        if (q.symbol.isNotEmpty && q.price > 0) _quotes[q.symbol] = q;
      }
      _lastUpdated = DateTime.now().toIso8601String();
      notifyListeners();
    } catch (_) {}
  }

  void subscribeSymbols(List<String> symbols) {
    _subscribed
      ..clear()
      ..addAll(symbols.map((s) => s.toUpperCase()).where((s) => s.isNotEmpty));
    _invokeSubscribe();
  }

  Future<void> _invokeSubscribe() async {
    if (_hub?.state != HubConnectionState.Connected || _subscribed.isEmpty) return;
    try {
      await _hub!.invoke('Subscribe', args: [_subscribed.toList()]);
    } catch (_) {}
  }
}
