import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/services/app_services.dart';
import '../core/theme/app_colors.dart';
import '../widgets/app_bottom_nav.dart';
import '../widgets/exit_confirm_dialog.dart';
import '../widgets/juice_logo.dart';
import '../widgets/live_quote.dart';
import '../widgets/wave_background.dart';

class MobileShell extends StatefulWidget {
  const MobileShell({super.key, required this.child, required this.navIndex, required this.title});

  final Widget child;
  final int navIndex;
  final String title;

  @override
  State<MobileShell> createState() => _MobileShellState();
}

class _MobileShellState extends State<MobileShell> {
  var _busy = false;

  Future<void> _onPopInvoked(bool didPop) async {
    if (didPop || _busy) return;
    _busy = true;
    try {
      final path = GoRouterState.of(context).uri.path;
      if (path != '/') {
        if (mounted) context.go('/');
        return;
      }

      final leave = await showExitConfirmDialog(context);
      if (leave && mounted) {
        await SystemNavigator.pop();
      }
    } finally {
      _busy = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) => _onPopInvoked(didPop),
      child: Scaffold(
        resizeToAvoidBottomInset: true,
        backgroundColor: Colors.transparent,
        body: WaveBackground(
          child: Column(
            children: [
              AppTopBar(title: widget.title),
              Expanded(
                child: Align(
                  alignment: Alignment.topCenter,
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: AppColors.maxContentWidth),
                    child: widget.child,
                  ),
                ),
              ),
            ],
          ),
        ),
        bottomNavigationBar: AppBottomNav(currentIndex: widget.navIndex),
      ),
    );
  }
}

class AppTopBar extends StatelessWidget {
  const AppTopBar({super.key, required this.title});

  final String title;

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
                const SizedBox(
                  width: 44,
                  height: 40,
                  child: Center(
                    child: JuiceLogo(variant: JuiceLogoVariant.mark, size: JuiceLogoSize.sm),
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
                        child: const Text('Đăng nhập', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700)),
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
