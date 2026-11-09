import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:juice_app/screens/nhat_ky_lenh_screen.dart';

void main() {
  testWidgets('form ghi lệnh mới được điền sẵn từ vị thế đang giữ', (tester) async {
    // Cảnh báo debug có sẵn của form: ListTile trong khung có màu nền (chỉ ảnh hưởng hiệu ứng chạm).
    // Chỉ bỏ đúng cảnh báo này; lỗi khác vẫn làm test hỏng.
    final truoc = FlutterError.onError;
    FlutterError.onError = (details) {
      if (details.exceptionAsString().contains('ListTile background color')) return;
      truoc?.call(details);
    };
    try {
      await tester.pumpWidget(const MaterialApp(
        home: Scaffold(
          body: NhatKyLenhScreen(
            prefill: NhatKyLenhPrefill(
              symbol: 'FPT',
              loaiLenh: 'Ban',
              nguonTinHieu: 'V3',
              maTinHieu: '901',
              giaKhop: 54.1,
              khoiLuong: 200,
            ),
          ),
        ),
      ));

      expect(find.widgetWithText(TextFormField, 'FPT'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, '54.10'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, '200'), findsOneWidget);
      expect(find.textContaining('Đã điền sẵn từ vị thế đang giữ'), findsOneWidget);
      final chipBan = tester.widget<ChoiceChip>(find.widgetWithText(ChoiceChip, 'Bán'));
      expect(chipBan.selected, isTrue);
      final chipMua = tester.widget<ChoiceChip>(find.widgetWithText(ChoiceChip, 'Mua'));
      expect(chipMua.selected, isFalse);
    } finally {
      FlutterError.onError = truoc;
    }
  });
}
