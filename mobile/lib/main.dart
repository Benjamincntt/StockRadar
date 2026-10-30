import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';

import 'app.dart';
import 'core/services/push_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  try {
    await Firebase.initializeApp();
    PushService.firebaseSanSang = true;
  } catch (_) {
    // BUSINESS-RULE: Thiếu google-services.json hoặc sai cấu hình thì app vẫn chạy, chỉ tắt phần đẩy (FR-013).
  }
  if (PushService.firebaseSanSang) {
    FirebaseMessaging.onBackgroundMessage(tinDayNen);
  }
  runApp(const JuiceApp());
}
