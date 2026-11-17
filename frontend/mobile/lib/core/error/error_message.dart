import 'package:dio/dio.dart';

import '../network/error_interceptor.dart';
import 'api_exception.dart';

/// Chuyển mọi loại lỗi thành câu thông báo thân thiện để hiển thị trên UI.
String errorMessage(Object error) {
  if (error is ApiException) return error.message;
  if (error is DioException) return error.apiException.message;
  return 'Something went wrong. Please try again.';
}

/// Kiểm tra lỗi có phải là lỗi mạng hay không.
bool isNetworkError(Object error) {
  if (error is ApiException) return error.isNetwork;
  if (error is DioException) return error.apiException.isNetwork;
  return false;
}
