import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

enum AppFlavor { customer, operator }

/// Cấu hình theo từng app. Đổi API lúc build:
///   --dart-define=API_BASE_URL=https://staging.example.com/api
class AppConfig {
  const AppConfig._({
    required this.flavor,
    required this.appName,
    required this.baseUrl,
    required this.applicationId,
  });

  final AppFlavor flavor;
  final String appName;
  final String baseUrl;

  /// Phải khớp applicationId trong android/app/build.gradle.kts (dùng để test/verify).
  final String applicationId;

  /// Địa chỉ API mặc định khi chạy local:
  /// - Android emulator dùng 10.0.2.2 để kết nối localhost của máy host
  /// - iOS simulator / Web / Desktop dùng localhost
  static String get _defaultApiBase {
    if (kIsWeb) return 'http://localhost:5055/api';
    if (defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:5055/api';
    }
    return 'http://localhost:5055/api';
  }

  static const _envBase = String.fromEnvironment('API_BASE_URL');

  static String get _apiBase =>
      _envBase.isNotEmpty ? _envBase : _defaultApiBase;

  factory AppConfig.customer() => AppConfig._(
        flavor: AppFlavor.customer,
        appName: 'SmartDrone Delivery',
        baseUrl: _apiBase,
        applicationId: 'com.smartdrone.smart_drone_delivery',
      );

  factory AppConfig.operator() => AppConfig._(
        flavor: AppFlavor.operator,
        appName: 'SmartDrone Station',
        baseUrl: _apiBase,
        applicationId: 'com.smartdrone.smart_drone_delivery.station',
      );
}

/// Luôn được override trong bootstrap; đọc trước đó sẽ ném lỗi.
final appConfigProvider = Provider<AppConfig>(
  (ref) => throw UnimplementedError('appConfigProvider must be overridden'),
);
