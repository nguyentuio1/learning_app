import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

void _print(String message) => debugPrint(message, wrapWidth: 1024);

/// Che dữ liệu nhạy cảm trước khi ghi log.
abstract final class LogRedactor {
  static const mask = '***';

  static const sensitiveHeaders = {
    'authorization',
    'proxy-authorization',
    'cookie',
    'set-cookie',
    'x-api-key',
  };

  static const sensitiveKeys = {
    'password',
    'old_password',
    'new_password',
    'current_password',
    'token',
    'access_token',
    'refresh_token',
    'id_token',
    'authorization',
    'secret',
    'client_secret',
    'otp',
    'pin',
    'card_number',
    'cvv',
  };

  static Map<String, String> headers(Map<String, Object?> headers) => {
        for (final e in headers.entries)
          e.key: sensitiveHeaders.contains(e.key.toLowerCase()) ? mask : '${e.value}',
      };

  /// Duyệt đệ quy Map/List, thay giá trị của các key nhạy cảm bằng [mask].
  static Object? redact(Object? value) {
    if (value is Map) {
      return {
        for (final e in value.entries)
          '${e.key}': sensitiveKeys.contains('${e.key}'.toLowerCase())
              ? mask
              : redact(e.value),
      };
    }
    if (value is List) return value.map(redact).toList();
    return value;
  }
}

/// Log request / response / lỗi gọn gàng, kèm thời gian, đã che dữ liệu nhạy cảm.
///
///   --> POST https://api.example.com/v1/auth/login
///       body: {"email":"a@b.c","password":"***"}
///   <-- 200 POST https://api.example.com/v1/auth/login (123 ms)
///       body: {"access_token":"***","user":{...}}
///   <-x 401 GET https://api.example.com/v1/me (87 ms) [badResponse]
class AppLogInterceptor extends Interceptor {
  AppLogInterceptor({
    this.logHeaders = false,
    this.logBodies = true,
    this.maxBodyLength = 2000,
    void Function(String message)? logger,
  }) : _logger = logger ?? _print;

  final bool logHeaders;
  final bool logBodies;
  final int maxBodyLength;
  final void Function(String message) _logger;

  static const _startKey = 'logStartMicros';

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    options.extra[_startKey] = DateTime.now().microsecondsSinceEpoch;
    final b = StringBuffer('--> ${options.method} ${options.uri}');
    if (logHeaders) {
      b.write('\n    headers: ${LogRedactor.headers(options.headers)}');
    }
    final body = _formatBody(options.data);
    if (body != null) b.write('\n    body: $body');
    _logger(b.toString());
    handler.next(options);
  }

  @override
  void onResponse(Response<dynamic> response, ResponseInterceptorHandler handler) {
    final o = response.requestOptions;
    final b = StringBuffer(
        '<-- ${response.statusCode} ${o.method} ${o.uri} (${_elapsed(o)})');
    if (logHeaders) {
      final h = response.headers.map.map((k, v) => MapEntry(k, v.join(', ')));
      b.write('\n    headers: ${LogRedactor.headers(h)}');
    }
    final body = _formatBody(response.data);
    if (body != null) b.write('\n    body: $body');
    _logger(b.toString());
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    final o = err.requestOptions;
    final status = err.response?.statusCode ?? '-';
    final b = StringBuffer(
        '<-x $status ${o.method} ${o.uri} (${_elapsed(o)}) [${err.type.name}]');
    final body = _formatBody(err.response?.data);
    if (body != null) b.write('\n    body: $body');
    _logger(b.toString());
    handler.next(err);
  }

  String _elapsed(RequestOptions o) {
    final start = o.extra[_startKey];
    if (start is! int) return '? ms';
    return '${(DateTime.now().microsecondsSinceEpoch - start) ~/ 1000} ms';
  }

  String? _formatBody(Object? data) {
    if (!logBodies || data == null) return null;
    if (data is FormData) {
      return '<FormData: ${data.fields.length} fields, ${data.files.length} files>';
    }
    if (data is Stream || data is List<int>) return '<binary>';

    var value = data;
    if (value is String) {
      try {
        value = jsonDecode(value); // body dạng chuỗi JSON -> vẫn che được key nhạy cảm
      } catch (_) {/* giữ nguyên chuỗi */}
    }
    String text;
    try {
      final redacted = LogRedactor.redact(value);
      text = redacted is String ? redacted : jsonEncode(redacted);
    } catch (_) {
      text = '$value';
    }
    if (text.length <= maxBodyLength) return text;
    return '${text.substring(0, maxBodyLength)}…[truncated ${text.length - maxBodyLength} chars]';
  }
}
