import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';

void main() {
  test('parses RFC 7807 problem details with detail and status', () {
    final dioErr = DioException(
      requestOptions: RequestOptions(path: '/api/auth/login'),
      response: Response(
        requestOptions: RequestOptions(path: '/api/auth/login'),
        statusCode: 401,
        data: {
          'type': 'https://httpstatuses.com/401',
          'title': 'Unauthorized',
          'status': 401,
          'detail': 'Invalid email or password.',
        },
      ),
      type: DioExceptionType.badResponse,
    );

    final apiEx = ApiException.fromDio(dioErr);

    expect(apiEx.statusCode, 401);
    expect(apiEx.message, 'Invalid email or password.');
    expect(apiEx.isNetwork, isFalse);
  });

  test('extracts first validation error from errors dictionary', () {
    final dioErr = DioException(
      requestOptions: RequestOptions(path: '/api/auth/register'),
      response: Response(
        requestOptions: RequestOptions(path: '/api/auth/register'),
        statusCode: 400,
        data: {
          'type': 'https://httpstatuses.com/400',
          'title': 'Bad Request',
          'status': 400,
          'errors': {
            'email': ['Email is required.'],
            'password': ['Password must be at least 8 characters.'],
          },
        },
      ),
      type: DioExceptionType.badResponse,
    );

    final apiEx = ApiException.fromDio(dioErr);

    expect(apiEx.statusCode, 400);
    expect(apiEx.message, 'Email is required.');
  });

  test('parses timeout as network exception', () {
    final dioErr = DioException.connectionTimeout(
      timeout: const Duration(seconds: 10),
      requestOptions: RequestOptions(path: '/test'),
    );

    final apiEx = ApiException.fromDio(dioErr);

    expect(apiEx.isNetwork, isTrue);
    expect(apiEx.message, contains('Network problem'));
  });
}
