import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
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
  List<String> _sectorOptions = [];
  final _symbolCtrl = TextEditingController();
  String? _editingSymbol;
  String _draftSector = '';
  var _loading = true;
  var _savingSector = false;
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
      final results = await Future.wait([
        _api.getWatchlist(),
        _api.getSectorCatalog().catchError((_) => <String>[]),
      ]);
      setState(() {
        _items = results[0] as List<WatchlistItem>;
        _sectorOptions = results[1] as List<String>;
      });
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

  void _openSectorEdit(WatchlistItem item) {
    if (item.sectorLocked) return;
    setState(() {
      _editingSymbol = item.symbol;
      _draftSector = item.sector;
    });
  }

  Future<void> _saveSector() async {
    final sym = _editingSymbol;
    if (sym == null || _draftSector.trim().isEmpty) return;
    setState(() => _savingSector = true);
    try {
      await _api.updateStockSector(sym, _draftSector.trim());
      setState(() {
        _editingSymbol = null;
        _draftSector = '';
      });
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      setState(() => _savingSector = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthService>();
    final scheme = Theme.of(context).colorScheme;
    if (_loading) return const LoadingView();

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
        children: [
          const PageHeader(
            title: 'Watchlist',
            subtitle: 'Theo dõi mã · chỉnh ngành thủ công (khóa sau khi lưu)',
          ),
          const SizedBox(height: 12),
          if (!auth.isLoggedIn)
            const Padding(
              padding: EdgeInsets.only(bottom: 12),
              child: ErrorBanner(message: 'Đăng nhập để quản lý watchlist cá nhân'),
            ),
          Container(
            padding: const EdgeInsets.all(6),
            decoration: BoxDecoration(
              color: AppColors.surfaceLowest(context),
              borderRadius: BorderRadius.circular(999),
              border: Border.all(color: scheme.outline.withValues(alpha: 0.3)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _symbolCtrl,
                    textCapitalization: TextCapitalization.characters,
                    decoration: const InputDecoration(
                      hintText: 'VD: FPT',
                      border: InputBorder.none,
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      isDense: true,
                    ),
                  ),
                ),
                FilledButton(
                  onPressed: _add,
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(72, 40),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                  ),
                  child: const Text('Thêm'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (_error != null) ErrorBanner(message: _error!, onRetry: _load),
          if (_items.isEmpty)
            GlassCard(child: Text('Watchlist trống.', style: TextStyle(color: scheme.onSurfaceVariant)))
          else ...[
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                children: [
                  Expanded(flex: 3, child: Text('MÃ', style: labelCaps(context))),
                  Expanded(flex: 2, child: Text('ĐIỂM', style: labelCaps(context))),
                  Expanded(flex: 2, child: Text('%', style: labelCaps(context), textAlign: TextAlign.end)),
                  const SizedBox(width: 36),
                ],
              ),
            ),
            ..._items.map((item) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Column(
                    children: [
                      SurfaceRow(
                        onTap: () => context.push('/stocks/${item.symbol}'),
                        child: Row(
                          children: [
                            Expanded(
                              flex: 3,
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(item.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                                  Text(item.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant)),
                                  const SizedBox(height: 2),
                                  InkWell(
                                    onTap: item.sectorLocked ? null : () => _openSectorEdit(item),
                                    child: Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        Flexible(
                                          child: Text(
                                            item.sector.isNotEmpty ? item.sector : 'Chưa phân ngành',
                                            style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                                            overflow: TextOverflow.ellipsis,
                                          ),
                                        ),
                                        if (!item.sectorLocked) ...[
                                          const SizedBox(width: 4),
                                          Icon(Icons.edit, size: 12, color: scheme.primary),
                                        ],
                                        if (item.sectorLocked)
                                          Padding(
                                            padding: const EdgeInsets.only(left: 4),
                                            child: Icon(Icons.lock, size: 11, color: scheme.onSurfaceVariant),
                                          ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            Expanded(flex: 2, child: ScorePill(item.score)),
                            Expanded(flex: 2, child: Align(alignment: Alignment.centerRight, child: ChangePill(item.changePercent))),
                            IconButton(
                              icon: Icon(Icons.close, size: 18, color: scheme.error),
                              onPressed: () => _remove(item.symbol),
                            ),
                          ],
                        ),
                      ),
                      if (_editingSymbol == item.symbol) ...[
                        const SizedBox(height: 6),
                        GlassCard(
                          padding: const EdgeInsets.all(12),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              DropdownButtonFormField<String>(
                                value: _sectorOptions.contains(_draftSector) ? _draftSector : null,
                                decoration: const InputDecoration(labelText: 'Ngành'),
                                items: _sectorOptions
                                    .map((s) => DropdownMenuItem(value: s, child: Text(s, style: const TextStyle(fontSize: 13))))
                                    .toList(),
                                onChanged: (v) => setState(() => _draftSector = v ?? ''),
                              ),
                              const SizedBox(height: 8),
                              Row(
                                children: [
                                  TextButton(onPressed: () => setState(() => _editingSymbol = null), child: const Text('Hủy')),
                                  const Spacer(),
                                  FilledButton(
                                    onPressed: _savingSector ? null : _saveSector,
                                    child: Text(_savingSector ? 'Đang lưu…' : 'Lưu ngành'),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ],
                  ),
                )),
          ],
        ],
      ),
    );
  }
}
