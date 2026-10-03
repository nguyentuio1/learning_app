import 'package:flutter/foundation.dart';

import '../config/app_config.dart';

/// Toàn bộ thông số mạng ở một chỗ. Mọi giá trị có thể đổi lúc build bằng
/// `--dart-define`, không cần sửa code:
///
///   flutter run --dart-define=API_BASE_URL=https://staging.example.com/v1 \
///               --dart-define=HTTP_RECEIVE_TIMEOUT_MS=60000 \
///               --dart-define=HTTP_LOG_HEADERS=true
class NetworkConfig {
  const NetworkConfig({
    required this.baseUrl,
    this.connectTimeout = const Duration(seconds: 15),
    this.sendTimeout = const Duration(seconds: 20),
    this.receiveTimeout = const Duration(seconds: 20),
    this.enableLogging = false,
    this.logHeaders = false,
    this.logBodies = true,
    this.maxLogBodyLength = 2000,
  });

  /// Đã gồm version, ví dụ `https://api.example.com/v1` (không cần dấu `/` cuối).
  final String baseUrl;

  /// Thời gian chờ thiết lập kết nối (DNS + TCP + TLS).
  final Duration connectTimeout;

  /// Thời gian chờ gửi xong body lên server (chỉ áp dụng khi request có body).
  final Duration sendTimeout;

  /// Thời gian chờ server trả dữ liệu (tính giữa các lần nhận dữ liệu, không phải tổng thời gian).
  final Duration receiveTimeout;

  final bool enableLogging;
  final bool logHeaders;
  final bool logBodies;
  final int maxLogBodyLength;

  static const _connectMs =
      int.fromEnvironment('HTTP_CONNECT_TIMEOUT_MS', defaultValue: 15000);
  static const _sendMs =
      int.fromEnvironment('HTTP_SEND_TIMEOUT_MS', defaultValue: 20000);
  static const _receiveMs =
      int.fromEnvironment('HTTP_RECEIVE_TIMEOUT_MS', defaultValue: 20000);
  static const _logging = bool.fromEnvironment('HTTP_LOGGING', defaultValue: true);
  static const _logHeaders = bool.fromEnvironment('HTTP_LOG_HEADERS');
  static const _logBodies = bool.fromEnvironment('HTTP_LOG_BODIES', defaultValue: true);
  static const _maxBody =
      int.fromEnvironment('HTTP_LOG_MAX_BODY', defaultValue: 2000);

  /// Mặc định cho app: baseUrl lấy từ [AppConfig]; log chỉ bật khi KHÔNG phải
  /// bản release (dù có truyền HTTP_LOGGING=true cũng không bật ở release).
  factory NetworkConfig.fromApp(AppConfig app) => NetworkConfig(
        baseUrl: app.baseUrl,
        connectTimeout: const Duration(milliseconds: _connectMs),
        sendTimeout: const Duration(milliseconds: _sendMs),
        receiveTimeout: const Duration(milliseconds: _receiveMs),
        enableLogging: !kReleaseMode && _logging,
        logHeaders: _logHeaders,
        logBodies: _logBodies,
        maxLogBodyLength: _maxBody,
      );
}
