import 'package:smart_drone_delivery/app/providers.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/core/storage/token_storage.dart';
import 'package:smart_drone_delivery/domain/repositories/auth_repository.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../support/test_helpers.dart';

class _FakeAuthRepository implements AuthRepository {
  _FakeAuthRepository({this.error});
  final ApiException? error;

  @override
  Future<AuthTokens> login(
      {required String email, required String password}) async {
    if (error != null) throw error!;
    return const AuthTokens(accessToken: 'a', refreshToken: 'r');
  }

  @override
  Future<void> logout() async {}
}

ProviderContainer _container(
        InMemoryTokenStorage storage, AuthRepository repo) =>
    createContainer(overrides: [
      appConfigProvider.overrideWithValue(AppConfig.customer()),
      tokenStorageProvider.overrideWithValue(storage),
      authRepositoryProvider.overrideWithValue(repo),
    ]);

void main() {
  test('Dio được dựng từ AppConfig của flavor', () {
    final c = _container(InMemoryTokenStorage(), _FakeAuthRepository());
    final dio = c.read(dioProvider);
    expect(dio.options.baseUrl, AppConfig.customer().baseUrl);
    expect(dio.options.headers['X-App'], 'customer');
  });

  test('login thành công: lưu token và chuyển sang authenticated', () async {
    final storage = InMemoryTokenStorage();
    final c = _container(storage, _FakeAuthRepository());

    c.read(authControllerProvider); // khởi tạo + restore phiên
    await pumpEventQueue();
    c.listen(loginControllerProvider, (_, _) {}); // giữ autoDispose provider sống

    await c.read(loginControllerProvider.notifier).login('a@b.c', 'pw');

    expect(c.read(loginControllerProvider).hasError, isFalse);
    expect((await storage.read())?.accessToken, 'a');
    expect(c.read(authControllerProvider), AuthStatus.authenticated);
  });

  test('login thất bại: state là AsyncError(ApiException), không lưu token',
      () async {
    final storage = InMemoryTokenStorage();
    final c = _container(
      storage,
      _FakeAuthRepository(
          error: const ApiException(message: 'Sai mật khẩu', statusCode: 401)),
    );

    c.read(authControllerProvider);
    await pumpEventQueue();
    c.listen(loginControllerProvider, (_, _) {});

    await c.read(loginControllerProvider.notifier).login('a@b.c', 'bad');

    final state = c.read(loginControllerProvider);
    expect(state.hasError, isTrue);
    expect(state.error, isA<ApiException>());
    expect(await storage.read(), isNull);
    expect(c.read(authControllerProvider), isNot(AuthStatus.authenticated));
  });

  test('refresh bị từ chối -> onSessionExpired đưa app về unauthenticated',
      () async {
    final storage = InMemoryTokenStorage();
    await storage.write(const AuthTokens(accessToken: 'a', refreshToken: 'r'));
    final c = _container(storage, _FakeAuthRepository());

    c.read(authControllerProvider);
    await pumpEventQueue();
    expect(c.read(authControllerProvider), AuthStatus.authenticated);

    c.read(authControllerProvider.notifier).onSessionExpired();
    expect(c.read(authControllerProvider), AuthStatus.unauthenticated);
  });

  test('dioProvider có thể bị thay thế hoàn toàn trong test', () {
    final fake = Dio(BaseOptions(baseUrl: 'http://fake'));
    final c = createContainer(overrides: [dioProvider.overrideWithValue(fake)]);
    expect(c.read(dioProvider).options.baseUrl, 'http://fake');
  });
}
