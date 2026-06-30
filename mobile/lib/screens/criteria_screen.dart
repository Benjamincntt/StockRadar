import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../widgets/glass_card.dart';

class CriteriaScreen extends StatefulWidget {
  const CriteriaScreen({super.key});

  @override
  State<CriteriaScreen> createState() => _CriteriaScreenState();
}

class _CriteriaScreenState extends State<CriteriaScreen> {
  ApiClient get _api => context.read<ApiClient>();
  CriteriaSummary? _summary;
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
      final summary = await _api.getCriteriaSummary();
      setState(() => _summary = summary);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const LoadingView();
    final criteria = _summary?.criteria ?? [];

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
        children: [
          const SectionTitle('Phân tích chỉ báo', subtitle: 'Độ khớp T-1 · Top TA'),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          if (_summary?.statusMessage != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: GlassCard(child: Text(_summary!.statusMessage!)),
            ),
          ...criteria.map((c) => Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: GlassCard(
                  child: Row(
                    children: [
                      Expanded(child: Text(c.label, style: const TextStyle(fontWeight: FontWeight.w600))),
                      Text(
                        '${c.successRatePercent.toStringAsFixed(1)}%',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          color: Theme.of(context).colorScheme.primary,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text('n=${c.sampleCount}', style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                    ],
                  ),
                ),
              )),
          if (criteria.isEmpty)
            const GlassCard(child: Text('Chưa có dữ liệu chỉ báo.')),
        ],
      ),
    );
  }
}
