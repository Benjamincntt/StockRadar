import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../widgets/glass_card.dart';
import '../widgets/pushed_page_scaffold.dart';

class SuKienQuyenScreen extends StatefulWidget {
  const SuKienQuyenScreen({super.key, required this.symbol});

  final String symbol;

  @override
  State<SuKienQuyenScreen> createState() => _SuKienQuyenScreenState();
}

class _SuKienQuyenScreenState extends State<SuKienQuyenScreen> {
  ApiClient get _api => context.read<ApiClient>();

  List<SuKienQuyen> _danhSach = [];
  var _dangTai = true;
  var _dangLuu = false;
  String? _loi;
  DateTime? _ngay;
  final _tienMat = TextEditingController(text: '0');
  final _heSoPhaLoang = TextEditingController(text: '1');
  final _soCoCu = TextEditingController(text: '0');
  final _soCoMoi = TextEditingController(text: '0');
  final _giaPhatHanh = TextEditingController(text: '0');

  @override
  void initState() {
    super.initState();
    _tai();
  }

  @override
  void dispose() {
    _tienMat.dispose();
    _heSoPhaLoang.dispose();
    _soCoCu.dispose();
    _soCoMoi.dispose();
    _giaPhatHanh.dispose();
    super.dispose();
  }

  Future<void> _tai() async {
    setState(() {
      _dangTai = true;
      _loi = null;
    });
    try {
      final ds = await _api.laySuKienQuyen(widget.symbol);
      if (!mounted) return;
      setState(() {
        _danhSach = ds;
        _dangTai = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loi = e.message;
        _dangTai = false;
      });
    }
  }

  String get _ngayIso {
    final n = _ngay;
    if (n == null) return '';
    final mm = n.month.toString().padLeft(2, '0');
    final dd = n.day.toString().padLeft(2, '0');
    return '${n.year}-$mm-$dd';
  }

  Future<void> _chonNgay() async {
    final now = DateTime.now();
    final chon = await showDatePicker(
      context: context,
      initialDate: _ngay ?? now,
      firstDate: DateTime(2015),
      lastDate: DateTime(now.year + 1),
    );
    if (chon != null && mounted) setState(() => _ngay = chon);
  }

  Future<void> _luu() async {
    if (_ngay == null) return;
    setState(() {
      _dangLuu = true;
      _loi = null;
    });
    try {
      await _api.themSuKienQuyen(
        symbol: widget.symbol,
        exDate: _ngayIso,
        cash: double.tryParse(_tienMat.text.replaceAll(',', '.')) ?? 0,
        dilution: double.tryParse(_heSoPhaLoang.text.replaceAll(',', '.')) ?? 1,
        oldShares: int.tryParse(_soCoCu.text) ?? 0,
        newShares: int.tryParse(_soCoMoi.text) ?? 0,
        issuePrice: double.tryParse(_giaPhatHanh.text.replaceAll(',', '.')) ?? 0,
      );
      if (!mounted) return;
      _tienMat.text = '0';
      _heSoPhaLoang.text = '1';
      _soCoCu.text = '0';
      _soCoMoi.text = '0';
      _giaPhatHanh.text = '0';
      setState(() {
        _ngay = null;
        _dangLuu = false;
      });
      await _tai();
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loi = e.message;
        _dangLuu = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return PushedPageScaffold(
      title: 'Sự kiện quyền',
      subtitle: widget.symbol,
      child: _dangTai
          ? const LoadingView()
          : ListView(
              children: [
                GlassCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Đã ghi nhận', style: TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 4),
                      Text(
                        'Dùng khi tính % / RS / FOMO. Giá last không đổi.',
                        style: TextStyle(
                          fontSize: 12,
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),
                      const SizedBox(height: 12),
                      if (_danhSach.isEmpty)
                        Text(
                          'Chưa có sự kiện cho ${widget.symbol}.',
                          style: TextStyle(
                            fontSize: 13,
                            color: Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                        )
                      else
                        ..._danhSach.map(
                          (sk) => Padding(
                            padding: const EdgeInsets.only(bottom: 8),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'GDKHQ ${sk.exDate.length >= 10 ? sk.exDate.substring(0, 10) : sk.exDate}',
                                  style: const TextStyle(fontWeight: FontWeight.w600),
                                ),
                                Text(
                                  'Cổ tức ${sk.cash} · pha loãng ${sk.dilution}'
                                  '${sk.newShares > 0 ? ' · mua ${sk.oldShares}:${sk.newShares} giá ${sk.issuePrice}' : ''}',
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                GlassCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Thêm sự kiện', style: TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 4),
                      Text(
                        'Cổ tức 1.000đ = 1.0. Thưởng 5:1 = pha loãng 1.2. Mua 4:1 giá 10.000đ = 4 / 1 / 10.0.',
                        style: TextStyle(
                          fontSize: 12,
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                      ),
                      const SizedBox(height: 12),
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('Ngày không hưởng quyền'),
                        subtitle: Text(_ngayIso.isEmpty ? 'Chọn ngày' : _ngayIso),
                        trailing: const Icon(Icons.calendar_today),
                        onTap: _chonNgay,
                      ),
                      TextField(
                        controller: _tienMat,
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        decoration: const InputDecoration(labelText: 'Cổ tức tiền (thang Close)'),
                      ),
                      TextField(
                        controller: _heSoPhaLoang,
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        decoration: const InputDecoration(labelText: 'Hệ số pha loãng'),
                      ),
                      TextField(
                        controller: _soCoCu,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(labelText: 'Cổ cũ (n) — quyền mua n:m'),
                      ),
                      TextField(
                        controller: _soCoMoi,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(labelText: 'Cổ mới mua (m)'),
                      ),
                      TextField(
                        controller: _giaPhatHanh,
                        keyboardType: const TextInputType.numberWithOptions(decimal: true),
                        decoration: const InputDecoration(labelText: 'Giá phát hành (thang Close)'),
                      ),
                      if (_loi != null) ...[
                        const SizedBox(height: 8),
                        Text(_loi!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                      ],
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: _dangLuu || _ngay == null ? null : _luu,
                        child: Text(_dangLuu ? 'Đang lưu…' : 'Lưu sự kiện'),
                      ),
                    ],
                  ),
                ),
              ],
            ),
    );
  }
}
