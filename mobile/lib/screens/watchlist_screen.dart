import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../widgets/glass_card.dart';
import '../widgets/score_pill.dart';

class WatchlistScreen extends StatefulWidget {
  const WatchlistScreen({super.key});

  @override
  State<WatchlistScreen> createState() => _WatchlistScreenState();
}

class _WatchlistScreenState extends State<WatchlistScreen> {
  ApiClient get _api => context.read<ApiClient>();
  List<WatchlistItem> _items = [];
  final _symbolCtrl = TextEditingController();
  var _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _symbolCtrl.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await _api.getWatchlist();
      setState(() => _items = items);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _add() async {
    final sym = _symbolCtrl.text.trim().toUpperCase();
    if (sym.isEmpty) return;
    if (!context.read<AuthService>().isLoggedIn) {
      if (mounted) context.push('/login');
      return;
    }
    try {
      await _api.addToWatchlist(sym);
      _symbolCtrl.clear();
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  Future<void> _remove(String symbol) async {
    try {
      await _api.removeFromWatchlist(symbol);
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthService>();
    if (_loading) return const LoadingView();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
        children: [
          const SectionTitle('Watchlist', subtitle: 'Mã bạn đang theo dõi'),
          const SizedBox(height: 12),
          if (!auth.isLoggedIn)
            const ErrorBanner(message: 'Đăng nhập để quản lý watchlist cá nhân'),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _symbolCtrl,
                  textCapitalization: TextCapitalization.characters,
                  decoration: const InputDecoration(hintText: 'VD: FPT'),
                ),
              ),
              const SizedBox(width: 8),
              FilledButton(onPressed: _add, child: const Text('Thêm')),
            ],
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          ..._items.map((item) => Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: InkWell(
                  onTap: () => context.push('/stocks/${item.symbol}'),
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
                                  Text(item.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                                  const SizedBox(width: 8),
                                  ScorePill(item.score),
                                ],
                              ),
                              Text(item.name, style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                              Text(item.sector, style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                            ],
                          ),
                        ),
                        ChangePill(item.changePercent),
                        IconButton(
                          icon: const Icon(Icons.close, size: 20),
                          onPressed: () => _remove(item.symbol),
                        ),
                      ],
                    ),
                  ),
                ),
              )),
        ],
      ),
    );
  }
}
