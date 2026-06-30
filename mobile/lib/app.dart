import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/api/api_client.dart';
import 'core/services/app_services.dart';
import 'core/theme/app_theme.dart';
import 'screens/alerts_screen.dart';
import 'screens/criteria_screen.dart';
import 'screens/home_screen.dart';
import 'screens/login_screen.dart';
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
            return ShellScreen(navIndex: index, child: child);
          },
          routes: [
            GoRoute(path: '/', builder: (_, __) => const HomeScreen()),
            GoRoute(path: '/alerts', builder: (_, __) => const AlertsScreen()),
            GoRoute(path: '/watchlist', builder: (_, __) => const WatchlistScreen()),
            GoRoute(path: '/criteria', builder: (_, __) => const CriteriaScreen()),
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

  Future<void> _bootstrap() async {
    await Future.wait([_auth.load(), _theme.load()]);
    if (mounted) setState(() => _ready = true);
  }

  @override
  Widget build(BuildContext context) {
    if (!_ready) {
      return MaterialApp(
        home: Scaffold(
          backgroundColor: const Color(0xFF111319),
          body: Center(child: Image.asset('assets/juice-logo-dark.png', width: 160)),
        ),
      );
    }

    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: _api),
        ChangeNotifierProvider<AuthService>.value(value: _auth),
        ChangeNotifierProvider<ThemeService>.value(value: _theme),
      ],
      child: Consumer<ThemeService>(
        builder: (_, theme, __) => MaterialApp.router(
          title: 'JUICE',
          debugShowCheckedModeBanner: false,
          theme: AppTheme.light(),
          darkTheme: AppTheme.dark(),
          themeMode: theme.mode,
          routerConfig: _router,
        ),
      ),
    );
  }
}
