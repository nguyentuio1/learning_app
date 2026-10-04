import 'package:flutter_riverpod/misc.dart' show Override;
import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/config/app_config.dart';
import '../core/di/app_provider_observer.dart';
import 'app.dart';

/// [overrides] cho phép mỗi flavor thay thế implementation (DI theo flavor), ví dụ:
///   bootstrap(AppConfig.customer(), overrides: [someProvider.overrideWithValue(...)]);
Future<void> bootstrap(
  AppConfig config, {
  List<Override> overrides = const [],
}) async {
  await runZonedGuarded(() async {
    WidgetsFlutterBinding.ensureInitialized();

    FlutterError.onError = (details) {
      FlutterError.presentError(details);
      // TODO: forward to Crashlytics / Sentry
    };

    runApp(
      ProviderScope(
        overrides: [appConfigProvider.overrideWithValue(config), ...overrides],
        observers: [if (kDebugMode) AppProviderObserver()],
        // Riverpod 3 mặc định tự retry provider lỗi; với lỗi HTTP ta muốn
        // người dùng bấm "Retry" chủ động, nên tắt retry tự động.
        retry: (_, _) => null,
        child: const App(),
      ),
    );
  }, (error, stack) {
    debugPrint('Uncaught: $error\n$stack');
    // TODO: forward to Crashlytics / Sentry
  });
}
