import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../widgets/glass_card.dart';
import '../widgets/pushed_page_scaffold.dart';

class JobsScreen extends StatefulWidget {
  const JobsScreen({super.key});

  @override
  State<JobsScreen> createState() => _JobsScreenState();
}

class _JobsScreenState extends State<JobsScreen> {
  ApiClient get _api => context.read<ApiClient>();
  Job1Status? _status;
  String? _running;
  String? _lastResult;
  String? _error;
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    _refresh();
    _pollTimer = Timer.periodic(const Duration(seconds: 15), (_) => _refresh());
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    try {
      final s = await _api.getJob1Status();
      if (!mounted) return;
      setState(() {
        _status = s;
        if (!s.isRunning) _running = null;
      });
      if (s.isRunning) {
        _pollTimer?.cancel();
        _pollTimer = Timer.periodic(const Duration(seconds: 3), (_) => _refresh());
      }
    } catch (_) {
      if (mounted) setState(() => _status = null);
    }
  }

  Future<void> _runJob(String mode) async {
    setState(() {
      _running = mode;
      _error = null;
      _lastResult = null;
    });
    try {
      final result = mode == 'fast' ? await _api.runJob1Fast() : await _api.runJob1Night();
      setState(() {
        _lastResult =
            'Universe: ${result.symbolsInUniverse}/${result.symbolsTotal} mã · ${result.symbolsExcluded} loại · ${result.barsWritten} nến';
      });
      await _refresh();
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Không chạy được Job 1. Kiểm tra API và SYNC_API_KEY.');
    } finally {
      setState(() => _running = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final status = _status;
    final busy = _running != null || (status?.isRunning ?? false);

    return PushedPageScaffold(
      title: 'Jobs',
      subtitle: 'Quản lý đồng bộ dữ liệu',
      padding: EdgeInsets.zero,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        children: [
        GlassCard(
          wave: true,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SectionTitle(
                'Job 1 — Universe & Backfill',
                subtitle: 'Lọc HOSE+HNX+UPCOM · TB KL ≥100k/30 phiên',
              ),
              if (status != null) ...[
                const SizedBox(height: 12),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: scheme.surface.withValues(alpha: 0.5),
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(color: scheme.outline.withValues(alpha: 0.35)),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        status.isRunning ? 'Đang chạy…' : 'Sẵn sàng',
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      if (status.isRunning && status.currentSymbol != null)
                        Text(' · ${status.currentSymbol}', style: TextStyle(color: scheme.onSurfaceVariant)),
                      if (status.isRunning)
                        Padding(
                          padding: const EdgeInsets.only(top: 4),
                          child: Text(
                            'Tiến độ: ${status.processed}/${status.total} (${status.percentComplete}%)',
                            style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                          ),
                        ),
                    ],
                  ),
                ),
              ],
              const SizedBox(height: 12),
              FilledButton(
                onPressed: busy ? null : () => _runJob('fast'),
                child: Text(_running == 'fast' ? 'Đang chạy (nhanh)…' : 'Chạy Job 1 — nhanh'),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: busy ? null : () => _runJob('night'),
                child: Text(_running == 'night' ? 'Đang chạy (đêm)…' : 'Chạy Job 1 — ban đêm'),
              ),
              const SizedBox(height: 12),
              Text(
                'Chế độ đêm dùng delay lớn hơn, phù hợp chạy lúc ít tải. Sau Job 1, chạy Job 2 + phân tích hàng ngày.',
                style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
              ),
              if (_lastResult != null) ...[
                const SizedBox(height: 8),
                Text(_lastResult!, style: TextStyle(fontSize: 13, color: scheme.primary)),
              ],
              if (_error != null) ...[
                const SizedBox(height: 8),
                Text(_error!, style: TextStyle(fontSize: 13, color: scheme.error)),
              ],
            ],
          ),
        ),
        ],
      ),
    );
  }
}
