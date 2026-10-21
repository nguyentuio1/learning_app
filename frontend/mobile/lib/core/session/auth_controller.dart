import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/providers/repository_providers.dart';
import '../../domain/entities/user.dart';
import '../error/api_exception.dart';
import '../storage/auth_tokens.dart';
import '../storage/storage_providers.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

/// Quản lý thông tin người dùng hiện tại trong bộ nhớ.
class CurrentUserController extends Notifier<User?> {
  @override
  User? build() => null;

  void setUser(User? user) => state = user;
}

final currentUserProvider =
    NotifierProvider<CurrentUserController, User?>(
  CurrentUserController.new,
  name: 'currentUser',
);

/// Trạng thái phiên toàn app. Router lắng nghe provider này để bảo vệ route.
class AuthController extends Notifier<AuthStatus> {
  @override
  AuthStatus build() {
    _restore();
    return AuthStatus.unknown;
  }

  Future<void> _restore() async {
    final tokens = await ref.read(tokenStorageProvider).read();
    if (tokens == null) {
      state = AuthStatus.unauthenticated;
      return;
    }

    try {
      // Khi mở lại app: gọi GET /users/me để xác thực token và cập nhật hồ sơ mới nhất
      final user = await ref.read(authRepositoryProvider).getMe();
      ref.read(currentUserProvider.notifier).setUser(user);
      state = AuthStatus.authenticated;
    } on ApiException catch (e) {
      if (e.statusCode == 401 || e.statusCode == 403) {
        await signOut();
      } else {
        final cached = await ref.read(userStorageProvider).read();
        if (cached != null) {
          ref.read(currentUserProvider.notifier).setUser(cached);
        }
        state = AuthStatus.authenticated;
      }
    } catch (_) {
      // Mất mạng / offline: giữ phiên làm việc và dùng hồ sơ đã lưu
      final cached = await ref.read(userStorageProvider).read();
      if (cached != null) {
        ref.read(currentUserProvider.notifier).setUser(cached);
      }
      state = AuthStatus.authenticated;
    }
  }

  Future<void> signedIn(AuthTokens tokens, [User? user]) async {
    await ref.read(tokenStorageProvider).write(tokens);
    if (user != null) {
      await ref.read(userStorageProvider).write(user);
      ref.read(currentUserProvider.notifier).setUser(user);
    } else {
      final cached = await ref.read(userStorageProvider).read();
      if (cached != null) {
        ref.read(currentUserProvider.notifier).setUser(cached);
      }
    }
    state = AuthStatus.authenticated;
  }

  Future<void> signOut() async {
    await ref.read(tokenStorageProvider).clear();
    await ref.read(userStorageProvider).clear();
    ref.read(currentUserProvider.notifier).setUser(null);
    state = AuthStatus.unauthenticated;
  }

  /// Được Dio interceptor gọi khi refresh token bị từ chối.
  void onSessionExpired() {
    ref.read(userStorageProvider).clear();
    ref.read(currentUserProvider.notifier).setUser(null);
    state = AuthStatus.unauthenticated;
  }
}

final authControllerProvider =
    NotifierProvider<AuthController, AuthStatus>(AuthController.new, name: 'authController');
