import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/api/api_client.dart';
import 'core/navigation/app_router.dart';
import 'core/services/app_services.dart';
import 'core/services/market_hub_service.dart';
import 'core/services/push_service.dart';
import 'core/theme/app_theme.dart';
import 'widgets/juice_logo.dart';
import 'widgets/wave_background.dart';

class JuiceApp extends StatefulWidget {
  const JuiceApp({super.key});

  @override
  State<JuiceApp> createState() => _JuiceAppState();
}

class _JuiceAppState extends State<JuiceApp> {
  late final ApiClient _api = ApiClient();
  late final AuthService _auth = AuthService(_api);
  late final ThemeService _theme = ThemeService();
  late final MarketHubService _marketHub = MarketHubService(_api);
  late final GoRouter _router = createAppRouter();
  late final PushService _push = PushService(
    _api,
    dieuHuong: (duongDan) => _router.push(duongDan),
    daDangNhap: () => _auth.isLoggedIn,
  );
  var _ready = false;

  @override
  void dispose() {
    _marketHub.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    _auth.onSessionExpired = _onSessionExpired;
    _auth.onLoggedIn = _sauKhiDangNhap;
    _auth.onLoggingOut = _push.goToken;
    await Future.wait([_auth.load(), _theme.load()]);
    if (mounted) setState(() => _ready = true);
    _marketHub.start();
    // Hai kênh Android phải có trước khi xin quyền và trước mọi tin đẩy (plan §(g)).
    await _push.taoKenhAndroid();
    await _push.khoiDong();
    if (_auth.isLoggedIn) await _sauKhiDangNhap();
  }

  /// Sau đăng nhập hoặc mở lại app khi đã đăng nhập: đăng ký máy rồi mở mã đang chờ (T027).
  Future<void> _sauKhiDangNhap() async {
    await _push.dangKyToken();
    _push.guiDuongDanCho();
  }

  /// 401 từ bất kỳ màn nào: báo rõ rồi đưa về /login — không đăng xuất im lặng.
  void _onSessionExpired() {
    rootNavigatorKey.currentState?.context.go('/login');
    final context = rootNavigatorKey.currentContext;
    if (context != null) {
      ScaffoldMessenger.of(context)
        ..clearSnackBars()
        ..showSnackBar(
          const SnackBar(content: Text('Phiên đăng nhập đã hết, vui lòng đăng nhập lại.')),
        );
    }
  }

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: _api),
        Provider<PushService>.value(value: _push),
        ChangeNotifierProvider<AuthService>.value(value: _auth),
        ChangeNotifierProvider<ThemeService>.value(value: _theme),
        ChangeNotifierProvider<MarketHubService>.value(value: _marketHub),
      ],
      child: Consumer<ThemeService>(
        builder: (_, theme, __) => MaterialApp.router(
          title: 'JUICE',
          debugShowCheckedModeBanner: false,
          theme: AppTheme.light(),
          darkTheme: AppTheme.dark(),
          themeMode: theme.mode,
          routerConfig: _router,
          builder: (context, child) {
            if (!_ready) {
              return WaveBackground(
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 32),
                    child: JuiceLogo(
                      variant: JuiceLogoVariant.full,
                      size: JuiceLogoSize.lg,
                      isDarkOverride: theme.isDark,
                    ),
                  ),
                ),
              );
            }
            return child ?? const SizedBox.shrink();
          },
        ),
      ),
    );
  }
}
