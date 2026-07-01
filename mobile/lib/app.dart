import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/api/api_client.dart';
import 'core/services/app_services.dart';
import 'core/services/market_hub_service.dart';
import 'core/theme/app_theme.dart';
import 'widgets/app_shell.dart';
import 'widgets/juice_logo.dart';
import 'widgets/wave_background.dart';
import 'screens/alerts_screen.dart';
import 'screens/criteria_screen.dart';
import 'screens/home_screen.dart';
import 'screens/jobs_screen.dart';
import 'screens/login_screen.dart';
import 'screens/performance_screen.dart';
import 'screens/stock_detail_screen.dart';
import 'screens/watchlist_screen.dart';

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
  late final GoRouter _router;
  var _ready = false;

  @override
  void initState() {
    super.initState();
    _router = GoRouter(
      initialLocation: '/',
      routes: [
        GoRoute(path: '/login', builder: (_, __) => const LoginScreen()),
        ShellRoute(
          builder: (context, state, child) {
            final path = state.uri.path;
            final index = switch (path) {
              '/alerts' => 1,
              '/watchlist' => 2,
              '/criteria' => 3,
              _ => 0,
            };
            final title = switch (path) {
              '/alerts' => 'Lệnh realtime',
              '/watchlist' => 'Watchlist',
              '/criteria' => 'Phân tích chỉ báo',
              '/performance' => 'Hiệu quả',
              '/jobs' => 'Jobs',
              _ => 'Trang chủ',
            };
            return MobileShell(navIndex: index, title: title, child: child);
          },
          routes: [
            GoRoute(path: '/', builder: (_, __) => const HomeScreen()),
            GoRoute(path: '/alerts', builder: (_, __) => const AlertsScreen()),
            GoRoute(path: '/watchlist', builder: (_, __) => const WatchlistScreen()),
            GoRoute(path: '/criteria', builder: (_, __) => const CriteriaScreen()),
            GoRoute(path: '/performance', builder: (_, __) => const PerformanceScreen()),
            GoRoute(path: '/jobs', builder: (_, __) => const JobsScreen()),
          ],
        ),
        GoRoute(
          path: '/stocks/:symbol',
          builder: (_, state) => StockDetailScreen(symbol: state.pathParameters['symbol']!.toUpperCase()),
        ),
      ],
    );
    _bootstrap();
  }

  @override
  void dispose() {
    _marketHub.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    await Future.wait([_auth.load(), _theme.load()]);
    if (mounted) setState(() => _ready = true);
    // Hub kết nối nền — không chặn hiển thị UI.
    _marketHub.start();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: _api),
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
