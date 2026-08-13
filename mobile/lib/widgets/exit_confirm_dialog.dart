import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';

/// Dialog thoát — gradient cyan↔tím, 2 nút ngang, hiệu ứng màu chạy trái→phải.
Future<bool> showExitConfirmDialog(BuildContext context) async {
  final result = await showDialog<bool>(
    context: context,
    barrierColor: Colors.black.withValues(alpha: 0.6),
    builder: (ctx) => const _ExitConfirmDialog(),
  );
  return result == true;
}

class _ExitConfirmDialog extends StatefulWidget {
  const _ExitConfirmDialog();

  @override
  State<_ExitConfirmDialog> createState() => _ExitConfirmDialogState();
}

class _ExitConfirmDialogState extends State<_ExitConfirmDialog>
    with SingleTickerProviderStateMixin {
  late final AnimationController _flow = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 2200),
  )..repeat();

  @override
  void dispose() {
    _flow.dispose();
    super.dispose();
  }

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
      child: AnimatedBuilder(
        animation: _flow,
        builder: (context, _) {
          final t = _flow.value;
          // Gradient border + glow chạy từ trái (Ở lại) sang phải (Thoát).
          final align = Alignment(-1.4 + t * 2.8, 0);

          return Container(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(22),
              gradient: LinearGradient(
                begin: Alignment(-1.0 + t * 2, -1),
                end: Alignment(1.0 - t * 2, 1),
                colors: [
                  cyan.withValues(alpha: 0.85),
                  purple.withValues(alpha: 0.9),
                  cyan.withValues(alpha: 0.75),
                ],
                stops: const [0.0, 0.5, 1.0],
              ),
              boxShadow: [
                BoxShadow(
                  color: Color.lerp(cyan, purple, t)!.withValues(alpha: 0.35),
                  blurRadius: 28,
                  spreadRadius: 0,
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
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: isDark
                      ? [
                          AppColors.darkSurface,
                          AppColors.darkSurfaceLow,
                          Color.lerp(AppColors.darkSurface, purple, 0.08)!,
                        ]
                      : [
                          AppColors.lightSurfaceLowest,
                          AppColors.lightSurfaceLow,
                          AppColors.lightSurfaceLowest,
                        ],
                ),
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
                  // Dải sáng chạy từ nút trái → nút phải
                  SizedBox(
                    height: 2,
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(2),
                        gradient: LinearGradient(
                          begin: align,
                          end: Alignment(align.x + 1.2, 0),
                          colors: [
                            Colors.transparent,
                            cyan,
                            purple,
                            Colors.transparent,
                          ],
                          stops: const [0.0, 0.35, 0.65, 1.0],
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: _ExitActionButton(
                          label: 'Ở lại',
                          accent: cyan,
                          secondary: purple,
                          flow: t,
                          highlight: t < 0.55,
                          onFill: onFill,
                          outlined: true,
                          onPressed: () => Navigator.of(context).pop(false),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: _ExitActionButton(
                          label: 'Thoát',
                          accent: purple,
                          secondary: cyan,
                          flow: t,
                          highlight: t >= 0.45,
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
          );
        },
      ),
    );
  }
}

class _ExitActionButton extends StatelessWidget {
  const _ExitActionButton({
    required this.label,
    required this.accent,
    required this.secondary,
    required this.flow,
    required this.highlight,
    required this.onFill,
    required this.outlined,
    required this.onPressed,
  });

  final String label;
  final Color accent;
  final Color secondary;
  final double flow;
  final bool highlight;
  final Color onFill;
  final bool outlined;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final intensity = highlight ? (0.55 + 0.45 * (1 - (flow - 0.5).abs() * 2).clamp(0.0, 1.0)) : 0.35;
    final a = Color.lerp(accent, secondary, flow)!;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onPressed,
        borderRadius: BorderRadius.circular(24),
        child: Ink(
          height: 48,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(24),
            gradient: LinearGradient(
              begin: Alignment(-1.2 + flow * 2.4, 0),
              end: Alignment(1.2 - flow * 2.4, 0),
              colors: outlined
                  ? [
                      a.withValues(alpha: 0.12 + intensity * 0.2),
                      secondary.withValues(alpha: 0.08 + intensity * 0.15),
                    ]
                  : [
                      Color.lerp(accent, secondary, flow * 0.6)!,
                      Color.lerp(secondary, accent, flow * 0.6)!,
                    ],
            ),
            border: Border.all(
              color: a.withValues(alpha: outlined ? 0.55 + intensity * 0.35 : 0.9),
              width: 1.4,
            ),
            boxShadow: highlight
                ? [
                    BoxShadow(
                      color: a.withValues(alpha: outlined ? 0.25 : 0.4),
                      blurRadius: 14,
                      spreadRadius: 0,
                    ),
                  ]
                : null,
          ),
          child: Center(
            child: Text(
              label,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 15,
                color: outlined ? a : onFill,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
