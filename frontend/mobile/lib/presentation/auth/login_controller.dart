import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/session/auth_controller.dart';
import 'auth_providers.dart';

/// State màn hình đăng nhập: AsyncLoading / AsyncError / AsyncData.
class LoginController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> login(String email, String password) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final tokens = await ref
          .read(loginUseCaseProvider)
          .call(email: email, password: password);
      await ref.read(authControllerProvider.notifier).signedIn(tokens);
    });
  }
}

final loginControllerProvider =
    AsyncNotifierProvider.autoDispose<LoginController, void>(
  LoginController.new,
  name: 'loginController',
);
