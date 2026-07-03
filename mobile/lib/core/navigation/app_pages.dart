import 'package:flutter/cupertino.dart';
import 'package:flutter/foundation.dart' show defaultTargetPlatform, TargetPlatform;
import 'package:go_router/go_router.dart';

import '../../widgets/swipe_back_scope.dart';

/// Trang có thể pop — bật vuốt-back iOS/Android (CupertinoPage).
Page<T> appPushedPage<T>({
  required LocalKey key,
  required Widget child,
}) {
  final wrapped = defaultTargetPlatform == TargetPlatform.iOS
      ? child
      : SwipeBackScope(child: child);

  return CupertinoPage<T>(
    key: key,
    child: wrapped,
  );
}

/// Tab trong shell — không animation stack.
Page<T> appTabPage<T>({
  required LocalKey key,
  required Widget child,
}) {
  return NoTransitionPage<T>(
    key: key,
    child: child,
  );
}
