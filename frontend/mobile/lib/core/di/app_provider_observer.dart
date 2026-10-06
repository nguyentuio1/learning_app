import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/token_storage.dart';

/// Ghi log mọi thay đổi state của provider (chỉ bật ở debug, xem bootstrap.dart).
/// Giúp debug luồng: UI -> controller -> repository -> Dio.
final class AppProviderObserver extends ProviderObserver {
  AppProviderObserver();

  String _name(ProviderObserverContext c) =>
      c.provider.name ?? c.provider.runtimeType.toString();

  String _short(Object? v) {
    if (v is AuthTokens) return '<redacted>'; // không bao giờ log token
    final s = '$v';
    return s.length > 160 ? '${s.substring(0, 160)}…' : s;
  }

  @override
  void didAddProvider(ProviderObserverContext context, Object? value) =>
      debugPrint('[provider+] ${_name(context)} = ${_short(value)}');

  @override
  void didUpdateProvider(
    ProviderObserverContext context,
    Object? previousValue,
    Object? newValue,
  ) =>
      debugPrint(
          '[provider~] ${_name(context)}: ${_short(previousValue)} -> ${_short(newValue)}');

  @override
  void providerDidFail(
    ProviderObserverContext context,
    Object error,
    StackTrace stackTrace,
  ) =>
      debugPrint('[provider!] ${_name(context)} failed: $error');
}
