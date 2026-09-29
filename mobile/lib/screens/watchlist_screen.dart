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

/// Watchlist đa danh sách: mặc định · ngành tự động (read-only) · tùy chỉnh.
/// Chip ngang chọn danh sách — chạm giữ chip tùy chỉnh để đổi tên/xóa.
class WatchlistScreen extends StatefulWidget {
  const WatchlistScreen({super.key});

  @override
  State<WatchlistScreen> createState() => _WatchlistScreenState();
}

class _WatchlistScreenState extends State<WatchlistScreen> {
  ApiClient get _api => context.read<ApiClient>();

  List<Watchlist> _lists = [];
  int? _selectedId;
  List<WatchlistItem> _items = [];
  final _symbolCtrl = TextEditingController();

  var _loadingLists = true;
  var _loadingItems = false;
  var _adding = false;
  String? _error;
  String? _itemsError;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  @override
  void dispose() {
    _symbolCtrl.dispose();
    super.dispose();
  }

  Watchlist? get _selected {
    for (final list in _lists) {
      if (list.id == _selectedId) return list;
    }
    return null;
  }

  /// Thứ tự chip: Mặc định → tùy chỉnh (theo thời điểm tạo) → ngành (A→Z).
  List<Watchlist> get _orderedLists {
    final defaults = <Watchlist>[];
    final customs = <Watchlist>[];
    final sectors = <Watchlist>[];
    for (final list in _lists) {
      if (list.laMacDinh) {
        defaults.add(list);
      } else if (list.laDanhSachNganh) {
        sectors.add(list);
      } else {
        customs.add(list);
      }
    }
    customs.sort((a, b) => a.id.compareTo(b.id));
    sectors.sort((a, b) =>
        (a.maNganh ?? a.name).toLowerCase().compareTo((b.maNganh ?? b.name).toLowerCase()));
    return [...defaults, ...customs, ...sectors];
  }

  // ==== Tải dữ liệu ====

  Future<void> _reload({bool silent = false, int? selectId}) async {
    if (!silent) {
      setState(() {
        _loadingLists = true;
        _error = null;
      });
    }
    try {
      final lists = await _api.getWatchlists();
      if (!mounted) return;

      // Giữ lựa chọn hiện tại nếu còn tồn tại; nếu không chọn danh sách mặc định.
      var target = selectId ?? _selectedId;
      if (target == null || !lists.any((w) => w.id == target)) {
        final defaults = lists.where((w) => w.laMacDinh).toList();
        target = defaults.isNotEmpty
            ? defaults.first.id
            : (lists.isNotEmpty ? lists.first.id : null);
      }

      final changed = target != _selectedId;
      setState(() {
        _lists = lists;
        _selectedId = target;
        _loadingLists = false;
        _error = null;
        if (changed) {
          _items = [];
          _itemsError = null;
          _loadingItems = true;
        }
      });
      await _loadItems(silent: silent && !changed);
    } on ApiException catch (e) {
      if (!mounted) return;
      if (silent && _lists.isNotEmpty) {
        setState(() => _loadingLists = false);
        _showSnack(e.message);
      } else {
        setState(() {
          _loadingLists = false;
          _error = e.message;
        });
      }
    }
  }

  Future<void> _loadItems({bool silent = false}) async {
    final id = _selectedId;
    if (id == null) {
      setState(() {
        _items = [];
        _loadingItems = false;
      });
      return;
    }
    if (!silent) {
      setState(() {
        _loadingItems = true;
        _itemsError = null;
      });
    }
    try {
      final items = await _api.getWatchlistItems(id);
      if (!mounted) return;
      if (_selectedId != id) return;
      setState(() {
        _items = items;
        _itemsError = null;
        _loadingItems = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      if (_selectedId != id) return;
      if (silent && _items.isNotEmpty) {
        setState(() => _loadingItems = false);
        _showSnack(e.message);
      } else {
        setState(() {
          _itemsError = e.message;
          _loadingItems = false;
        });
      }
    }
  }

  void _selectList(int id) {
    if (id == _selectedId) return;
    setState(() {
      _selectedId = id;
      _items = [];
      _itemsError = null;
      _loadingItems = true;
    });
    _loadItems();
  }

  // ==== Hành động danh sách ====

  /// Chặn thao tác quản lý khi chưa đăng nhập — đẩy sang màn đăng nhập.
  bool _requireLogin() {
    if (context.read<AuthService>().isLoggedIn) return true;
    context.push('/login');
    return false;
  }

  Future<void> _openCreateDialog() async {
    if (!_requireLogin()) return;
    final ctrl = TextEditingController();
    final name = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Tạo danh sách mới'),
        content: TextField(
          controller: ctrl,
          autofocus: true,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(hintText: 'VD: Cổ phiếu ưa thích'),
          onSubmitted: (value) => Navigator.of(dialogContext).pop(value.trim()),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(ctrl.text.trim()),
            child: const Text('Tạo'),
          ),
        ],
      ),
    );
    ctrl.dispose();
    if (name == null || name.isEmpty) return;
    if (!mounted) return;
    try {
      final created = await _api.createWatchlist(name);
      if (!mounted) return;
      await _reload(silent: true, selectId: created.id);
    } on ApiException catch (e) {
      if (mounted) _showSnack(e.message);
    }
  }

  void _openListOptions(Watchlist list) {
    if (!_requireLogin()) return;
    final scheme = Theme.of(context).colorScheme;
    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      list.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
                    ),
                  ),
                ],
              ),
            ),
            ListTile(
              leading: const Icon(Icons.edit_outlined, size: 20),
              title: const Text('Đổi tên danh sách'),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _renameList(list);
              },
            ),
            ListTile(
              leading: Icon(Icons.delete_outline, size: 20, color: scheme.error),
              title: Text('Xóa danh sách', style: TextStyle(color: scheme.error)),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _deleteList(list);
              },
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }

  Future<void> _renameList(Watchlist list) async {
    final ctrl = TextEditingController(text: list.name);
    final name = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Đổi tên danh sách'),
        content: TextField(
          controller: ctrl,
          autofocus: true,
          textCapitalization: TextCapitalization.sentences,
          onSubmitted: (value) => Navigator.of(dialogContext).pop(value.trim()),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(ctrl.text.trim()),
            child: const Text('Lưu'),
          ),
        ],
      ),
    );
    ctrl.dispose();
    if (name == null || name.isEmpty || name == list.name) return;
    if (!mounted) return;
    try {
      await _api.renameWatchlist(list.id, name);
      if (!mounted) return;
      await _reload(silent: true);
    } on ApiException catch (e) {
      if (mounted) _showSnack(e.message);
    }
  }

  Future<void> _deleteList(Watchlist list) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('Xóa danh sách "${list.name}"?'),
        content: const Text('Các mã đã thêm chỉ bị xóa khỏi danh sách này.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Hủy'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(dialogContext).colorScheme.error,
              foregroundColor: Colors.white,
              minimumSize: const Size(64, 40),
            ),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Xóa'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    if (!mounted) return;
    try {
      await _api.deleteWatchlist(list.id);
      if (!mounted) return;
      if (_selectedId == list.id) {
        setState(() {
          _selectedId = null;
          _items = [];
        });
      }
      await _reload();
    } on ApiException catch (e) {
      if (mounted) _showSnack(e.message);
    }
  }

  // ==== Hành động item ====

  Future<void> _addSymbol() async {
    final list = _selected;
    if (list == null || !list.choPhepSua || _adding) return;
    if (!_requireLogin()) return;
    final symbol = _symbolCtrl.text.trim().toUpperCase();
    if (symbol.isEmpty) {
      _showSnack('Nhập mã cổ phiếu trước đã.');
      return;
    }
    setState(() => _adding = true);
    try {
      await _api.addToWatchlistById(list.id, symbol);
      if (!mounted) return;
      _symbolCtrl.clear();
      await _loadItems();
    } on ApiException catch (e) {
      if (mounted) _showSnack(e.message);
    } finally {
      if (mounted) setState(() => _adding = false);
    }
  }

  Future<void> _removeSymbol(WatchlistItem item) async {
    final list = _selected;
    if (list == null) return;
    // Gỡ khỏi UI ngay để Dismissible không dựng lại item vừa bị vuốt.
    setState(() => _items.removeWhere((e) => e.symbol == item.symbol));
    try {
      await _api.removeFromWatchlistById(list.id, item.symbol);
    } on ApiException catch (e) {
      if (!mounted) return;
      _showSnack(e.message);
      await _loadItems();
    }
  }

  void _showSnack(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  // ==== UI ====

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthService>();

    if (_loadingLists) return const LoadingView();

    if (_lists.isEmpty) {
      return RefreshIndicator(
        onRefresh: () => _reload(silent: true),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
          children: [
            const PageHeader(
              title: 'Watchlist',
              subtitle: 'Danh sách theo dõi mã — mặc định, ngành & tùy chỉnh',
            ),
            const SizedBox(height: 12),
            if (!auth.isLoggedIn)
              const Padding(
                padding: EdgeInsets.only(bottom: 12),
                child: ErrorBanner(message: 'Đăng nhập để quản lý watchlist cá nhân'),
              ),
            if (_error != null) ErrorBanner(message: _error!, onRetry: _reload),
          ],
        ),
      );
    }

    final selected = _selected;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const PageHeader(
                title: 'Watchlist',
                subtitle: 'Danh sách theo dõi mã — mặc định, ngành & tùy chỉnh',
              ),
              if (!auth.isLoggedIn) ...[
                const SizedBox(height: 12),
                const ErrorBanner(message: 'Đăng nhập để quản lý watchlist cá nhân'),
              ],
            ],
          ),
        ),
        const SizedBox(height: 10),
        _buildChipsRow(),
        if (selected != null && selected.choPhepSua)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 10, 16, 2),
            child: _buildAddField(),
          ),
        if (selected != null && !selected.choPhepSua) _buildAutoSectorLabel(),
        Expanded(
          child: RefreshIndicator(
            onRefresh: () => _reload(silent: true),
            child: _buildItemsArea(selected),
          ),
        ),
      ],
    );
  }

  Widget _buildChipsRow() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Row(
        children: [
          for (final list in _orderedLists)
            _buildChip(
              label: list.name,
              selected: list.id == _selectedId,
              onTap: () => _selectList(list.id),
              // Chỉ danh sách tùy chỉnh mới có đổi tên/xóa (chạm giữ).
              onLongPress: (!list.laMacDinh && !list.laDanhSachNganh)
                  ? () => _openListOptions(list)
                  : null,
            ),
          _buildAddChip(),
        ],
      ),
    );
  }

  Widget _buildChip({
    required String label,
    required bool selected,
    required VoidCallback onTap,
    VoidCallback? onLongPress,
  }) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final onFill = isDark ? const Color(0xFF002022) : Colors.white;
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          onLongPress: onLongPress,
          borderRadius: BorderRadius.circular(999),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 160),
            curve: Curves.easeOut,
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
            decoration: BoxDecoration(
              color: selected ? scheme.primary : AppColors.surfaceLow(context),
              borderRadius: BorderRadius.circular(999),
              border: Border.all(
                color: selected ? scheme.primary : scheme.outline.withValues(alpha: 0.25),
              ),
            ),
            child: Text(
              label,
              style: TextStyle(
                fontSize: 12,
                fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                color: selected ? onFill : scheme.onSurfaceVariant,
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildAddChip() {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: _openCreateDialog,
        borderRadius: BorderRadius.circular(999),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: scheme.primary.withValues(alpha: 0.5)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.add, size: 15, color: scheme.primary),
              const SizedBox(width: 4),
              Text(
                'Thêm',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: scheme.primary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildAutoSectorLabel() {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 2),
      child: Row(
        children: [
          Icon(Icons.auto_awesome, size: 13, color: scheme.onSurfaceVariant),
          const SizedBox(width: 6),
          Text(
            'Danh sách tự động theo ngành',
            style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
          ),
        ],
      ),
    );
  }

  Widget _buildAddField() {
    final scheme = Theme.of(context).colorScheme;
    return Container(
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
              textInputAction: TextInputAction.done,
              onSubmitted: (_) => _addSymbol(),
              decoration: const InputDecoration(
                hintText: 'VD: FPT',
                border: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                isDense: true,
              ),
            ),
          ),
          FilledButton(
            onPressed: _adding ? null : _addSymbol,
            style: FilledButton.styleFrom(
              minimumSize: const Size(72, 40),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
            ),
            child: _adding
                ? SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Theme.of(context).colorScheme.onPrimary,
                    ),
                  )
                : const Text('Thêm'),
          ),
        ],
      ),
    );
  }

  Widget _buildItemsArea(Watchlist? selected) {
    final scheme = Theme.of(context).colorScheme;
    if (selected == null) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
        children: [
          GlassCard(
            child: Text('Chọn một danh sách để xem mã.', style: TextStyle(color: scheme.onSurfaceVariant)),
          ),
        ],
      );
    }
    if (_loadingItems) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_itemsError != null) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
        children: [ErrorBanner(message: _itemsError!, onRetry: () => _loadItems())],
      );
    }
    if (_items.isEmpty) {
      final message = selected.laDanhSachNganh
          ? 'Chưa có mã nào trong ngành này.'
          : 'Chưa có mã nào. Thêm mã để theo dõi.';
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
        children: [
          GlassCard(child: Text(message, style: TextStyle(color: scheme.onSurfaceVariant))),
        ],
      );
    }

    final canEdit = selected.choPhepSua;
    return ListView.builder(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
      itemCount: _items.length + 1,
      itemBuilder: (context, index) {
        if (index == 0) return _buildColumnHeader();
        return _buildItemRow(selected, _items[index - 1], canEdit: canEdit);
      },
    );
  }

  Widget _buildColumnHeader() {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          Expanded(flex: 3, child: Text('MÃ', style: labelCaps(context))),
          Expanded(flex: 2, child: Text('ĐIỂM', style: labelCaps(context))),
          Expanded(
            flex: 2,
            child: Text('%', style: labelCaps(context), textAlign: TextAlign.end),
          ),
        ],
      ),
    );
  }

  Widget _buildItemRow(Watchlist list, WatchlistItem item, {required bool canEdit}) {
    final scheme = Theme.of(context).colorScheme;
    final row = SurfaceRow(
      onTap: () => context.push('/stocks/${item.symbol}'),
      child: Row(
        children: [
          Expanded(
            flex: 3,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.symbol, style: const TextStyle(fontWeight: FontWeight.w700)),
                Text(
                  item.name,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 11, color: scheme.onSurfaceVariant),
                ),
                const SizedBox(height: 2),
                Text(
                  item.sector.isNotEmpty ? item.sector : 'Chưa phân ngành',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 10, color: scheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
          Expanded(flex: 2, child: ScorePill(item.score)),
          Expanded(
            flex: 2,
            child: Align(alignment: Alignment.centerRight, child: ChangePill(item.changePercent)),
          ),
        ],
      ),
    );

    if (!canEdit) {
      return Padding(padding: const EdgeInsets.only(bottom: 8), child: row);
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Dismissible(
        key: ValueKey('watchlist-${list.id}-${item.symbol}'),
        direction: DismissDirection.endToStart,
        background: Container(
          alignment: Alignment.centerRight,
          padding: const EdgeInsets.only(right: 20),
          decoration: BoxDecoration(
            color: AppColors.negativeDim(context),
            borderRadius: BorderRadius.circular(16),
          ),
          child: Icon(Icons.delete_outline, size: 20, color: scheme.error),
        ),
        confirmDismiss: (_) async {
          if (!context.read<AuthService>().isLoggedIn) {
            context.push('/login');
            return false;
          }
          return true;
        },
        onDismissed: (_) => _removeSymbol(item),
        child: row,
      ),
    );
  }
}
