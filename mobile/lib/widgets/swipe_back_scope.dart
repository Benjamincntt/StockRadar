import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Vuốt từ cạnh trái sang phải để pop (giống iOS / FireAnt).
class SwipeBackScope extends StatefulWidget {
  const SwipeBackScope({super.key, required this.child});

  final Widget child;

  @override
  State<SwipeBackScope> createState() => _SwipeBackScopeState();
}

class _SwipeBackScopeState extends State<SwipeBackScope> {
  static const _edgeWidth = 28.0;
  double _dragDx = 0;

  void _onDragUpdate(DragUpdateDetails details) {
    if (details.delta.dx > 0) {
      setState(() => _dragDx += details.delta.dx);
    }
  }

  void _onDragEnd(DragEndDetails details) {
    final velocity = details.primaryVelocity ?? 0;
    if (_dragDx > 56 || velocity > 320) {
      if (context.canPop()) context.pop();
    }
    setState(() => _dragDx = 0);
  }

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        widget.child,
        Positioned(
          left: 0,
          top: 0,
          bottom: 0,
          width: _edgeWidth,
          child: GestureDetector(
            behavior: HitTestBehavior.translucent,
            onHorizontalDragUpdate: _onDragUpdate,
            onHorizontalDragEnd: _onDragEnd,
          ),
        ),
      ],
    );
  }
}
