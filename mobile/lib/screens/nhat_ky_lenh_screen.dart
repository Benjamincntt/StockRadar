import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/time/api_date.dart';
import 'nhat_ky_tin_hieu_picker.dart';

/// Form ghi/sửa một lệnh nhật ký (US1). 400/422 từ API hiển thị nguyên văn detail.
class NhatKyLenhScreen extends StatefulWidget {
  const NhatKyLenhScreen({super.key, this.existing});

  final NhatKyLenh? existing;

  @override
  State<NhatKyLenhScreen> createState() => _NhatKyLenhScreenState();
}

class _NhatKyLenhScreenState extends State<NhatKyLenhScreen> {
  final _formKey = GlobalKey<FormState>();
  final _symbolCtrl = TextEditingController();
  final _giaCtrl = TextEditingController();
  final _klCtrl = TextEditingController();
  final _phiCtrl = TextEditingController();
  final _lyDoCtrl = TextEditingController();
  final _ghiChuCtrl = TextEditingController();
  final _kLieuChinhCtrl = TextEditingController();
  final _vonDieuChinhCtrl = TextEditingController();

  String _loai = 'Mua';
  DateTime _thoiDiem = DateTime.now().toUtc();
  String? _nguonTinHieu;
  String? _maTinHieu;
  double? _giaTinHieu;
  var _saving = false;

  bool get _isEdit => widget.existing != null;

  @override
  void initState() {
    super.initState();
    final e = widget.existing;
    if (e == null) return;
    _loai = e.loaiLenh;
    _symbolCtrl.text = e.symbol;
    _nguonTinHieu = e.nguonTinHieu;
    _maTinHieu = e.maTinHieu;
    _giaTinHieu = e.giaTinHieuChup;
    if (e.loaiLenh != 'DieuChinh') {
      _giaCtrl.text = e.giaKhop.toStringAsFixed(0);
      _klCtrl.text = e.khoiLuong.toStringAsFixed(0);
    }
    _phiCtrl.text = e.phi?.toStringAsFixed(0) ?? '';
    _lyDoCtrl.text = e.lyDo ?? '';
    _ghiChuCtrl.text = e.ghiChu ?? '';
    _kLieuChinhCtrl.text = e.kLieuChinh?.toStringAsFixed(0) ?? '';
    _vonDieuChinhCtrl.text = e.vonDieuChinh?.toStringAsFixed(0) ?? '';
    if (e.thoiDiemKhop.isNotEmpty) {
      _thoiDiem = parseApiInstant(e.thoiDiemKhop);
    }
  }

  @override
  void dispose() {
    for (final c in [
      _symbolCtrl, _giaCtrl, _klCtrl, _phiCtrl, _lyDoCtrl, _ghiChuCtrl, _kLieuChinhCtrl, _vonDieuChinhCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _pickTime() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _thoiDiem.toLocal(),
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_thoiDiem.toLocal()),
    );
    if (time == null || !mounted) return;
    setState(() {
      _thoiDiem = DateTime(date.year, date.month, date.day, time.hour, time.minute).toUtc();
    });
  }

  double? _parse(String text) => double.tryParse(text.replaceAll('.', '').replaceAll(',', '.'));

  /// US2: mở picker tín hiệu (V2), chọn → điền sẵn mã, chiều, giá tín hiệu.
  Future<void> _pickTinHieu() async {
    final picked = await Navigator.of(context).push<TinHieuNhatKy>(
      MaterialPageRoute(builder: (_) => const NhatKyTinHieuPickerScreen()),
    );
    if (picked == null || !mounted) return;
    setState(() {
      _nguonTinHieu = picked.nguon;
      _maTinHieu = picked.maTinHieu;
      _giaTinHieu = picked.giaTinHieu;
      if (_symbolCtrl.text.isEmpty) _symbolCtrl.text = picked.symbol;
      // Kịch bản bán (Kiệt sức / Gãy nền) → đề xuất chiều Bán; tín hiệu mua → Mua.
      if (_loai == 'Mua' && (picked.loaiKichBan == 'KietSuc' || picked.loaiKichBan == 'GayNen')) {
        _loai = 'Ban';
      }
      if (picked.giaTinHieu != null && _giaCtrl.text.isEmpty) {
        _giaCtrl.text = picked.giaTinHieu!.toStringAsFixed(0);
      }
    });
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final laDieuChinh = _loai == 'DieuChinh';
    final body = <String, dynamic>{
      'symbol': _symbolCtrl.text.trim().toUpperCase(),
      'loaiLenh': _loai,
      'thoiDiemKhop': _thoiDiem.toUtc().toIso8601String(),
      'giaKhop': laDieuChinh ? null : _parse(_giaCtrl.text),
      'khoiLuong': laDieuChinh ? null : _parse(_klCtrl.text),
      'phi': _phiCtrl.text.isEmpty ? null : _parse(_phiCtrl.text),
      'nguonTinHieu': _nguonTinHieu,
      'maTinHieu': _maTinHieu,
      'lyDo': _lyDoCtrl.text.isEmpty ? null : _lyDoCtrl.text.trim(),
      'ghiChu': _ghiChuCtrl.text.isEmpty ? null : _ghiChuCtrl.text.trim(),
      'kLieuChinh': laDieuChinh && _kLieuChinhCtrl.text.isNotEmpty ? _parse(_kLieuChinhCtrl.text) : null,
      'vonDieuChinh': laDieuChinh && _vonDieuChinhCtrl.text.isNotEmpty ? _parse(_vonDieuChinhCtrl.text) : null,
    };

    setState(() => _saving = true);
    try {
      final api = context.read<ApiClient>();
      if (_isEdit) {
        await api.suaLenhNhatKy(widget.existing!.id, body);
      } else {
        await api.themLenhNhatKy(body);
      }
      if (mounted) Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  String? _validateRequired(String? value) =>
      (value == null || value.trim().isEmpty) ? 'Bắt buộc' : null;

  @override
  Widget build(BuildContext context) {
    final laDieuChinh = _loai == 'DieuChinh';
    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Sửa lệnh' : 'Thêm lệnh')),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              SegmentedButton<String>(
                segments: const [
                  ButtonSegment(value: 'Mua', label: Text('Mua')),
                  ButtonSegment(value: 'Ban', label: Text('Bán')),
                  ButtonSegment(value: 'DieuChinh', label: Text('Điều chỉnh')),
                ],
                selected: {_loai},
                onSelectionChanged: (s) => setState(() => _loai = s.first),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _symbolCtrl,
                textCapitalization: TextCapitalization.characters,
                decoration: const InputDecoration(labelText: 'Mã CK (vd: HPG)'),
                validator: _validateRequired,
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Gắn tín hiệu (tuỳ chọn)'),
                subtitle: Text(_nguonTinHieu == null
                    ? 'Chưa gắn — chạm để chọn từ V2'
                    : '${tinHieuNguonLabel(_nguonTinHieu)} · ${_maTinHieu ?? ''}'
                        '${_giaTinHieu != null ? ' · giá tín hiệu ${_giaTinHieu!.toStringAsFixed(0)}' : ''}'),
                trailing: const Icon(Icons.sensors),
                onTap: _pickTinHieu,
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Thời điểm khớp (giờ VN)'),
                subtitle: Text(formatApiDateTime(_thoiDiem.toIso8601String())),
                trailing: const Icon(Icons.edit_calendar),
                onTap: _pickTime,
              ),
              if (!laDieuChinh) ...[
                TextFormField(
                  controller: _giaCtrl,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Giá khớp (VND/cp)'),
                  validator: _validateRequired,
                ),
                TextFormField(
                  controller: _klCtrl,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'Khối lượng (cp)'),
                  validator: _validateRequired,
                ),
              ] else ...[
                TextFormField(
                  controller: _kLieuChinhCtrl,
                  keyboardType: const TextInputType.numberWithOptions(signed: true, decimal: true),
                  decoration: const InputDecoration(labelText: '± Khối lượng (vd: +100 cp cổ tức)'),
                ),
                TextFormField(
                  controller: _vonDieuChinhCtrl,
                  keyboardType: const TextInputType.numberWithOptions(signed: true, decimal: true),
                  decoration: const InputDecoration(labelText: '± Giá vốn (VND/cp)'),
                ),
              ],
              TextFormField(
                controller: _phiCtrl,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Phí + thuế thực tế (VND, để trống = theo config)',
                ),
              ),
              TextFormField(
                controller: _lyDoCtrl,
                decoration: const InputDecoration(labelText: 'Lý do (tuỳ chọn)'),
              ),
              TextFormField(
                controller: _ghiChuCtrl,
                maxLines: 2,
                decoration: InputDecoration(
                  labelText: 'Ghi chú${laDieuChinh ? ' (bắt buộc với Điều chỉnh)' : ''}',
                ),
                validator: laDieuChinh ? _validateRequired : null,
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _saving ? null : _save,
                child: _saving
                    ? const SizedBox(
                        width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(_isEdit ? 'Lưu thay đổi' : 'Ghi lệnh'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
