import 'package:flutter_test/flutter_test.dart';
import 'package:juice_app/core/models/vi_the_dang_giu.dart';

void main() {
  ViTheDangGiu viThe(Map<String, dynamic> extra) => ViTheDangGiu.fromJson({
        'symbol': 'hpg',
        'loai': 'KB1',
        'giaVao': 25.0,
        'soPhienDaGi': 5,
        ...extra,
      });

  test('đọc đủ trường server mới trả', () {
    final v = viThe({'giaChotLoi': 27.5, 'soPhienConLai': 10});

    expect(v.symbol, 'HPG');
    expect(v.giaVao, 25.0);
    expect(v.giaChotLoi, 27.5);
    expect(v.soPhienConLai, 10);
    expect(v.soPhienDaGiu, 5);
  });

  test('server cũ chưa có trường mới: không vỡ, trường mới là null', () {
    final v = viThe({});

    expect(v.giaChotLoi, isNull);
    expect(v.soPhienConLai, isNull);
  });

  test('lãi lỗ theo % so với giá mua', () {
    final v = viThe({});

    expect(v.laiLoPhanTram(27.5), closeTo(10.0, 1e-9));
    expect(v.laiLoPhanTram(22.5), closeTo(-10.0, 1e-9));
  });

  test('thiếu giá hiện tại hoặc giá mua thì không tính', () {
    expect(viThe({}).laiLoPhanTram(null), isNull);
    expect(viThe({}).laiLoPhanTram(0), isNull);
    expect(viThe({'giaVao': null}).laiLoPhanTram(27.5), isNull);
  });

  test('N3 bán theo lịch, kịch bản khác không', () {
    expect(viThe({'loai': 'N3'}).banTheoLich, isTrue);
    expect(viThe({'loai': 'KB4'}).banTheoLich, isFalse);
  });
}
