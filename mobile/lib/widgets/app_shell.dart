import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/services/app_services.dart';
import '../core/theme/app_colors.dart';
import '../widgets/app_bottom_nav.dart';
import '../widgets/juice_logo.dart';
import '../widgets/live_quote.dart';
import '../widgets/wave_background.dart';

class MobileShell extends StatelessWidget {
  const MobileShell({super.key, required this.child, required this.navIndex, required this.title});

  final Widget child;
  final int navIndex;
  final String title;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      resizeToAvoidBottomInset: true,
      backgroundColor: Colors.transparent,
      drawer: const _AppDrawer(),
      body: WaveBackground(
        child: Column(
          children: [
            Builder(builder: (ctx) => AppTopBar(title: title, onMenu: () => Scaffold.of(ctx).openDrawer())),
            Expanded(
              child: Align(
                alignment: Alignment.topCenter,
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: AppColors.maxContentWidth),
                  child: child,
                ),
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: AppBottomNav(currentIndex: navIndex),
    );
  }
}

class _AppDrawer extends StatelessWidget {
  const _AppDrawer();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Drawer(
      backgroundColor: AppColors.glassBg(context),
      child: SafeArea(
        child: ListView(
          padding: const EdgeInsets.symmetric(vertical: 8),
          children: [
            const Padding(
              padding: EdgeInsets.all(16),
              child: JuiceLogo(variant: JuiceLogoVariant.full, size: JuiceLogoSize.sm),
            ),
            const Divider(height: 1),
            ListTile(
              leading: Icon(Icons.home_outlined, color: scheme.onSurface),
              title: const Text('Trang chủ'),
              onTap: () {
                Navigator.pop(context);
                context.go('/');
              },
            ),
            ListTile(
              leading: Icon(Icons.trending_up, color: scheme.onSurface),
              title: const Text('Hiệu quả'),
              subtitle: const Text('Performance review', style: TextStyle(fontSize: 11)),
              onTap: () {
                Navigator.pop(context);
                context.push('/performance');
              },
            ),
            ListTile(
              leading: Icon(Icons.sync, color: scheme.onSurface),
              title: const Text('Jobs'),
              subtitle: const Text('Đồng bộ dữ liệu', style: TextStyle(fontSize: 11)),
              onTap: () {
                Navigator.pop(context);
                context.push('/jobs');
              },
            ),
            const Divider(height: 1),
            ListTile(
              leading: Icon(Icons.notifications_outlined, color: scheme.onSurface),
              title: const Text('Lệnh realtime'),
              onTap: () {
                Navigator.pop(context);
                context.go('/alerts');
              },
            ),
            ListTile(
              leading: Icon(Icons.star_outline, color: scheme.onSurface),
              title: const Text('Watchlist'),
              onTap: () {
                Navigator.pop(context);
                context.go('/watchlist');
              },
            ),
            ListTile(
              leading: Icon(Icons.tune, color: scheme.onSurface),
              title: const Text('Phân tích chỉ báo'),
              onTap: () {
                Navigator.pop(context);
                context.go('/criteria');
              },
            ),
          ],
        ),
      ),
    );
  }
}

class AppTopBar extends StatelessWidget {
  const AppTopBar({super.key, required this.title, required this.onMenu});

  final String title;
  final VoidCallback onMenu;

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthService>();
    final themeService = context.watch<ThemeService>();
    final scheme = Theme.of(context).colorScheme;
    final isLight = !themeService.isDark;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.headerBg(context),
        border: Border(bottom: BorderSide(color: scheme.outline.withValues(alpha: 0.4))),
        boxShadow: isLight ? null : [BoxShadow(color: Colors.black.withValues(alpha: 0.25), blurRadius: 16)],
      ),
      child: SafeArea(
        bottom: false,
        child: SizedBox(
          height: 56,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Row(
              children: [
                SizedBox(
                  width: 40,
                  height: 40,
                  child: IconButton(
                    padding: EdgeInsets.zero,
                    onPressed: onMenu,
                    icon: Icon(Icons.menu, color: scheme.onSurface, size: 22),
                    style: IconButton.styleFrom(
                      backgroundColor: AppColors.surfaceLow(context),
                      shape: const CircleBorder(),
                    ),
                  ),
                ),
                Expanded(
                  child: Center(
                    child: Text(
                      title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: scheme.onSurface,
                      ),
                    ),
                  ),
                ),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const LiveStatusBadge(),
                    IconButton(
                      onPressed: themeService.toggle,
                      icon: Icon(
                        themeService.isDark ? Icons.light_mode_outlined : Icons.dark_mode_outlined,
                        size: 20,
                      ),
                      style: IconButton.styleFrom(
                        minimumSize: const Size(36, 36),
                        padding: EdgeInsets.zero,
                        backgroundColor: AppColors.surfaceLow(context),
                      ),
                    ),
                    if (auth.isLoggedIn)
                      IconButton(
                        onPressed: () async {
                          await auth.logout();
                          if (context.mounted) context.go('/login');
                        },
                        icon: const Icon(Icons.logout, size: 18),
                        style: IconButton.styleFrom(
                          minimumSize: const Size(36, 36),
                          padding: EdgeInsets.zero,
                          backgroundColor: AppColors.surfaceLow(context),
                        ),
                      )
                    else
                      TextButton(
                        onPressed: () => context.go('/login'),
                        style: TextButton.styleFrom(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                          minimumSize: Size.zero,
                          tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                          backgroundColor: isLight
                              ? scheme.primary.withValues(alpha: 0.1)
                              : AppColors.surfaceLow(context),
                          foregroundColor: isLight ? scheme.primary : scheme.onSurface,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(20),
                            side: isLight
                                ? BorderSide.none
                                : BorderSide(color: scheme.outline.withValues(alpha: 0.5)),
                          ),
                        ),
                        child: const Text('Sign In', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700)),
                      ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
