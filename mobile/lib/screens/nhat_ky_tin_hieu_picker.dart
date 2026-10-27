import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../widgets/glass_card.dart';

/// Picker tín hiệu US2 (chỉ đọc): tín hiệu V3 (kịch bản P1++) đã vào lệnh.
/// Pop về form lệnh với tín hiệu đã chọn; mỗi dòng có nút "không khớp / bỏ qua" (Q3).
class NhatKyTinHieuPickerScreen extends StatefulWidget {
  const NhatKyTinHieuPickerScreen({super.key});

  @override
  State<NhatKyTinHieuPickerScreen> createState() => _NhatKyTinHieuPickerScreenState();
}

class _NhatKyTinHieuPickerScreenState extends State<NhatKyTinHieuPickerScreen> {
  ApiClient get _api => context.read<ApiClient>();

  List<TinHieuNhatKy> _items = [];
  var _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  static const String _nguon = 'v3';

  Future<void> _reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await _api.getTinHieuNhatKy(_nguon, page: 1, size: 50);
      if (!mounted) return;
      setState(() {
        _items = items;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    }
  }

  Future<void> _toggleBoQua(TinHieuNhatKy item) async {
    try {
      if (item.daBoQua) {
        await _api.xoaBoQuaTinHieu(_nguon, item.maTinHieu);
      } else {
        await _api.datBoQuaTinHieu(_nguon, item.maTinHieu);
      }
      await _reload();
    } on ApiException catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  String _formatGia(double? gia) {
    if (gia == null) return '—';
    final digits = gia.round().abs().toString();
    final buffer = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) buffer.write('.');
      buffer.write(digits[i]);
    }
    return '${gia < 0 ? '-' : ''}$buffer';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Chọn tín hiệu'),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Text(_error!, textAlign: TextAlign.center),
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _reload,
                  child: _items.isEmpty
                      ? ListView(
                          children: const [
                            SizedBox(height: 80),
                            Center(child: Text('Không có tín hiệu nào trong khoảng lấy về.')),
                          ],
                        )
                      : ListView.builder(
                          padding: const EdgeInsets.fromLTRB(12, 8, 12, 24),
                          itemCount: _items.length,
                          itemBuilder: (context, i) {
                            final item = _items[i];
                            return Padding(
                              padding: const EdgeInsets.only(bottom: 8),
                              child: GlassCard(
                                child: Row(
                                  children: [
                                    Expanded(
                                      child: GestureDetector(
                                        onTap: () => Navigator.pop(context, item),
                                        child: Column(
                                          crossAxisAlignment: CrossAxisAlignment.start,
                                          children: [
                                            Text(
                                              '${item.symbol} · ${_formatGia(item.giaTinHieu)} đ',
                                              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14),
                                            ),
                                            const SizedBox(height: 2),
                                            Text(
                                              [
                                                item.nguon,
                                                item.loaiTinHieu,
                                                item.ngay,
                                                if (item.ketQuaTinHieu != null) 'kết quả: ${item.ketQuaTinHieu}',
                                                if (item.ketQuaTinHieuPhanTram != null)
                                                  'kết quả: ${item.ketQuaTinHieuPhanTram!.toStringAsFixed(1)}%',
                                                if (item.daCoLenh) 'đã có lệnh',
                                                if (item.daBoQua) 'đã bỏ qua',
                                              ].where((s) => s != null).join(' · '),
                                              style: TextStyle(
                                                  fontSize: 11,
                                                  color: Theme.of(context).colorScheme.onSurfaceVariant),
                                            ),
                                          ],
                                        ),
                                      ),
                                    ),
                                    IconButton(
                                      onPressed: () => _toggleBoQua(item),
                                      icon: Icon(
                                        item.daBoQua ? Icons.undo : Icons.do_disturb_alt_outlined,
                                        size: 20,
                                        color: item.daBoQua ? Colors.blueGrey : Colors.orange,
                                      ),
                                      tooltip: item.daBoQua ? 'Bỏ đánh dấu bỏ qua' : 'Không khớp / bỏ qua',
                                    ),
                                    TextButton(
                                      onPressed: () => Navigator.pop(context, item),
                                      child: const Text('Chọn'),
                                    ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
            ),
    );
  }
}
