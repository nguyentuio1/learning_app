import 'package:smart_drone_delivery/core/network/logging_interceptor.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../support/fake_http_adapter.dart';

Dio _dio(
  List<String> logs,
  ResponseBody Function(RequestOptions) handler, {
  bool headers = true,
  bool bodies = true,
  int maxBody = 2000,
}) {
  final dio = Dio(BaseOptions(baseUrl: 'https://api.test/v1'));
  dio.httpClientAdapter = FakeHttpAdapter(handler);
  dio.interceptors.add(AppLogInterceptor(
    logger: logs.add,
    logHeaders: headers,
    logBodies: bodies,
    maxBodyLength: maxBody,
  ));
  return dio;
}

void main() {
  test('log request/response và che Authorization, password, token', () async {
    final logs = <String>[];
    final dio = _dio(
      logs,
      (_) => jsonBody({
        'access_token': 'SECRET-ACCESS',
        'user': {'name': 'An', 'refresh_token': 'SECRET-REFRESH'},
      }),
    );

    await dio.post<dynamic>(
      '/auth/login',
      data: {'email': 'a@b.c', 'password': 'hunter2'},
      options: Options(headers: {'Authorization': 'Bearer SECRET-BEARER'}),
    );

    final all = logs.join('\n');
    expect(all, contains('--> POST https://api.test/v1/auth/login'));
    expect(all, contains('<-- 200 POST'));
    expect(all, contains('a@b.c')); // dữ liệu thường vẫn thấy
    for (final secret in ['hunter2', 'SECRET-BEARER', 'SECRET-ACCESS', 'SECRET-REFRESH']) {
      expect(all, isNot(contains(secret)), reason: '$secret bị lộ trong log');
    }
    expect(all, contains('***'));
  });

  test('log lỗi 500 kèm status và body', () async {
    final logs = <String>[];
    final dio = _dio(logs, (_) => jsonBody({'message': 'boom'}, 500));

    await expectLater(dio.get<dynamic>('/x'), throwsA(isA<DioException>()));

    final all = logs.join('\n');
    expect(all, contains('<-x 500 GET'));
    expect(all, contains('boom'));
  });

  test('body dài bị cắt', () async {
    final logs = <String>[];
    final dio = _dio(logs, (_) => jsonBody({'data': 'x' * 200}), maxBody: 30);

    await dio.get<dynamic>('/x');

    expect(logs.join('\n'), contains('truncated'));
  });

  test('logBodies=false thì không log body', () async {
    final logs = <String>[];
    final dio = _dio(logs, (_) => jsonBody({'ok': true}), bodies: false);

    await dio.post<dynamic>('/x', data: {'email': 'a@b.c'});

    final all = logs.join('\n');
    expect(all, isNot(contains('a@b.c')));
    expect(all, isNot(contains('body:')));
  });

  test('LogRedactor che key nhạy cảm ở mọi cấp lồng nhau', () {
    final out = LogRedactor.redact({
      'items': [
        {'Token': 'abc', 'name': 'ok'},
      ],
      'password': 'p',
    });
    expect(out, {
      'items': [
        {'Token': '***', 'name': 'ok'},
      ],
      'password': '***',
    });
  });
}
