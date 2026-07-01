import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';

/// Ô tìm mã — pill giống watchlist, gợi ý từ `/market/stock-search`.
class StockSearchBar extends StatefulWidget {
  const StockSearchBar({super.key});

  @override
  State<StockSearchBar> createState() => _StockSearchBarState();
}

class _StockSearchBarState extends State<StockSearchBar> {
  final _controller = TextEditingController();
  final _focus = FocusNode();
  List<StockSearchHit> _hits = [];
  var _searching = false;
  var _usingFallback = false;
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    final q = value.trim();
    if (q.length < 1) {
      setState(() {
        _hits = [];
        _usingFallback = false;
        _searching = false;
      });
      return;
    }
    _debounce = Timer(const Duration(milliseconds: 300), () => _runSearch(q));
  }

  List<StockSearchHit> _fallbackHits(String q) {
    final sym = q.toUpperCase();
    return [StockSearchHit(symbol: sym, name: 'Mở trang chi tiết $sym')];
  }

  Future<void> _runSearch(String q) async {
    setState(() {
      _searching = true;
      _usingFallback = false;
    });
    try {
      final api = context.read<ApiClient>();
      final hits = await api.searchStocks(q, limit: 8);
      if (!mounted || _controller.text.trim() != q) return;
      setState(() {
        _hits = hits.isEmpty ? _fallbackHits(q) : hits;
        _usingFallback = hits.isEmpty;
      });
    } on ApiException catch (_) {
      if (!mounted) return;
      setState(() {
        _hits = _fallbackHits(q);
        _usingFallback = true;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _hits = _fallbackHits(q);
        _usingFallback = true;
      });
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  void _openSymbol(String symbol) {
    final sym = symbol.toUpperCase();
    _controller.clear();
    _focus.unfocus();
    setState(() {
      _hits = [];
      _usingFallback = false;
    });
    context.push('/stocks/$sym');
  }

  void _submit() {
    final sym = _controller.text.trim().toUpperCase();
    if (sym.isEmpty) return;
    if (_hits.length == 1) {
      _openSymbol(_hits.first.symbol);
      return;
    }
    final exact = _hits.where((h) => h.symbol == sym).toList();
    if (exact.isNotEmpty) {
      _openSymbol(exact.first.symbol);
      return;
    }
    _openSymbol(sym);
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final q = _controller.text.trim();
    final showResults = _focus.hasFocus && q.isNotEmpty && (_hits.isNotEmpty || _searching);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 6),
          decoration: BoxDecoration(
            color: AppColors.surfaceLowest(context),
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: scheme.outline.withValues(alpha: 0.3)),
          ),
          child: Row(
            children: [
              Expanded(
                child: Theme(
                  data: Theme.of(context).copyWith(
                    inputDecorationTheme: const InputDecorationTheme(
                      filled: false,
                      border: InputBorder.none,
                      enabledBorder: InputBorder.none,
                      focusedBorder: InputBorder.none,
                      disabledBorder: InputBorder.none,
                      errorBorder: InputBorder.none,
                      focusedErrorBorder: InputBorder.none,
                      isDense: true,
                    ),
                  ),
                  child: TextField(
                    controller: _controller,
                    focusNode: _focus,
                    textCapitalization: TextCapitalization.characters,
                    keyboardType: TextInputType.text,
                    textInputAction: TextInputAction.done,
                    autocorrect: false,
                    enableSuggestions: false,
                    onChanged: _onChanged,
                    onSubmitted: (_) => _submit(),
                    style: const TextStyle(fontSize: 14),
                    decoration: const InputDecoration(
                      hintText: 'Tìm mã (FPT) hoặc tên công ty',
                      hintStyle: TextStyle(fontSize: 13),
                      border: InputBorder.none,
                      enabledBorder: InputBorder.none,
                      focusedBorder: InputBorder.none,
                      contentPadding: EdgeInsets.fromLTRB(12, 12, 4, 12),
                      isDense: true,
                      prefixIcon: null,
                      prefixIconConstraints: BoxConstraints(),
                    ),
                  ),
                ),
              ),
              if (_searching)
                const Padding(
                  padding: EdgeInsets.only(right: 10),
                  child: SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                )
              else if (_controller.text.isNotEmpty)
                IconButton(
                  onPressed: () {
                    _controller.clear();
                    setState(() {
                      _hits = [];
                      _usingFallback = false;
                    });
                  },
                  icon: const Icon(Icons.close, size: 18),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
        ),
        if (showResults) ...[
          const SizedBox(height: 6),
          Container(
            decoration: BoxDecoration(
              color: AppColors.surfaceLow(context),
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: scheme.outline.withValues(alpha: 0.25)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_searching && _hits.isEmpty)
                  Padding(
                    padding: const EdgeInsets.all(12),
                    child: Text('Đang tìm…', style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant)),
                  )
                else ...[
                  if (_usingFallback)
                    Padding(
                      padding: const EdgeInsets.fromLTRB(12, 10, 12, 0),
                      child: Text(
                        'Nhấn mã hoặc Enter để xem chi tiết.',
                        style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                      ),
                    ),
                  ..._hits.map(
                    (hit) => _HitTile(hit: hit, onTap: () => _openSymbol(hit.symbol)),
                  ),
                ],
              ],
            ),
          ),
        ],
      ],
    );
  }
}

class _HitTile extends StatelessWidget {
  const _HitTile({required this.hit, required this.onTap});

  final StockSearchHit hit;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Row(
            children: [
              Text(hit.symbol, style: dataFont(context, weight: FontWeight.w700)),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  hit.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: scheme.onSurfaceVariant),
                ),
              ),
              Icon(Icons.chevron_right, size: 18, color: scheme.onSurfaceVariant),
            ],
          ),
        ),
      ),
    );
  }
}
