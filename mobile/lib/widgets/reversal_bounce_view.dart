import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/labels/reversal_bounce_labels.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import 'glass_card.dart';
import 'reversal_bounce_card.dart';
import 'score_pill.dart';

/// Body của tab "Sóng hồi": regime banner (sticky đầu) + filter + danh sách ứng viên.
/// Widget không tự cuộn — nhúng vào ListView của HomeScreen.
class ReversalBounceView extends StatefulWidget {
  const ReversalBounceView({super.key});

  @override
  State<ReversalBounceView> createState() => ReversalBounceViewState();
}

class ReversalBounceViewState extends State<ReversalBounceView> {
  ApiClient get _api => context.read<ApiClient>();

  static const _stageOptions = <String, String?>{
    'Tất cả': null,
    'Đang bán tháo': 'Capitulating',
    'Đang cân bằng': 'Stabilizing',
    'Đang xác nhận hồi': 'Confirmed',
    'Mất hiệu lực': 'Invalidated',
  };

  MarketRegimeInfo? _regime;
  ReversalCandidateList? _list;
  bool _loading = true;
  String? _error;
  String _stageLabel = 'Tất cả';
  bool _actionableOnly = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> reload() => _load();

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        _api.getReversalMarketRegime(),
        _api.getReversalCandidates(
          stage: _stageOptions[_stageLabel],
          actionableOnly: _actionableOnly ? true : null,
        ),
      ]);
      setState(() {
        _regime = results[0] as MarketRegimeInfo;
        _list = results[1] as ReversalCandidateList;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Không thể tải dữ liệu sóng hồi.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_regime != null) _regimeBanner(context, _regime!),
        const SizedBox(height: 12),
        _filters(context),
        const SizedBox(height: 12),
        if (_loading)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 48),
            child: Center(child: CircularProgressIndicator()),
          )
        else if (_error != null)
          ErrorBanner(message: _error!, onRetry: _load)
        else
          _content(context),
      ],
    );
  }

  Widget _regimeBanner(BuildContext context, MarketRegimeInfo r) {
    final scheme = Theme.of(context).colorScheme;
    final color = ReversalBounceLabels.regimeColor(context, r.regime);
    final allows = r.allowsCounterTrendEntry;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(shape: BoxShape.circle, color: color),
              ),
              const SizedBox(width: 8),
              Text('Trạng thái thị trường: ',
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
              Text(ReversalBounceLabels.regime(r.regime),
                  style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: color)),
              const Spacer(),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: (allows ? scheme.primary : scheme.error).withValues(alpha: 0.14),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(
                  allows ? 'Cho phép bắt đáy' : 'Chưa nên bắt đáy',
                  style: TextStyle(
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                      color: allows ? scheme.primary : scheme.error),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 14,
            runSpacing: 6,
            children: [
              _metric(context, 'VN-Index giảm', '${r.vnIndexDrawdownPercent.toStringAsFixed(1)}%'),
              _metric(context, 'Trên MA20', '${r.pctAboveMa20.toStringAsFixed(0)}%'),
              _metric(context, 'Mã sàn', '${r.floorCount}'),
              if (r.improveStreak > 0) _metric(context, 'Phiên cải thiện', '${r.improveStreak}'),
            ],
          ),
          if (r.statusMessage != null && r.statusMessage!.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(r.statusMessage!,
                style: TextStyle(fontSize: 11, height: 1.35, color: scheme.onSurfaceVariant)),
          ],
        ],
      ),
    );
  }

  Widget _metric(BuildContext context, String label, String value) {
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant)),
        const SizedBox(height: 2),
        Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
      ],
    );
  }

  Widget _filters(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FilterChips(
          options: _stageOptions.keys.toList(),
          selected: _stageLabel,
          onSelected: (label) {
            if (label == _stageLabel) return;
            setState(() => _stageLabel = label);
            _load();
          },
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            FilterChip(
              label: const Text('Chỉ mã có thể hành động', style: TextStyle(fontSize: 12)),
              selected: _actionableOnly,
              showCheckmark: true,
              onSelected: (v) {
                setState(() => _actionableOnly = v);
                _load();
              },
              selectedColor: scheme.primary.withValues(alpha: 0.18),
              backgroundColor: AppColors.surfaceLow(context),
              side: BorderSide(color: scheme.outline.withValues(alpha: 0.25)),
            ),
          ],
        ),
      ],
    );
  }

  Widget _content(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final list = _list;
    final items = list?.items ?? const <ReversalCandidate>[];
    if (items.isEmpty) {
      return GlassCard(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 16),
          child: Center(
            child: Text(
              list?.statusMessage ?? 'Chưa có ứng viên sóng hồi cho phiên này.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: scheme.onSurfaceVariant),
            ),
          ),
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final c in items) ...[
          ReversalBounceCard(candidate: c),
          const SizedBox(height: 10),
        ],
      ],
    );
  }
}
