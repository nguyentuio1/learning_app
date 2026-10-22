import 'package:dio/dio.dart';

/// Domain-friendly error surfaced by repositories/controllers.
class ApiException implements Exception {
  const ApiException({
    required this.message,
    this.statusCode,
    this.code,
    this.isNetwork = false,
  });

  final String message;
  final int? statusCode;
  final String? code;
  final bool isNetwork;

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
        if (data is Map) {
          msg =
              _nonEmptyString(data['message']) ??
              _nonEmptyString(data['detail']) ??
              _nonEmptyString(data['title']);
          code = _nonEmptyString(data['code']);
        }
        return ApiException(
          message: msg ?? 'Something went wrong. Please try again.',
          statusCode: e.response?.statusCode,
          code: code,
        );
    }
  }

  static String? _nonEmptyString(dynamic value) {
    if (value is! String || value.trim().isEmpty) return null;
    return value.trim();
  }

  @override
  String toString() => 'ApiException($statusCode, $message)';
}
