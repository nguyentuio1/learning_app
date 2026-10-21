import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/core/network/auth_interceptor.dart';
import 'package:smart_drone_delivery/core/network/error_interceptor.dart';
import 'package:smart_drone_delivery/core/storage/auth_tokens.dart';
import 'package:smart_drone_delivery/data/repositories/auth_repository_impl.dart';

import '../../support/fake_http_adapter.dart';
import '../../support/test_helpers.dart';

void main() {
  late InMemoryTokenStorage storage;
  late InMemoryUserStorage userStorage;

  setUp(() {
    storage = InMemoryTokenStorage();
    userStorage = InMemoryUserStorage();
  });

  Dio createDio(ResponseBody Function(RequestOptions) handler) {
    final dio = Dio(BaseOptions(baseUrl: 'http://localhost:5055/api'));
    dio.httpClientAdapter = FakeHttpAdapter(handler);
    dio.interceptors.add(ErrorInterceptor());
    return dio;
  }

  group('AuthRepositoryImpl.login', () {
    test('sends email and password to ApiEndpoints.login with skipAuth', () async {
      RequestOptions? recordedOptions;
      Map<String, dynamic>? recordedBody;

      final dio = createDio((options) {
        recordedOptions = options;
        recordedBody = options.data as Map<String, dynamic>;
        return jsonBody({
          'accessToken': 'jwt-access-token',
          'refreshToken': 'random-refresh-token',
          'expiresIn': 900,
          'user': {
            'id': '6f1c8c1e-3a3e-4a52-9b47-0c3f4a8d9e11',
            'email': 'customer@example.com',
            'fullName': 'Nguyen Van An',
            'phoneNumber': '0901234567',
            'status': 'ACTIVE',
            'role': {'id': 1, 'code': 'CUSTOMER', 'name': 'Customer'},
            'createdAt': '2026-10-15T08:30:00Z',
          },
        });
      });

      final repo = AuthRepositoryImpl(dio, storage, userStorage);
      final tokens = await repo.login(
        email: 'customer@example.com',
        password: 'Passw0rd123',
      );

      expect(recordedOptions?.path, ApiEndpoints.login);
      expect(recordedOptions?.extra[AuthInterceptor.skipAuth], isTrue);
      expect(recordedBody?['email'], 'customer@example.com');
      expect(recordedBody?['password'], 'Passw0rd123');
      expect(tokens.accessToken, 'jwt-access-token');
      expect(tokens.refreshToken, 'random-refresh-token');
      expect(tokens.expiresIn, 900);

      final savedUser = await userStorage.read();
      expect(savedUser, isNotNull);
      expect(savedUser?.email, 'customer@example.com');
      expect(savedUser?.fullName, 'Nguyen Van An');
      expect(savedUser?.roleCode, 'CUSTOMER');
    });

    test('maps 401 unauthorized to ApiException with detail message', () async {
      final dio = createDio((_) => jsonBody({
            'type': 'https://httpstatuses.com/401',
            'title': 'Unauthorized',
            'status': 401,
            'detail': 'Invalid email or password.',
          }, 401));

      final repo = AuthRepositoryImpl(dio, storage, userStorage);

      expect(
        () => repo.login(email: 'wrong@example.com', password: 'bad'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 401)
            .having((e) => e.message, 'message', 'Invalid email or password.')),
      );
    });

    test('maps 403 forbidden to ApiException for inactive account', () async {
      final dio = createDio((_) => jsonBody({
            'type': 'https://httpstatuses.com/403',
            'title': 'Forbidden',
            'status': 403,
            'detail': 'This account is not active.',
          }, 403));

      final repo = AuthRepositoryImpl(dio, storage, userStorage);

      expect(
        () => repo.login(email: 'inactive@example.com', password: 'pw'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 403)
            .having((e) => e.message, 'message', 'This account is not active.')),
      );
    });
  });

  group('AuthRepositoryImpl.getMe', () {
    test('sends GET /users/me and saves/returns user', () async {
      final dio = createDio((options) {
        expect(options.path, ApiEndpoints.me);
        expect(options.method, 'GET');
        return jsonBody({
          'id': 'user-123',
          'email': 'customer@example.com',
          'fullName': 'Nguyen Van An',
          'phoneNumber': '0901234567',
          'avatarUrl': null,
          'status': 'ACTIVE',
          'role': {'code': 'CUSTOMER'},
          'createdAt': '2026-01-01T00:00:00Z',
        });
      });

      final repo = AuthRepositoryImpl(dio, storage, userStorage);
      final user = await repo.getMe();

      expect(user.id, 'user-123');
      expect(user.email, 'customer@example.com');
      expect(user.fullName, 'Nguyen Van An');
      expect(user.phoneNumber, '0901234567');
      expect(user.roleCode, 'CUSTOMER');

      final saved = await userStorage.read();
      expect(saved?.id, 'user-123');
      expect(saved?.fullName, 'Nguyen Van An');
    });

    test('getMe throws ApiException on 401', () async {
      final dio = createDio((_) => jsonBody({'detail': 'Unauthorized'}, 401));
      final repo = AuthRepositoryImpl(dio, storage, userStorage);

      expect(() => repo.getMe(), throwsA(isA<ApiException>()));
    });
  });

  group('AuthRepositoryImpl.logout', () {
    test('sends refreshToken to ApiEndpoints.logout and clears userStorage', () async {
      await storage.write(
        const AuthTokens(accessToken: 'live-access', refreshToken: 'live-refresh'),
      );
      RequestOptions? recordedOptions;
      Map<String, dynamic>? recordedBody;

      final dio = createDio((options) {
        recordedOptions = options;
        recordedBody = options.data as Map<String, dynamic>?;
        return jsonBody({}, 204);
      });

      final repo = AuthRepositoryImpl(dio, storage, userStorage);
      await repo.logout();

      expect(recordedOptions?.path, ApiEndpoints.logout);
      expect(recordedBody?['refreshToken'], 'live-refresh');
      expect(await userStorage.read(), isNull);
    });

    test('best-effort: suppresses server error on logout', () async {
      final dio = createDio((_) => jsonBody({'detail': 'Server error'}, 500));
      final repo = AuthRepositoryImpl(dio, storage, userStorage);

      await expectLater(repo.logout(), completes);
      expect(await userStorage.read(), isNull);
    });
  });
}
