import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../api/api_client.dart';

/// Kênh Android cho tin V3 và tin thử (FR-004).
const kenhTinHieuV3 = 'v3-signal';

/// Kênh Android cho tin hệ thống / HPO (FR-001a).
const kenhHeThong = 'he-thong';

/// Handler background bắt buộc là hàm top-level; Android tự hiển thị notification payload.
@pragma('vm:entry-point')
Future<void> tinDayNen(RemoteMessage tin) async {}

/// Đẩy thông báo phía app: quyền, mã FCM, hiển thị foreground và điều hướng khi chạm.
///
/// Không khởi tạo FirebaseApp ở đây — `main.dart` đã làm trước `runApp` (plan §(g)).
class PushService {
  PushService(
    this._api, {
    required this.dieuHuong,
    required this.daDangNhap,
    Future<String?> Function()? layMaFcm,
  }) : _layMaFcm = layMaFcm ?? _docMaFcm;

  static const khoaMaMay = 'stockradar_device_token';
  static const _khoaQuyen = 'stockradar_push_permission';

  /// `main.dart` bật cờ này sau khi `Firebase.initializeApp()` thành công.
  static bool firebaseSanSang = false;

  final ApiClient _api;

  /// Điều hướng bằng `GoRouter.push` để nút Quay lại về màn trước (US2 AC2).
  final void Function(String duongDan) dieuHuong;

  /// Nguồn sự thật của phiên đăng nhập, để biết lưu `pendingRoute` hay điều hướng ngay.
  final bool Function() daDangNhap;

  /// Lấy mã FCM hiện tại; tham số cho phép test thuần không cần Firebase.
  final Future<String?> Function() _layMaFcm;

  final _thongBaoLocal = FlutterLocalNotificationsPlugin();
  var _daKhoiTaoLocal = false;

  /// Quyền POST_NOTIFICATIONS đã được cấp; false thì snackbar nhắc bật trong Cài đặt (US1 AC4).
  bool daDuocQuyen = false;

  /// Đường dẫn chờ đăng nhập xong mới mở; chỉ giữ một giá trị.
  String? pendingRoute;

  /// true khi `Firebase.initializeApp()` chạy được — thiếu google-services.json thì false.
  bool get kenhDaSanSang => firebaseSanSang;

  /// Tạo 2 kênh Android, idempotent, gọi mỗi lần app chạy và trước khi xin quyền.
  Future<void> taoKenhAndroid() async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;
    if (!_daKhoiTaoLocal) {
      await _thongBaoLocal.initialize(
        settings: const InitializationSettings(
          android: AndroidInitializationSettings('@mipmap/ic_launcher'),
        ),
        onDidReceiveNotificationResponse: (phanHoi) => moTheoDuongDan(phanHoi.payload),
      );
      _daKhoiTaoLocal = true;
    }
    final android =
        _thongBaoLocal.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();
    await android?.createNotificationChannel(const AndroidNotificationChannel(
      kenhTinHieuV3,
      'Tín hiệu V3',
      description: 'Tín hiệu mua/bán V3 và tin thử',
      importance: Importance.max,
    ));
    await android?.createNotificationChannel(const AndroidNotificationChannel(
      kenhHeThong,
      'Hệ thống',
      description: 'Tin hệ thống và HPO',
      importance: Importance.defaultImportance,
    ));
  }

  /// Xin quyền thông báo (Android 13+) và ghi nhớ kết quả để nhắc lại trong app.
  Future<bool> xinQuyen() async {
    final prefs = await SharedPreferences.getInstance();
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) {
      daDuocQuyen = prefs.getBool(_khoaQuyen) ?? false;
      return daDuocQuyen;
    }
    final android =
        _thongBaoLocal.resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();
    daDuocQuyen = await android?.requestNotificationsPermission() ?? false;
    await prefs.setBool(_khoaQuyen, daDuocQuyen);
    return daDuocQuyen;
  }

  /// Mở cài đặt thông báo của app — nút trong snackbar khi quyền bị chặn.
  Future<void> moCaiDatThongBao() async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;
    try {
      await _thongBaoLocal.openAppNotificationSettings();
    } catch (_) {
      // BUSINESS-RULE: Không mở được cài đặt thì bỏ qua, nút nhắc không được làm sập màn.
    }
  }

  /// Đăng ký mã FCM của máy cho user đang đăng nhập; mã đổi thì gửi kèm `tokenCu`.
  Future<void> dangKyToken() async {
    if (!kenhDaSanSang || !daDangNhap()) return;
    final prefs = await SharedPreferences.getInstance();
    final maCu = prefs.getString(khoaMaMay);
    String? maMoi;
    try {
      maMoi = await _layMaFcm();
    } catch (_) {
      // BUSINESS-RULE: Thiếu cấu hình Firebase thì bỏ qua, đăng nhập vẫn phải thành công (FR-013).
      return;
    }
    if (maMoi == null || maMoi.isEmpty || maMoi == maCu) return;
    try {
      await _api.dangKyThietBi(maMoi, tokenCu: maCu);
      await prefs.setString(khoaMaMay, maMoi);
    } on ApiException {
      // BUSINESS-RULE: Lỗi mạng khi đăng ký không chặn đăng nhập; lần mở app sau sẽ đăng ký lại.
    }
  }

  /// Gỡ mã thiết bị khi bấm Đăng xuất: một lần duy nhất, không thử lại, lỗi không chặn đăng xuất.
  Future<void> goToken() async {
    final prefs = await SharedPreferences.getInstance();
    final ma = prefs.getString(khoaMaMay);
    if (ma == null || ma.isEmpty) return;
    try {
      await _api.goThietBi(ma);
      await prefs.remove(khoaMaMay);
    } catch (_) {
      // BUSINESS-RULE: Gỡ lỗi thì giữ mã trong máy, không thử lại — FCM báo hỏng thì server tự xoá (FR-009).
    }
  }

  /// Lắng nghe tin, xin quyền và đăng ký máy; gọi một lần từ `app.dart` sau khi giao diện sẵn sàng.
  Future<void> khoiDong() async {
    final prefs = await SharedPreferences.getInstance();
    daDuocQuyen = prefs.getBool(_khoaQuyen) ?? false;
    await xinQuyen();
    if (!kenhDaSanSang) return;

    FirebaseMessaging.onMessage.listen(_hienThiKhiDangMo);
    FirebaseMessaging.onMessageOpenedApp.listen((tin) => moTheoDuongDan(duongDanTuTin(_duLieu(tin))));
    // LIMIT: FCM v17 đã deprecate getToken/onTokenRefresh cho API FID, nhưng server vẫn gửi theo Message.Token.
    // Giữ cặp cũ tới khi cả hai phía chuyển sang FID; bỏ ngay sẽ mất tin đẩy.
    // ignore: deprecated_member_use
    FirebaseMessaging.instance.onTokenRefresh.listen((_) => dangKyToken());

    final banDau = await FirebaseMessaging.instance.getInitialMessage();
    if (banDau != null) moTheoDuongDan(duongDanTuTin(_duLieu(banDau)));
  }

  /// Lấy mã đăng ký FCM hiện tại; `layMaFcm` cho phép test thuần không cần Firebase.
  // ignore: deprecated_member_use
  static Future<String?> _docMaFcm() => FirebaseMessaging.instance.getToken();

  /// `RemoteMessage.data` là `Map<String, dynamic>`; đổi sang chuỗi để `duongDanTuTin` thuần và test được.
  static Map<String, String> _duLieu(RemoteMessage tin) =>
      tin.data.map((khoa, giaTri) => MapEntry(khoa, giaTri?.toString() ?? ''));

  /// Hàm thuần: `data.symbol` ⇒ `/stocks/<MÃ>`; không có symbol (he_thong, thu) ⇒ null, chỉ mở app.
  static String? duongDanTuTin(Map<String, String> data) {
    final ma = (data['symbol'] ?? '').trim().toUpperCase();
    if (ma.isEmpty) return null;
    return '/stocks/$ma';
  }

  /// Điều hướng tới mã trong tin; chưa đăng nhập thì giữ lại một lần cho sau khi đăng nhập.
  void moTheoDuongDan(String? duongDan) {
    final tuyen = duongDan?.trim();
    if (tuyen == null || tuyen.isEmpty) return;
    if (!daDangNhap()) {
      pendingRoute = tuyen;
      return;
    }
    dieuHuong(tuyen);
  }

  /// Mở mã đang chờ sau khi đăng nhập xong rồi xoá, để không mở lại lần sau.
  void guiDuongDanCho() {
    final tuyen = pendingRoute;
    if (tuyen == null) return;
    pendingRoute = null;
    dieuHuong(tuyen);
  }

  Future<void> _hienThiKhiDangMo(RemoteMessage tin) async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;
    final kenh = tin.data['nguon'] == 'he_thong' ? kenhHeThong : kenhTinHieuV3;
    await _thongBaoLocal.show(
      id: (tin.messageId ?? DateTime.now().microsecondsSinceEpoch.toString()).hashCode & 0x7fffffff,
      title: tin.notification?.title ?? 'JUICE',
      body: tin.notification?.body ?? '',
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          kenh,
          kenh == kenhHeThong ? 'Hệ thống' : 'Tín hiệu V3',
          importance: kenh == kenhHeThong ? Importance.defaultImportance : Importance.max,
          priority: Priority.high,
        ),
      ),
      payload: duongDanTuTin(_duLieu(tin)),
    );
  }
}
