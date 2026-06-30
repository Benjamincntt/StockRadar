import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../core/theme/app_colors.dart';
import '../widgets/app_bottom_nav.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class ShellScreen extends StatelessWidget {
  const ShellScreen({super.key, required this.child, required this.navIndex});

  final Widget child;
  final int navIndex;

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthService>();
    final themeService = context.watch<ThemeService>();
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: Row(
          children: [
            Image.asset(
              themeService.isDark ? 'assets/juice-logo-dark.png' : 'assets/juice-logo.png',
              height: 32,
            ),
            const Spacer(),
            IconButton(
              icon: Icon(themeService.isDark ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
              onPressed: themeService.toggle,
            ),
            if (auth.isLoggedIn)
              IconButton(
                icon: const Icon(Icons.logout),
                onPressed: () async {
                  await auth.logout();
                  if (context.mounted) context.go('/login');
                },
              )
            else
              TextButton(onPressed: () => context.go('/login'), child: const Text('Đăng nhập')),
          ],
        ),
      ),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: AppColors.maxContentWidth),
          child: child,
        ),
      ),
      bottomNavigationBar: AppBottomNav(currentIndex: navIndex),
    );
  }
}

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  ApiClient get _api => context.read<ApiClient>();
  OpportunitiesList? _opportunities;
  List<AlertItem> _universeAlerts = [];
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
        _api.getOpportunities(),
        _api.getUniverseAlerts(),
        _api.getIntradayMonitorStatus(),
      ]);
      setState(() {
        _opportunities = results[0] as OpportunitiesList;
        _universeAlerts = results[1] as List<AlertItem>;
        _monitor = results[2] as IntradayMonitorStatus;
      });
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Không thể tải dữ liệu');
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const LoadingView();
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
        children: [
          if (_error != null) ...[
            ErrorBanner(message: _error!, onRetry: _load),
            const SizedBox(height: 12),
          ],
          if (_monitor != null) _MonitorLine(status: _monitor!),
          const SizedBox(height: 12),
          const SectionTitle('Top Opportunities', subtitle: 'Smart Money · phiên gần nhất'),
          const SizedBox(height: 8),
          ...(_opportunities?.items ?? []).map(_oppTile),
          const SizedBox(height: 16),
          const SectionTitle('Tín hiệu realtime', subtitle: 'Universe feed'),
          const SizedBox(height: 8),
          ..._universeAlerts.take(8).map(_alertTile),
        ],
      ),
    );
  }

  Widget _oppTile(Opportunity o) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: InkWell(
        onTap: () => context.push('/stocks/${o.symbol}'),
        borderRadius: BorderRadius.circular(16),
        child: GlassCard(
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Text(o.symbol, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                        const SizedBox(width: 8),
                        ScorePill(o.score),
                        const SizedBox(width: 6),
                        RecommendationBadge(o.recommendation),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(o.name, style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                    Text(o.sector, style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                  ],
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(formatPrice(o.price), style: const TextStyle(fontWeight: FontWeight.w600)),
                  ChangePill(o.changePercent),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _alertTile(AlertItem a) {
    final scheme = Theme.of(context).colorScheme;
    final isBuy = a.isBuy;
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: GlassCard(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 4,
              height: 48,
              decoration: BoxDecoration(
                color: a.isMaster ? scheme.primary : (isBuy ? scheme.primary : scheme.error),
                borderRadius: BorderRadius.circular(4),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(a.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(width: 6),
                      Text(formatAlertTime(a.createdAt), style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
                    ],
                  ),
                  Text(a.title, style: const TextStyle(fontSize: 13)),
                  if (a.message.isNotEmpty)
                    Text(a.message, maxLines: 2, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MonitorLine extends StatelessWidget {
  const _MonitorLine({required this.status});

  final IntradayMonitorStatus status;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return GlassCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          Icon(
            status.marketOpen ? Icons.radar : Icons.radar_outlined,
            size: 18,
            color: status.isStale ? scheme.error : scheme.primary,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              status.status.isNotEmpty ? status.status : 'Quét lệnh đột biến',
              style: const TextStyle(fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }
}
