import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../screens/alerts_screen.dart';
import '../../screens/alert_history_screen.dart';
import '../../screens/criteria_screen.dart';
import '../../screens/home_screen.dart';
import '../../screens/jobs_screen.dart';
import '../../screens/login_screen.dart';
import '../../screens/stock_detail_screen.dart';
import '../../screens/watchlist_screen.dart';
import '../../widgets/app_shell.dart';
import 'app_pages.dart';

final rootNavigatorKey = GlobalKey<NavigatorState>(debugLabel: 'root');
final shellNavigatorKey = GlobalKey<NavigatorState>(debugLabel: 'shell');

GoRouter createAppRouter() {
  return GoRouter(
    navigatorKey: rootNavigatorKey,
    initialLocation: '/',
    routes: [
      GoRoute(
        path: '/login',
        parentNavigatorKey: rootNavigatorKey,
        pageBuilder: (context, state) => appPushedPage(
          key: state.pageKey,
          child: const LoginScreen(),
        ),
      ),
      ShellRoute(
        navigatorKey: shellNavigatorKey,
        builder: (context, state, child) {
          final path = state.uri.path;
          final index = switch (path) {
            '/alerts' => 1,
            '/watchlist' => 2,
            '/criteria' => 3,
            '/performance' => 4,
            _ => 0,
          };
          final title = switch (path) {
            '/alerts' => 'Khớp lệnh',
            '/watchlist' => 'Watchlist',
            '/criteria' => 'Phân tích chỉ báo',
            '/performance' => 'Hiệu quả',
            _ => 'Trang chủ',
          };
          return MobileShell(navIndex: index, title: title, child: child);
        },
        routes: [
          GoRoute(
            path: '/',
            pageBuilder: (context, state) => appTabPage(
              key: state.pageKey,
              child: const HomeScreen(),
            ),
          ),
          GoRoute(
            path: '/alerts',
            pageBuilder: (context, state) => appTabPage(
              key: state.pageKey,
              child: const AlertsScreen(),
            ),
          ),
          GoRoute(
            path: '/watchlist',
            pageBuilder: (context, state) => appTabPage(
              key: state.pageKey,
              child: const WatchlistScreen(),
            ),
          ),
          GoRoute(
            path: '/criteria',
            pageBuilder: (context, state) => appTabPage(
              key: state.pageKey,
              child: const CriteriaScreen(),
            ),
          ),
          GoRoute(
            path: '/performance',
            pageBuilder: (context, state) => appTabPage(
              key: state.pageKey,
              child: const AlertHistoryScreen(),
            ),
          ),
        ],
      ),
      GoRoute(
        path: '/stocks/:symbol',
        parentNavigatorKey: rootNavigatorKey,
        pageBuilder: (context, state) => appPushedPage(
          key: state.pageKey,
          child: StockDetailScreen(
            symbol: state.pathParameters['symbol']!.toUpperCase(),
          ),
        ),
      ),
      GoRoute(
        path: '/jobs',
        parentNavigatorKey: rootNavigatorKey,
        pageBuilder: (context, state) => appPushedPage(
          key: state.pageKey,
          child: const JobsScreen(),
        ),
      ),
    ],
  );
}
