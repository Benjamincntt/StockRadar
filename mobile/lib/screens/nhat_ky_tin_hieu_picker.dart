import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../widgets/glass_card.dart';
import '../widgets/pushed_page_scaffold.dart';
import '../widgets/scenario_widgets.dart';
import '../widgets/score_pill.dart';

/// Picker tín hiệu US2 (chỉ đọc): tín hiệu V3 (kịch bản P1++) đã vào lệnh.
/// Pop về form lệnh với tín hiệu đã chọn; mỗi dòng có nút "không khớp / bỏ qua" (Q3).
class NhatKyTinHieuPickerScreen extends StatefulWidget {
  const NhatKyTinHieuPickerScreen({super.key});

  @override
  State<NhatKyTinHieuPickerScreen> createState() =>
      _NhatKyTinHieuPickerScreenState();
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
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  String _formatGia(double? gia) => gia == null ? '—' : formatPrice(gia);

  /// Ngày hiển thị dd/MM/yyyy; không parse được thì giữ nguyên gốc.
  static String _fmtNgay(String ngay) {
    final d = DateTime.tryParse(ngay);
    if (d == null) return ngay;
    final mm = d.month.toString().padLeft(2, '0');
    final dd = d.day.toString().padLeft(2, '0');
    return '$dd/$mm/${d.year}';
  }

  /// Nguồn 'v3' → nhãn thân thiện 'V3' — không để độc giả thấy khóa nội bộ.
  static String _nhanNguon(String nguon) => nguon.toUpperCase();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return PushedPageScaffold(
      title: 'Chọn tín hiệu',
      child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_error!, textAlign: TextAlign.center),
                        const SizedBox(height: 12),
                        OutlinedButton.icon(
                          onPressed: _reload,
                          icon: const Icon(Icons.refresh, size: 16),
                          label: const Text('Thử lại'),
                        ),
                      ],
                    ),
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _reload,
                  child: _items.isEmpty
                      ? ListView(
                          children: const [
                            SizedBox(height: 80),
                            Center(
                                child: Text(
                                    'Không có tín hiệu nào trong khoảng lấy về.')),
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
                                        onTap: () =>
                                            Navigator.pop(context, item),
                                        child: Column(
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Text(
                                              '${item.symbol} · ${_formatGia(item.giaTinHieu)}',
                                              style: const TextStyle(
                                                  fontWeight: FontWeight.w800,
                                                  fontSize: 14),
                                            ),
                                            const SizedBox(height: 2),
                                            Text(
                                              [
                                                _nhanNguon(item.nguon),
                                                if (item.loaiTinHieu != null &&
                                                    item.loaiTinHieu!
                                                        .isNotEmpty)
                                                  TinHieuV3Mau.tenLoai(
                                                      item.loaiTinHieu!),
                                                _fmtNgay(item.ngay),
                                                if (item.ketQuaTinHieu != null)
                                                  'kết quả: ${TinHieuV3Mau.nhanKetQua(item.ketQuaTinHieu)}',
                                                if (item.ketQuaTinHieuPhanTram !=
                                                    null)
                                                  'kết quả: ${item.ketQuaTinHieuPhanTram!.toStringAsFixed(1)}%',
                                                if (item.daCoLenh) 'đã có lệnh',
                                                if (item.daBoQua) 'đã bỏ qua',
                                              ].join(' · '),
                                              style: TextStyle(
                                                  fontSize: 11,
                                                  color: Theme.of(context)
                                                      .colorScheme
                                                      .onSurfaceVariant),
                                            ),
                                          ],
                                        ),
                                      ),
                                    ),
                                    IconButton(
                                      onPressed: () => _toggleBoQua(item),
                                      icon: Icon(
                                        item.daBoQua
                                            ? Icons.undo
                                            : Icons.do_disturb_alt_outlined,
                                        size: 20,
                                        // Màu theo token chủ đề thay vì Colors.orange/blueGrey gõ cứng (NKP-04).
                                        color: item.daBoQua
                                            ? scheme.primary
                                            : scheme.error,
                                      ),
                                      tooltip: item.daBoQua
                                          ? 'Bỏ đánh dấu bỏ qua'
                                          : 'Không khớp / bỏ qua',
                                    ),
                                    TextButton(
                                      onPressed: () =>
                                          Navigator.pop(context, item),
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
