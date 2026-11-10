import 'package:dio/dio.dart';

/// Domain-friendly error surfaced by repositories/controllers.
class ApiException implements Exception {
  const ApiException({
    required this.message,
    this.statusCode,
    this.code,
    this.isNetwork = false,
    this.attemptsRemaining,
    this.lockoutExpiresAt,
    this.retryAfterSeconds,
  });

  final String message;
  final int? statusCode;
  final String? code;
  final bool isNetwork;
  final int? attemptsRemaining;
  final DateTime? lockoutExpiresAt;
  final int? retryAfterSeconds;

  factory ApiException.fromDio(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.connectionError:
        return const ApiException(
          message: 'Network problem. Check your connection and try again.',
          isNetwork: true,
        );
      case DioExceptionType.cancel:
        return const ApiException(message: 'Request cancelled.');
      default:
        final data = e.response?.data;
        String? msg;
        String? code;
        int? attemptsRemaining;
        DateTime? lockoutExpiresAt;
        int? retryAfterSeconds;

        final retryAfterHeader = e.response?.headers.value('retry-after');
        if (retryAfterHeader != null) {
          retryAfterSeconds = int.tryParse(retryAfterHeader.trim());
        }

        if (data is Map) {
          msg = data['detail']?.toString() ?? data['message']?.toString();
          if (msg == null && data['errors'] is Map) {
            final errs = data['errors'] as Map;
            for (final v in errs.values) {
              if (v is List && v.isNotEmpty) {
                msg = v.first.toString();
                break;
              }
            }
          }
          msg ??= data['title']?.toString();
          code = data['code']?.toString() ?? data['type']?.toString();
          if (data['attemptsRemaining'] is num) {
            attemptsRemaining = (data['attemptsRemaining'] as num).toInt();
          }
          if (data['lockoutExpiresAt'] is String) {
            lockoutExpiresAt =
                DateTime.tryParse(data['lockoutExpiresAt'] as String);
          }
          if (retryAfterSeconds == null && data['retryAfterSeconds'] is num) {
            retryAfterSeconds = (data['retryAfterSeconds'] as num).toInt();
          }
        }
        return ApiException(
          message: msg ?? 'Something went wrong. Please try again.',
          statusCode: e.response?.statusCode,
          code: code,
          attemptsRemaining: attemptsRemaining,
          lockoutExpiresAt: lockoutExpiresAt,
          retryAfterSeconds: retryAfterSeconds,
        );
    }
  }

  @override
  String toString() => 'ApiException($statusCode, $message)';
}
