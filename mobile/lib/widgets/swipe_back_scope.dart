import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Vuốt từ cạnh trái sang phải để pop (bổ sung cho CupertinoPage trên Android).
class SwipeBackScope extends StatefulWidget {
  const SwipeBackScope({super.key, required this.child});

  final Widget child;

  @override
  State<SwipeBackScope> createState() => _SwipeBackScopeState();
}

class _SwipeBackScopeState extends State<SwipeBackScope> {
  static const _edgeStartMax = 48.0;
  static const _popDragThreshold = 72.0;
  static const _popVelocityThreshold = 400.0;

  var _startedFromEdge = false;
  var _dragDx = 0.0;

  void _onDragStart(DragStartDetails details) {
    _startedFromEdge = details.globalPosition.dx <= _edgeStartMax;
    _dragDx = 0;
  }

  void _onDragUpdate(DragUpdateDetails details) {
    if (!_startedFromEdge) return;
    if (details.delta.dx > 0) {
      setState(() => _dragDx += details.delta.dx);
    }
  }

  void _onDragEnd(DragEndDetails details) {
    if (!_startedFromEdge) return;
    final velocity = details.primaryVelocity ?? 0;
    if (_dragDx > _popDragThreshold || velocity > _popVelocityThreshold) {
      if (context.canPop()) {
        context.pop();
      }
    }
    setState(() {
      _dragDx = 0;
      _startedFromEdge = false;
    });
  }

  void _onDragCancel() {
    setState(() {
      _dragDx = 0;
      _startedFromEdge = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final offset = _dragDx.clamp(0.0, 120.0);

    return PopScope(
      canPop: context.canPop(),
      child: Stack(
        children: [
          Transform.translate(
            offset: Offset(offset, 0),
            child: widget.child,
          ),
          Positioned(
            left: 0,
            top: 0,
            bottom: 0,
            width: _edgeStartMax,
            child: GestureDetector(
              behavior: HitTestBehavior.translucent,
              onHorizontalDragStart: _onDragStart,
              onHorizontalDragUpdate: _onDragUpdate,
              onHorizontalDragEnd: _onDragEnd,
              onHorizontalDragCancel: _onDragCancel,
            ),
          ),
        ],
      ),
    );
  }
}
