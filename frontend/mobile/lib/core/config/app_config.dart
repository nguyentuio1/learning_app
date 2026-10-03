import 'package:flutter_riverpod/flutter_riverpod.dart';

enum AppFlavor { customer, operator }

/// Cấu hình theo từng app. Đổi API lúc build:
///   --dart-define=API_BASE_URL=https://staging.example.com/v1
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

  static const _apiBase = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://api.example.com/v1', // TODO: thay bằng URL backend thật
  );

  factory AppConfig.customer() => const AppConfig._(
        flavor: AppFlavor.customer,
        appName: 'SmartDrone Delivery',
        baseUrl: _apiBase,
        applicationId: 'com.smartdrone.smart_drone_delivery',
      );

  factory AppConfig.operator() => const AppConfig._(
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
