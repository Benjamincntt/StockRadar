import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:juice_app/core/models/models.dart';
import 'package:juice_app/widgets/scenario_widgets.dart';

void main() {
  group('WatchlistItem.fromJson', () {
    test('đọc trạng thái V3 khi server trả', () {
      final item = WatchlistItem.fromJson({
        'symbol': 'HPG',
        'name': 'Hòa Phát',
        'sector': 'Thép',
        'changePercent': 1.5,
        'trangThaiV3': 'DangGiu',
      });

      expect(item.trangThaiV3, 'DangGiu');
      expect(item.changePercent, 1.5);
    });

    test('server cũ không có trường mới hoặc server trả null: trạng thái null, không vỡ', () {
      final cu = WatchlistItem.fromJson({'symbol': 'HPG', 'score': 70});
      final khongCoGi = WatchlistItem.fromJson({'symbol': 'HPG', 'trangThaiV3': null});

      expect(cu.trangThaiV3, isNull);
      expect(khongCoGi.trangThaiV3, isNull);
    });
  });

  group('HuyHieuTrangThaiV3', () {
    Future<void> hien(WidgetTester tester, String? trangThai) => tester.pumpWidget(
          MaterialApp(home: Scaffold(body: HuyHieuTrangThaiV3(trangThai: trangThai))),
        );

    testWidgets('hiện đúng nhãn cho từng trạng thái', (tester) async {
      await hien(tester, 'DangGiu');
      expect(find.text('Đang giữ'), findsOneWidget);

      await hien(tester, 'ChoMua');
      expect(find.text('Chờ mua'), findsOneWidget);

      await hien(tester, 'CoTinHieuMoi');
      expect(find.text('Có tín hiệu'), findsOneWidget);
    });

    testWidgets('trạng thái null hoặc lạ thì không hiện gì', (tester) async {
      await hien(tester, null);
      expect(find.byType(Text), findsNothing);

      await hien(tester, 'GiaTriLa');
      expect(find.byType(Text), findsNothing);
    });
  });
}
