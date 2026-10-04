import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/auth_tokens.dart';
import '../storage/storage_providers.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

/// Trạng thái phiên toàn app. Router lắng nghe provider này để bảo vệ route.
class AuthController extends Notifier<AuthStatus> {
  @override
  AuthStatus build() {
    _restore();
    return AuthStatus.unknown;
  }

  Future<void> _restore() async {
    final tokens = await ref.read(tokenStorageProvider).read();
    state = tokens == null ? AuthStatus.unauthenticated : AuthStatus.authenticated;
  }

  Future<void> signedIn(AuthTokens tokens) async {
    await ref.read(tokenStorageProvider).write(tokens);
    state = AuthStatus.authenticated;
  }

  Future<void> signOut() async {
    await ref.read(tokenStorageProvider).clear();
    state = AuthStatus.unauthenticated;
  }

  /// Được Dio interceptor gọi khi refresh token bị từ chối.
  void onSessionExpired() => state = AuthStatus.unauthenticated;
}

final authControllerProvider =
    NotifierProvider<AuthController, AuthStatus>(AuthController.new, name: 'authController');
