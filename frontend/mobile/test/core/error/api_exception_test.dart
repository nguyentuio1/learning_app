import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/core/error/error_message.dart';

void main() {
  group('ApiException parsing', () {
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

    test('parses connection error as network exception', () {
      final dioErr = DioException.connectionError(
        requestOptions: RequestOptions(path: '/test'),
        reason: 'Host lookup failed',
      );

      final apiEx = ApiException.fromDio(dioErr);

      expect(apiEx.isNetwork, isTrue);
      expect(apiEx.message, contains('Network problem'));
    });

    test('parses request cancelled exception', () {
      final dioErr = DioException(
        requestOptions: RequestOptions(path: '/test'),
        type: DioExceptionType.cancel,
      );

      final apiEx = ApiException.fromDio(dioErr);

      expect(apiEx.message, 'Request cancelled.');
      expect(apiEx.isNetwork, isFalse);
    });
  });

  group('HTTP Status Code Fallback Messages', () {
    DioException createEmptyResponseException(int statusCode) {
      return DioException(
        requestOptions: RequestOptions(path: '/test'),
        response: Response(
          requestOptions: RequestOptions(path: '/test'),
          statusCode: statusCode,
          data: null,
        ),
        type: DioExceptionType.badResponse,
      );
    }

    test('falls back to 401 message when body is empty', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(401));
      expect(apiEx.statusCode, 401);
      expect(apiEx.message, 'Missing, invalid or expired token.');
    });

    test('falls back to 403 message when body is empty', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(403));
      expect(apiEx.statusCode, 403);
      expect(
        apiEx.message,
        'You do not have permission to perform this action.',
      );
    });

    test('falls back to 404 message when body is empty', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(404));
      expect(apiEx.statusCode, 404);
      expect(apiEx.message, 'The requested resource was not found.');
    });

    test('falls back to 409 message when body is empty', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(409));
      expect(apiEx.statusCode, 409);
      expect(
        apiEx.message,
        'This item has changed. Reload it before continuing.',
      );
    });

    test('falls back to 423 message when body is empty', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(423));
      expect(apiEx.statusCode, 423);
      expect(apiEx.message, 'Too many wrong codes. Try again later.');
    });

    test('falls back to default 500 message when body is empty', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(500));
      expect(apiEx.statusCode, 500);
      expect(apiEx.message, 'Something went wrong. Please try again.');
    });

    test('falls back to default message when status code is 503', () {
      final apiEx = ApiException.fromDio(createEmptyResponseException(503));
      expect(apiEx.statusCode, 503);
      expect(apiEx.message, 'Something went wrong. Please try again.');
    });
  });

  group('isNetworkError helper', () {
    test('returns true for ApiException with isNetwork = true', () {
      const ex = ApiException(message: 'Timeout', isNetwork: true);
      expect(isNetworkError(ex), isTrue);
    });

    test('returns false for ApiException with isNetwork = false', () {
      const ex = ApiException(message: 'Bad Request', isNetwork: false);
      expect(isNetworkError(ex), isFalse);
    });

    test('returns true for DioException network timeout', () {
      final dioErr = DioException.connectionTimeout(
        timeout: const Duration(seconds: 5),
        requestOptions: RequestOptions(path: '/test'),
      );
      expect(isNetworkError(dioErr), isTrue);
    });

    test('returns false for generic errors', () {
      expect(isNetworkError(Exception('Generic')), isFalse);
      expect(isNetworkError('Some string error'), isFalse);
    });
  });
}
