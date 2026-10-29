import 'package:flutter_test/flutter_test.dart';

import 'package:juice_app/core/utils/v3_parsers.dart';

void main() {
  group('parseSoVn', () {
    test('dấu chấm thập phân và phẩy thập phân đều ra 25.5', () {
      expect(parseSoVn('25.5'), 25.5);
      expect(parseSoVn('25,5'), 25.5);
    });

    test('nhóm nghìn kiểu 25.500 ra 25500 (không âm thầm thành 25.5 hay 255)', () {
      expect(parseSoVn('25.500'), 25500);
      expect(parseSoVn('1.234.567'), 1234567);
    });

    test('số không phân cách giữ nguyên', () {
      expect(parseSoVn('25500'), 25500);
      expect(parseSoVn('25'), 25);
    });

    test('phẩy thập phân + chấm nhóm nghìn', () {
      expect(parseSoVn('25.500,5'), 25500.5);
    });

    test('dấu âm/dương cho giá trị điều chỉnh', () {
      expect(parseSoVn('+100'), 100);
      expect(parseSoVn('-100'), -100);
    });

    test('rỗng hoặc sai định dạng trả null', () {
      expect(parseSoVn(''), isNull);
      expect(parseSoVn('1.2.3'), isNull);
      expect(parseSoVn('abc'), isNull);
    });
  });

  group('v3LyDoThoatLabel', () {
    test('bản đồ các giá trị enum đã biết', () {
      expect(v3LyDoThoatLabel('ChamMA'), 'Chạm MA');
      expect(v3LyDoThoatLabel('ChotLoiHoangLoan'), 'Chốt +15% hoảng loạn sâu');
      expect(v3LyDoThoatLabel('HetHanATC'), 'Hết hạn giữ (ATC)');
      expect(v3LyDoThoatLabel('AtoTheoLich'), 'ATO đầu tháng sau');
    });

    test('chưa thoát trả rỗng', () {
      expect(v3LyDoThoatLabel(null), '');
      expect(v3LyDoThoatLabel(''), '');
      expect(v3LyDoThoatLabel('Khong'), '');
    });

    test('giá trị lạ trả "Khác", không in chuỗi thô', () {
      expect(v3LyDoThoatLabel('Foo'), 'Khác');
    });
  });
}
