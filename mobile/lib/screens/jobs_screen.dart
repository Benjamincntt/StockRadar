import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/labels/job_state_labels.dart';
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

  List<JobInfo>? _jobs;
  bool _loading = true;
  String? _error;
  String? _runningJobId;
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    _refresh();
    _pollTimer = Timer.periodic(const Duration(seconds: 20), (_) => _refresh(silent: true));
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    super.dispose();
  }

  Future<void> _refresh({bool silent = false}) async {
    if (!silent) setState(() => _error = null);
    try {
      final jobs = await _api.getJobs();
      if (!mounted) return;
      setState(() {
        _jobs = jobs;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        if (!silent || _jobs == null) _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        if (!silent || _jobs == null) {
          _error = 'Không tải được danh sách job. Kiểm tra kết nối API.';
        }
      });
    }
  }

  Future<void> _run(JobInfo job) async {
    if (_runningJobId != null) return; // chạy lần lượt từng job
    setState(() => _runningJobId = job.jobId);
    try {
      await _api.runJob(job.triggerEndpoint);
      if (!mounted) return;
      _snack('${job.name}: đã chạy xong.');
      await _refresh(silent: true);
    } on ApiException catch (e) {
      if (mounted) _snack('${job.name}: ${e.message}', error: true);
    } catch (_) {
      if (mounted) _snack('${job.name}: chạy thất bại. Kiểm tra API / SYNC_API_KEY.', error: true);
    } finally {
      if (mounted) setState(() => _runningJobId = null);
    }
  }

  void _snack(String message, {bool error = false}) {
    final scheme = Theme.of(context).colorScheme;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(
        content: Text(message),
        backgroundColor: error ? scheme.error : null,
      ));
  }

  @override
  Widget build(BuildContext context) {
    return PushedPageScaffold(
      title: 'Jobs',
      subtitle: 'Trạng thái pipeline & chạy thủ công',
      padding: EdgeInsets.zero,
      child: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading && _jobs == null) return const LoadingView();

    final jobs = _jobs;
    if (jobs == null) {
      return ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [ErrorBanner(message: _error ?? 'Lỗi không xác định', onRetry: _refresh)],
      );
    }

    return RefreshIndicator(
      onRefresh: () => _refresh(),
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        children: [
          Padding(
            padding: const EdgeInsets.only(bottom: 4),
            child: Text(
              'Xếp theo tần suất chạy — job chạy nhiều lần nhất ở trên.',
              style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
            ),
          ),
          for (final job in jobs) ...[
            const SizedBox(height: 12),
            _JobCard(
              job: job,
              running: _runningJobId == job.jobId,
              busy: _runningJobId != null,
              onRun: () => _run(job),
            ),
          ],
        ],
      ),
    );
  }
}

class _JobCard extends StatelessWidget {
  const _JobCard({
    required this.job,
    required this.running,
    required this.busy,
    required this.onRun,
  });

  final JobInfo job;
  final bool running;
  final bool busy;
  final VoidCallback onRun;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final style = jobStateStyle(context, job.status, running: running);

    return GlassCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(job.name,
                        style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                    const SizedBox(height: 2),
                    Text(job.schedule,
                        style: TextStyle(fontSize: 11.5, color: scheme.onSurfaceVariant)),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              _StatusBadge(style: style),
            ],
          ),
          const SizedBox(height: 8),
          Text(job.description,
              style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant, height: 1.35)),
          const SizedBox(height: 10),
          _lastRunLine(context),
          if (job.status == 'failed' && job.error != null) ...[
            const SizedBox(height: 6),
            Text(job.error!,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 11.5, color: scheme.error)),
          ] else if (job.summary != null && job.summary!.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(job.summary!, style: TextStyle(fontSize: 11.5, color: scheme.primary)),
          ],
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: running
                ? FilledButton.tonal(
                    onPressed: null,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            valueColor: AlwaysStoppedAnimation(scheme.primary),
                          ),
                        ),
                        const SizedBox(width: 10),
                        const Text('Đang chạy…'),
                      ],
                    ),
                  )
                : FilledButton.tonalIcon(
                    onPressed: (busy || !job.triggerable) ? null : onRun,
                    icon: const Icon(Icons.play_arrow_rounded, size: 20),
                    label: const Text('Chạy ngay'),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _lastRunLine(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final finished = job.lastFinishedAt;
    final parts = <String>[];
    if (finished != null) {
      parts.add('Lần cuối: ${_ago(finished)}');
      if (job.triggeredBy != null) {
        parts.add(job.triggeredBy == 'manual' ? 'thủ công' : 'theo lịch');
      }
      if (job.lastDurationMs != null) parts.add(_fmtDuration(job.lastDurationMs!));
    } else {
      parts.add('Chưa có lần chạy nào được ghi nhận');
    }
    return Text(parts.join(' · '),
        style: TextStyle(fontSize: 11.5, color: scheme.onSurfaceVariant));
  }

  static String _ago(DateTime dt) {
    final diff = DateTime.now().difference(dt);
    if (diff.inSeconds < 60) return 'vừa xong';
    if (diff.inMinutes < 60) return '${diff.inMinutes} phút trước';
    if (diff.inHours < 24) return '${diff.inHours} giờ trước';
    if (diff.inDays < 7) return '${diff.inDays} ngày trước';
    return '${dt.day}/${dt.month} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }

  static String _fmtDuration(int ms) {
    if (ms < 1000) return '${ms}ms';
    final s = ms / 1000;
    if (s < 60) return '${s.toStringAsFixed(s < 10 ? 1 : 0)}s';
    final m = ms ~/ 60000;
    final rem = (ms % 60000) ~/ 1000;
    return '${m}m${rem}s';
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.style});

  final JobStateStyle style;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: style.background,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        style.label,
        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: style.foreground),
      ),
    );
  }
}
