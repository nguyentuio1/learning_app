import 'package:dio/dio.dart';

import '../error/api_exception.dart';

/// Wraps every failure so callers can `catch (e) { e.error as ApiException }`.
class ErrorInterceptor extends Interceptor {
  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (err.error is ApiException) return handler.next(err); // already mapped
    handler.next(err.copyWith(error: ApiException.fromDio(err)));
  }
}

extension DioExceptionX on DioException {
  ApiException get apiException =>
      error is ApiException ? error as ApiException : ApiException.fromDio(this);
}
