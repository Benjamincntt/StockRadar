import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/models/models.dart';
import '../core/services/app_services.dart';
import '../core/time/api_date.dart';
import '../widgets/glass_card.dart';
import 'nhat_ky_lenh_screen.dart';

/// Nhật ký giao dịch thật (spec 010 US1/US3): danh sách lệnh + vị thế (giá vốn, lãi/lỗ đã thực hiện) + thống kê theo kỳ.
class NhatKyScreen extends StatefulWidget {
  const NhatKyScreen({super.key});

  @override
  State<NhatKyScreen> createState() => _NhatKyScreenState();
}

class _NhatKyScreenState extends State<NhatKyScreen> {
  ApiClient get _api => context.read<ApiClient>();

  List<NhatKyLenh> _lenh = [];
  List<ViTheThat> _viThe = [];
  ThongKeNhatKy? _thongKe;
  var _loading = false;
  String? _error;
  int _tab = 0;
  String _ky = 'month';

  @override
  void initState() {
    super.initState();
    _reload();
  }

  Future<void> _reload() async {
    if (!context.read<AuthService>().isLoggedIn) {
      setState(() => _error = null);
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final ds = await _api.getNhatKyLenh(page: 1, size: 200);
      final items = (ds['items'] as List<dynamic>? ?? [])
          .map((e) => NhatKyLenh.fromJson(e as Map<String, dynamic>))
          .toList();
      final viThe = await _api.getViTheNhatKy();
      final thongKe = await _api.getThongKeNhatKy(ky: _ky);
      if (!mounted) return;
      setState(() {
        _lenh = items;
        _viThe = viThe;
        _thongKe = thongKe;
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

  String _formatVnd(double value) {
    final rounded = value.round();
    final digits = rounded.abs().toString();
    final buffer = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) buffer.write('.');
      buffer.write(digits[i]);
    }
    return '${rounded < 0 ? '-' : ''}$buffer';
  }

  Future<void> _openForm({NhatKyLenh? existing}) async {
    final saved = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => NhatKyLenhScreen(existing: existing),
      ),
    );
    if (saved == true) await _reload();
  }

  Future<void> _delete(NhatKyLenh lenh) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Xoá lệnh?'),
        content: Text('${lenh.loaiLenh} ${lenh.symbol} ${_formatVnd(lenh.khoiLuong)} cp @ ${_formatVnd(lenh.giaKhop)}'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Huỷ')),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Xoá')),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await _api.xoaLenhNhatKy(lenh.id);
      if (mounted) await _reload();
    } on ApiException catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthService>();
    if (!auth.isLoggedIn) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Nhật ký giao dịch cần đăng nhập để lưu lệnh của riêng bạn.'),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: () => context.go('/login'),
                icon: const Icon(Icons.login, size: 18),
                label: const Text('Đăng nhập'),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: Colors.transparent,
      body: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: TabBar(
                  onTap: (i) => setState(() => _tab = i),
                  tabs: const [Tab(text: 'Lệnh'), Tab(text: 'Vị thế'), Tab(text: 'Thống kê')],
                ),
              ),
              IconButton(onPressed: _reload, icon: const Icon(Icons.refresh, size: 20)),
            ],
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                    ? Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Text(_error!, textAlign: TextAlign.center),
                        ),
                      )
                    : _tab == 0
                        ? _buildDanhSachLenh()
                        : _tab == 1
                            ? _buildViThe()
                            : _buildThongKe(),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _openForm(),
        child: const Icon(Icons.add),
      ),
    );
  }

  Widget _buildDanhSachLenh() {
    if (_lenh.isEmpty) {
      return const Center(child: Text('Chưa có lệnh nào. Chạm + để ghi lệnh đầu tiên.'));
    }
    return RefreshIndicator(
      onRefresh: _reload,
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 80),
        itemCount: _lenh.length,
        itemBuilder: (context, i) {
          final lenh = _lenh[i];
          final laMua = lenh.loaiLenh == 'Mua';
          final color = laMua
              ? Colors.green
              : lenh.loaiLenh == 'Ban'
                  ? Colors.red
                  : Colors.orange;
          return Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: GestureDetector(
              onTap: () => _openForm(existing: lenh),
              child: GlassCard(
                child: Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.15),
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        lenh.loaiLenh.toUpperCase(),
                        style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 11),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('${lenh.symbol} · ${_formatVnd(lenh.khoiLuong)} cp',
                              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                          const SizedBox(height: 2),
                          Text(
                            lenh.loaiLenh == 'DieuChinh'
                                ? '${formatApiDateTime(lenh.thoiDiemKhop)} · ${lenh.ghiChu ?? ''}'
                                : '${_formatVnd(lenh.giaKhop)} · ${formatApiDateTime(lenh.thoiDiemKhop)}',
                            style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant),
                          ),
                          if (lenh.lechSoTinHieu != null)
                            Text(
                              'tín hiệu ${lenh.nguonTinHieu} · lệch ${lenh.lechSoTinHieu! > 0 ? '+' : ''}'
                                  '${_formatVnd(lenh.lechSoTinHieu!)} đ'
                                  '${lenh.lechSoTinHieuPhanTram != null ? ' (${lenh.lechSoTinHieuPhanTram!.toStringAsFixed(2)}%)' : ''}',
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: lenh.lechSoTinHieu! >= 0 ? Colors.orange : Colors.lightBlue,
                              ),
                            ),
                          if (lenh.ketQuaTinHieu != null || lenh.ketQuaTinHieuPhanTram != null)
                            Text(
                              'kết quả tín hiệu: ${lenh.ketQuaTinHieu ?? '—'}'
                              '${lenh.ketQuaTinHieuPhanTram != null ? ' · ${lenh.ketQuaTinHieuPhanTram! > 0 ? '+' : ''}${lenh.ketQuaTinHieuPhanTram!.toStringAsFixed(2)}%' : ''}',
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: (lenh.ketQuaTinHieuPhanTram ?? 0) >= 0 ? Colors.green : Colors.red,
                              ),
                            ),
                        ],
                      ),
                    ),
                    IconButton(
                      onPressed: () => _delete(lenh),
                      icon: const Icon(Icons.delete_outline, size: 18),
                      visualDensity: VisualDensity.compact,
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildViThe() {
    if (_viThe.isEmpty) {
      return const Center(child: Text('Không có vị thế nào.'));
    }
    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 80),
      itemCount: _viThe.length,
      itemBuilder: (context, i) {
        final vt = _viThe[i];
        final mauLaiLo = (vt.loiNhuanRongVND ?? 0) >= 0 ? Colors.green : Colors.red;
        return Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: GlassCard(
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Text(vt.symbol, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14)),
                          if (vt.daDong) ...[
                            const SizedBox(width: 6),
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                              decoration: BoxDecoration(
                                color: Colors.blueGrey.withValues(alpha: 0.15),
                                borderRadius: BorderRadius.circular(5),
                              ),
                              child: const Text('ĐÃ ĐÓNG', style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: Colors.blueGrey)),
                            ),
                          ],
                        ],
                      ),
                      const SizedBox(height: 2),
                      Text(
                        vt.daDong
                            ? 'đã đóng toàn bộ · bán ${vt.soLenhDaBan} lệnh'
                            : '${_formatVnd(vt.khoiLuongMo)} cp · vốn BQ ${_formatVnd(vt.giaVonBinhQuan)}',
                        style: const TextStyle(fontSize: 12),
                      ),
                      if (vt.loiNhuanRongVND != null)
                        Text(
                          'lãi/lỗ đã thực hiện ${vt.loiNhuanRongVND! > 0 ? '+' : ''}${_formatVnd(vt.loiNhuanRongVND!)} đ'
                              '${vt.returnOnDeployedPercent != null ? ' (${vt.returnOnDeployedPercent! > 0 ? '+' : ''}${vt.returnOnDeployedPercent!.toStringAsFixed(2)}%)' : ''}',
                          style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: mauLaiLo),
                        ),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    if (!vt.daDong)
                      Text('${_formatVnd(vt.tienVonDangKe)} đ', style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
                    Text('đã bán ${vt.soLenhDaBan}', style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                    if (vt.ngayDong != null)
                      Text('đóng ${vt.ngayDong}', style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildThongKe() {
    final tk = _thongKe;
    return ListView(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 80),
      children: [
        Wrap(
          spacing: 8,
          children: [
            for (final ky in const ['week', 'month', 'quarter', 'all'])
              ChoiceChip(
                label: Text(_tenKy(ky)),
                selected: _ky == ky,
                onSelected: (_) {
                  setState(() => _ky = ky);
                  _reload();
                },
              ),
          ],
        ),
        const SizedBox(height: 8),
        if (tk == null)
          const Padding(padding: EdgeInsets.all(24), child: Text('Chưa có số liệu.'))
        else ...[
          GlassCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Vị thế đã đóng · thắng ${tk.tang} / thua ${tk.thua} / hòa ${tk.ngang} · chờ đóng ${tk.choDo}',
                    style: const TextStyle(fontSize: 12)),
                const SizedBox(height: 4),
                _dongTk(
                  'Tỷ lệ thắng',
                  tk.tyLeThang != null ? '${tk.tyLeThang!.toStringAsFixed(2)}%' : '—',
                  'thắng = lãi ròng > ${tk.nguongThangPhanTram.toStringAsFixed(2)}% (loại vị thế đang mở)',
                ),
                _dongTk(
                  'Tổng lãi/lỗ ròng',
                  '${tk.tongLoiNhuanRongVND > 0 ? '+' : ''}${_formatVnd(tk.tongLoiNhuanRongVND)} đ',
                  'sau phí mua + phí bán + thuế bán',
                  mau: tk.tongLoiNhuanRongVND >= 0 ? Colors.green : Colors.red,
                ),
                _dongTk('Tổng vốn đã dùng', '${_formatVnd(tk.tongDoLosuDungVND)} đ', 'theo giá vốn BQ từng nhịp bán'),
                _dongTk('Số lệnh', '${tk.soLenh}', 'trong kỳ'),
              ],
            ),
          ),
          const SizedBox(height: 8),
          GlassCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _dongTk(
                  'Tỷ lệ khớp tín hiệu',
                  tk.tyLeKhopTinHieu != null ? '${tk.tyLeKhopTinHieu!.toStringAsFixed(2)}%' : '—',
                  '${tk.tinHieuCoLenh} tín hiệu có lệnh / ${tk.tinHieuBoQua} bỏ qua',
                ),
                _dongTk(
                  'Lệch TB so tín hiệu',
                  tk.lechTBSoTinHieuPhanTram != null ? '${tk.lechTBSoTinHieuPhanTram!.toStringAsFixed(2)}%' : '—',
                  'mua ${tk.lechTBMuaPhanTram != null ? '${tk.lechTBMuaPhanTram!.toStringAsFixed(2)}%' : '—'} · bán ${tk.lechTBBanPhanTram != null ? '${tk.lechTBBanPhanTram!.toStringAsFixed(2)}%' : '—'}',
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }

  String _tenKy(String ky) => switch (ky) {
        'week' => 'Tuần',
        'month' => 'Tháng',
        'quarter' => 'Quý',
        _ => 'Tất cả',
      };

  Widget _dongTk(String nhan, String giaTri, String chuThich, {Color? mau}) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(nhan, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
                  Text(chuThich, style: TextStyle(fontSize: 11, color: Theme.of(context).colorScheme.onSurfaceVariant)),
                ],
              ),
            ),
            Text(giaTri, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w800, color: mau)),
          ],
        ),
      );
}
