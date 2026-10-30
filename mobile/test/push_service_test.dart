import 'package:flutter_test/flutter_test.dart';
import 'package:juice_app/core/api/api_client.dart';
import 'package:juice_app/core/services/app_services.dart';
import 'package:juice_app/core/services/push_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    PushService.firebaseSanSang = true;
  });

  tearDown(() => PushService.firebaseSanSang = false);

  group('duongDanTuTin', () {
    test('có symbol thì tới Chi tiết mã', () {
      expect(PushService.duongDanTuTin({'symbol': 'HPG'}), '/stocks/HPG');
      expect(PushService.duongDanTuTin({'symbol': 'HPG', 'loaiTin': 'mua'}), '/stocks/HPG');
    });

    test('không có symbol thì không điều hướng', () {
      expect(PushService.duongDanTuTin({'nguon': 'he_thong'}), isNull);
      expect(PushService.duongDanTuTin({'symbol': '   '}), isNull);
    });

    test('symbol viết thường được hoá thành chữ hoa', () {
      expect(PushService.duongDanTuTin({'symbol': 'hpg'}), '/stocks/HPG');
    });
  });

  group('điều hướng', () {
    test('đã đăng nhập thì mở ngay, không giữ pendingRoute', () {
      final daMo = <String>[];
      final push = PushService(ApiGia(), dieuHuong: daMo.add, daDangNhap: () => true);

      push.moTheoDuongDan('/stocks/HPG');

      expect(daMo, ['/stocks/HPG']);
      expect(push.pendingRoute, isNull);
    });

    test('chưa đăng nhập thì giữ một lần rồi mở sau khi đăng nhập', () {
      final daMo = <String>[];
      var daDangNhap = false;
      final push = PushService(ApiGia(), dieuHuong: daMo.add, daDangNhap: () => daDangNhap);

      push.moTheoDuongDan('/stocks/HPG');
      expect(daMo, isEmpty);
      expect(push.pendingRoute, '/stocks/HPG');

      push.moTheoDuongDan('/stocks/SSI');
      expect(push.pendingRoute, '/stocks/SSI');

      daDangNhap = true;
      push.guiDuongDanCho();
      expect(daMo, ['/stocks/SSI']);
      expect(push.pendingRoute, isNull);

      push.guiDuongDanCho();
      expect(daMo, ['/stocks/SSI']);
    });
  });

  group('vòng đời mã thiết bị', () {
    test('đăng xuất thường không gỡ máy, đăng xuất có goMay thì gỡ đúng một lần', () async {
      final api = ApiGia();
      final push = PushService(api, dieuHuong: (_) {}, daDangNhap: () => true);
      final auth = AuthService(api)..onLoggingOut = push.goToken;
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(PushService.khoaMaMay, 'token-may-1234567890');

      await auth.logout();
      expect(api.daGo, isEmpty);

      await auth.logout(goMay: true);
      expect(api.daGo, ['token-may-1234567890']);
      expect(prefs.getString(PushService.khoaMaMay), isNull);
    });

    test('mã FCM đổi thì đăng ký kèm tokenCu và lưu mã mới', () async {
      final api = ApiGia();
      final push = PushService(
        api,
        dieuHuong: (_) {},
        daDangNhap: () => true,
        layMaFcm: () async => 'token-may-moi-00000002',
      );
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(PushService.khoaMaMay, 'token-may-cu-00000001');

      await push.dangKyToken();

      expect(api.daDangKy.single.token, 'token-may-moi-00000002');
      expect(api.daDangKy.single.tokenCu, 'token-may-cu-00000001');
      expect(prefs.getString(PushService.khoaMaMay), 'token-may-moi-00000002');
    });

    test('mã FCM không đổi thì không gọi lại server', () async {
      final api = ApiGia();
      final push = PushService(
        api,
        dieuHuong: (_) {},
        daDangNhap: () => true,
        layMaFcm: () async => 'token-may-cu-00000001',
      );
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(PushService.khoaMaMay, 'token-may-cu-00000001');

      await push.dangKyToken();

      expect(api.daDangKy, isEmpty);
    });

    test('kênh đẩy chưa sẵn sàng thì không đăng ký máy', () async {
      PushService.firebaseSanSang = false;
      final api = ApiGia();
      final push = PushService(
        api,
        dieuHuong: (_) {},
        daDangNhap: () => true,
        layMaFcm: () async => 'token-may-moi-00000002',
      );

      await push.dangKyToken();

      expect(api.daDangKy, isEmpty);
    });
  });
}

class ApiGia extends ApiClient {
  final daDangKy = <({String token, String? tokenCu})>[];
  final daGo = <String>[];

  @override
  Future<Map<String, dynamic>> dangKyThietBi(String token, {String? tokenCu}) async {
    daDangKy.add((token: token, tokenCu: tokenCu));
    return {'daDangKy': true, 'maHoaThongBao': true};
  }

  @override
  Future<void> goThietBi(String token) async => daGo.add(token);

  @override
  Future<Map<String, dynamic>> guiThuThongBao() async => {'guiDuoc': true, 'soThietBi': 1};
}
