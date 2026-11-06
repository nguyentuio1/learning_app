import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/session/auth_controller.dart';
import 'auth_providers.dart';

class RegisterController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> register({
    required String email,
    required String password,
    required String fullName,
    required String phoneNumber,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final session = await ref
          .read(registerUseCaseProvider)
          .call(
            email: email,
            password: password,
            fullName: fullName,
            phoneNumber: phoneNumber,
          );
      await ref
          .read(authControllerProvider.notifier)
          .signedIn(session.tokens, session.user);
    });
  }
}

final registerControllerProvider =
    AsyncNotifierProvider.autoDispose<RegisterController, void>(
      RegisterController.new,
      name: 'registerController',
    );
