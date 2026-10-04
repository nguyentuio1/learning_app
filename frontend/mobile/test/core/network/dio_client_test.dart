import 'package:smart_drone_delivery/core/config/app_config.dart';
import 'package:smart_drone_delivery/core/network/auth_interceptor.dart';
import 'package:smart_drone_delivery/core/network/dio_client.dart';
import 'package:smart_drone_delivery/core/network/error_interceptor.dart';
import 'package:smart_drone_delivery/core/network/logging_interceptor.dart';
import 'package:smart_drone_delivery/core/network/network_config.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../support/fake_http_adapter.dart';
import '../../support/test_helpers.dart';

Dio _create(NetworkConfig net) => DioClient.create(
      config: AppConfig.operator(),
      network: net,
      storage: InMemoryTokenStorage(),
      onSessionExpired: () {},
    );

void main() {
  const net = NetworkConfig(
    baseUrl: 'https://api.test/v1',
    connectTimeout: Duration(seconds: 5),
    sendTimeout: Duration(seconds: 6),
    receiveTimeout: Duration(seconds: 7),
  );

  test('BaseOptions: baseUrl, 3 loại timeout và header mặc định', () {
    final o = _create(net).options;
    expect(o.baseUrl, 'https://api.test/v1');
    expect(o.connectTimeout, const Duration(seconds: 5));
    expect(o.sendTimeout, const Duration(seconds: 6));
    expect(o.receiveTimeout, const Duration(seconds: 7));
    expect(o.headers['X-App'], 'operator');
    expect(o.headers['Accept'], Headers.jsonContentType);
  });

  test('enableLogging=false -> không có AppLogInterceptor', () {
    final dio = _create(net);
    expect(dio.interceptors.whereType<AppLogInterceptor>(), isEmpty);
  });

  test('enableLogging=true -> log đứng TRƯỚC AuthInterceptor', () {
    final dio = _create(const NetworkConfig(baseUrl: 'https://api.test/v1', enableLogging: true));
    final list = dio.interceptors.toList();
    final log = list.indexWhere((e) => e is AppLogInterceptor);
    final auth = list.indexWhere((e) => e is AuthInterceptor);
    expect(log, greaterThanOrEqualTo(0));
    expect(log, lessThan(auth));
  });

  test('baseUrl sai -> ném ArgumentError ngay khi tạo client', () {
    expect(() => _create(const NetworkConfig(baseUrl: '')), throwsArgumentError);
    expect(() => _create(const NetworkConfig(baseUrl: 'api.test/v1')), throwsArgumentError);
  });

  test('timeout được đổi thành ApiException.isNetwork', () async {
    final dio = _create(net);
    dio.httpClientAdapter = FakeHttpAdapter((o) => throw DioException.connectionTimeout(
          timeout: const Duration(seconds: 5),
          requestOptions: o,
        ));

    try {
      await dio.get<dynamic>('/x');
      fail('phải ném DioException');
    } on DioException catch (e) {
      expect(e.type, DioExceptionType.connectionTimeout);
      expect(e.apiException.isNetwork, isTrue);
    }
  });

  test('path được nối đúng sau baseUrl (giữ /v1)', () async {
    final dio = _create(net);
    String? seen;
    dio.httpClientAdapter = FakeHttpAdapter((o) {
      seen = o.uri.toString();
      return jsonBody({'ok': true});
    });
    await dio.get<dynamic>('/stations');
    expect(seen, 'https://api.test/v1/stations');
  });
}
