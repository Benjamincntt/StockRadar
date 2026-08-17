import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';

/// Dialog thoát — gradient cyan→tím tĩnh, 2 nút ngang. Không animation.
Future<bool> showExitConfirmDialog(BuildContext context) async {
  final result = await showDialog<bool>(
    context: context,
    barrierColor: Colors.black.withValues(alpha: 0.6),
    builder: (ctx) => const _ExitConfirmDialog(),
  );
  return result == true;
}

class _ExitConfirmDialog extends StatelessWidget {
  const _ExitConfirmDialog();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final cyan = isDark ? AppColors.darkPrimary : AppColors.lightPrimaryContainer;
    final purple = isDark ? AppColors.darkSecondary : AppColors.lightPrimary;
    final surface = isDark ? AppColors.darkSurface : AppColors.lightSurfaceLowest;
    final onFill = isDark ? const Color(0xFF002022) : Colors.white;

    return Dialog(
      backgroundColor: Colors.transparent,
      insetPadding: const EdgeInsets.symmetric(horizontal: 28),
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(22),
          gradient: LinearGradient(
            begin: Alignment.centerLeft,
            end: Alignment.centerRight,
            colors: [cyan, purple],
          ),
          boxShadow: [
            BoxShadow(
              color: purple.withValues(alpha: 0.22),
              blurRadius: 24,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        padding: const EdgeInsets.all(1.5),
        child: Container(
          padding: const EdgeInsets.fromLTRB(20, 22, 20, 18),
          decoration: BoxDecoration(
            color: surface,
            borderRadius: BorderRadius.circular(20.5),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Thoát ứng dụng?',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                  color: scheme.onSurface,
                ),
              ),
              const SizedBox(height: 22),
              _StaticGradientDivider(cyan: cyan, purple: purple),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: _ExitActionButton(
                      label: 'Ở lại',
                      cyan: cyan,
                      purple: purple,
                      onFill: onFill,
                      outlined: true,
                      onPressed: () => Navigator.of(context).pop(false),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _ExitActionButton(
                      label: 'Thoát',
                      cyan: cyan,
                      purple: purple,
                      onFill: onFill,
                      outlined: false,
                      onPressed: () => Navigator.of(context).pop(true),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StaticGradientDivider extends StatelessWidget {
  const _StaticGradientDivider({required this.cyan, required this.purple});

  final Color cyan;
  final Color purple;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 10,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Container(
            height: 2,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(2),
              gradient: LinearGradient(
                colors: [
                  Colors.transparent,
                  cyan,
                  Colors.white,
                  purple,
                  Colors.transparent,
                ],
                stops: const [0.0, 0.28, 0.5, 0.72, 1.0],
              ),
            ),
          ),
          Container(
            width: 18,
            height: 6,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.white.withValues(alpha: 0.85),
              boxShadow: [
                BoxShadow(
                  color: Colors.white.withValues(alpha: 0.55),
                  blurRadius: 8,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ExitActionButton extends StatelessWidget {
  const _ExitActionButton({
    required this.label,
    required this.cyan,
    required this.purple,
    required this.onFill,
    required this.outlined,
    required this.onPressed,
  });

  final String label;
  final Color cyan;
  final Color purple;
  final Color onFill;
  final bool outlined;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onPressed,
        borderRadius: BorderRadius.circular(24),
        child: Ink(
          height: 48,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(24),
            gradient: outlined
                ? null
                : LinearGradient(
                    begin: Alignment.centerLeft,
                    end: Alignment.centerRight,
                    colors: [cyan, purple],
                  ),
            color: outlined ? Colors.transparent : null,
            border: Border.all(
              color: outlined ? cyan : Colors.transparent,
              width: 1.4,
            ),
          ),
          child: Center(
            child: Text(
              label,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 15,
                color: outlined ? cyan : onFill,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
