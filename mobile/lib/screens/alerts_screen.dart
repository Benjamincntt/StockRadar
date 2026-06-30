import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class AlertsScreen extends StatefulWidget {
  const AlertsScreen({super.key});

  @override
  State<AlertsScreen> createState() => _AlertsScreenState();
}

class _AlertsScreenState extends State<AlertsScreen> {
  ApiClient get _api => context.read<ApiClient>();
  var _category = 'All';
  List<AlertItem> _alerts = [];
  IntradayMonitorStatus? _monitor;
  var _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _api.getAlerts(category: _category),
        _api.getIntradayMonitorStatus(),
      ]);
      setState(() {
        _alerts = results[0] as List<AlertItem>;
        _monitor = results[1] as IntradayMonitorStatus;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    if (_loading) return const LoadingView();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
        children: [
          if (_monitor != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: GlassCard(
                padding: const EdgeInsets.all(12),
                child: Text(_monitor!.status, style: const TextStyle(fontSize: 12)),
              ),
            ),
          Wrap(
            spacing: 8,
            children: ['All', 'Buy', 'Sell'].map((c) {
              final active = _category == c;
              return FilterChip(
                label: Text(c),
                selected: active,
                onSelected: (_) {
                  setState(() => _category = c);
                  _load();
                },
                selectedColor: scheme.primary.withValues(alpha: 0.2),
              );
            }).toList(),
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          ..._alerts.map((a) => Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: GlassCard(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        width: a.isMaster ? 5 : 4,
                        height: 56,
                        color: a.isBuy ? scheme.primary : scheme.error,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Text(a.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                                if (a.isMaster) ...[
                                  const SizedBox(width: 6),
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                    decoration: BoxDecoration(
                                      color: scheme.primary.withValues(alpha: 0.2),
                                      borderRadius: BorderRadius.circular(4),
                                    ),
                                    child: const Text('MASTER', style: TextStyle(fontSize: 9, fontWeight: FontWeight.bold)),
                                  ),
                                ],
                                const Spacer(),
                                Text(formatAlertTime(a.createdAt), style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
                              ],
                            ),
                            Text(a.title, style: const TextStyle(fontSize: 13)),
                            if (a.message.isNotEmpty)
                              Text(a.message, maxLines: 3, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              )),
        ],
      ),
    );
  }
}
