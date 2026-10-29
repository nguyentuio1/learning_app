import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/core/network/auth_interceptor.dart';
import 'package:smart_drone_delivery/core/network/error_interceptor.dart';
import 'package:smart_drone_delivery/core/storage/auth_tokens.dart';

import '../../support/fake_http_adapter.dart';
import '../../support/test_helpers.dart';

void main() {
  late InMemoryTokenStorage storage;
  late bool sessionExpiredCalled;

  setUp(() {
    storage = InMemoryTokenStorage();
    sessionExpiredCalled = false;
  });

  (Dio, Dio) createClients({
    required ResponseBody Function(RequestOptions) onMain,
    required ResponseBody Function(RequestOptions) onRefresh,
  }) {
    final dio = Dio(BaseOptions(baseUrl: 'http://localhost:5055/api'));
    final refreshDio = Dio(BaseOptions(baseUrl: 'http://localhost:5055/api'));

    dio.httpClientAdapter = FakeHttpAdapter(onMain);
    refreshDio.httpClientAdapter = FakeHttpAdapter(onRefresh);

    dio.interceptors.addAll([
      AuthInterceptor(
        dio: dio,
        refreshDio: refreshDio,
        storage: storage,
        onSessionExpired: () => sessionExpiredCalled = true,
      ),
      ErrorInterceptor(),
    ]);

    return (dio, refreshDio);
  }

  test('attaches Authorization header when token exists', () async {
    await storage.write(
      const AuthTokens(accessToken: 'token-123', refreshToken: 'ref-123'),
    );
    RequestOptions? options;

    final (dio, _) = createClients(
      onMain: (opt) {
        options = opt;
        return jsonBody({'ok': true});
      },
      onRefresh: (_) => jsonBody({}),
    );

    await dio.get<dynamic>('/orders');

    expect(options?.headers['Authorization'], 'Bearer token-123');
  });

  test('skips Authorization header when skipAuth is true', () async {
    await storage.write(
      const AuthTokens(accessToken: 'token-123', refreshToken: 'ref-123'),
    );
    RequestOptions? options;

    final (dio, _) = createClients(
      onMain: (opt) {
        options = opt;
        return jsonBody({'ok': true});
      },
      onRefresh: (_) => jsonBody({}),
    );

    await dio.get<dynamic>(
      '/public',
      options: Options(extra: {AuthInterceptor.skipAuth: true}),
    );

    expect(options?.headers.containsKey('Authorization'), isFalse);
  });

  test('401 triggers refresh with camelCase refreshToken and retries request', () async {
    await storage.write(
      const AuthTokens(accessToken: 'old-access', refreshToken: 'old-refresh'),
    );

    var mainAttempts = 0;
    RequestOptions? refreshOptions;
    Map<String, dynamic>? refreshBody;

    final (dio, _) = createClients(
      onMain: (opt) {
        mainAttempts++;
        if (mainAttempts == 1) {
          return jsonBody({'detail': 'Token expired'}, 401);
        }
        return jsonBody({'message': 'Success with new token'});
      },
      onRefresh: (opt) {
        refreshOptions = opt;
        refreshBody = opt.data as Map<String, dynamic>?;
        return jsonBody({
          'accessToken': 'new-access',
          'refreshToken': 'new-refresh',
          'expiresIn': 900,
        });
      },
    );

    final res = await dio.get<Map<String, dynamic>>('/orders');

    expect(mainAttempts, 2);
    expect(res.data?['message'], 'Success with new token');
    expect(refreshOptions?.path, ApiEndpoints.refresh);
    expect(refreshBody?['refreshToken'], 'old-refresh');

    final updated = await storage.read();
    expect(updated?.accessToken, 'new-access');
    expect(updated?.refreshToken, 'new-refresh');
    expect(sessionExpiredCalled, isFalse);
  });

  test('invalid refresh token clears storage and invokes onSessionExpired', () async {
    await storage.write(
      const AuthTokens(accessToken: 'bad-access', refreshToken: 'invalid-refresh'),
    );

    final (dio, _) = createClients(
      onMain: (_) => jsonBody({'detail': 'Token expired'}, 401),
      onRefresh: (_) => jsonBody({'detail': 'Invalid refresh token'}, 401),
    );

    try {
      await dio.get<dynamic>('/orders');
      fail('Should throw DioException');
    } on DioException catch (e) {
      expect(e.response?.statusCode, 401);
    }

    expect(await storage.read(), isNull);
    expect(sessionExpiredCalled, isTrue);
  });

  test('network error during refresh preserves stored tokens and session', () async {
    await storage.write(
      const AuthTokens(accessToken: 'access-1', refreshToken: 'refresh-1'),
    );

    final (dio, _) = createClients(
      onMain: (_) => jsonBody({'detail': 'Token expired'}, 401),
      onRefresh: (opt) => throw DioException.connectionTimeout(
        timeout: const Duration(seconds: 5),
        requestOptions: opt,
      ),
    );

    try {
      await dio.get<dynamic>('/orders');
      fail('Should throw connectionTimeout');
    } on DioException catch (e) {
      expect(e.type, DioExceptionType.connectionTimeout);
    }

    expect(await storage.read(), isNotNull);
    expect(sessionExpiredCalled, isFalse);
  });

  test('401 on request with skipRefresh does not trigger refresh or session expired', () async {
    await storage.write(
      const AuthTokens(accessToken: 'access-1', refreshToken: 'refresh-1'),
    );

    var refreshCalled = false;
    final (dio, _) = createClients(
      onMain: (_) => jsonBody({'detail': 'Current password does not match'}, 401),
      onRefresh: (_) {
        refreshCalled = true;
        return jsonBody({'accessToken': 'new', 'refreshToken': 'new'});
      },
    );

    try {
      await dio.put<dynamic>(
        '/users/me/password',
        options: Options(extra: {AuthInterceptor.skipRefresh: true}),
      );
      fail('Should throw DioException');
    } on DioException catch (e) {
      expect(e.response?.statusCode, 401);
    }

    expect(refreshCalled, isFalse);
    expect(sessionExpiredCalled, isFalse);
    final tokens = await storage.read();
    expect(tokens?.accessToken, 'access-1');
  });
}
