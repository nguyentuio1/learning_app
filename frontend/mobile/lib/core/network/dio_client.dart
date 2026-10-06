import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../storage/token_storage.dart';
import 'auth_interceptor.dart';
import 'error_interceptor.dart';
import 'logging_interceptor.dart';
import 'network_config.dart';

abstract final class DioClient {
  /// Cấu hình chung cho mọi request: baseUrl, 3 loại timeout, header mặc định.
  static BaseOptions baseOptions(AppConfig app, NetworkConfig network) => BaseOptions(
        baseUrl: network.baseUrl,
        connectTimeout: network.connectTimeout,
        sendTimeout: network.sendTimeout,
        receiveTimeout: network.receiveTimeout,
        responseType: ResponseType.json,
        contentType: Headers.jsonContentType,
        headers: {
          'Accept': Headers.jsonContentType,
          'X-App': app.flavor.name, // để backend biết request từ app nào
        },
        // validateStatus giữ mặc định: chỉ 2xx là thành công, còn lại ném DioException.
      );

  static AppLogInterceptor _logger(NetworkConfig n) => AppLogInterceptor(
        logHeaders: n.logHeaders,
        logBodies: n.logBodies,
        maxBodyLength: n.maxLogBodyLength,
      );

  static Dio create({
    required AppConfig config,
    required TokenStorage storage,
    required void Function() onSessionExpired,
    NetworkConfig? network,
  }) {
    final net = network ?? NetworkConfig.fromApp(config);

    // Fail fast nếu baseUrl sai (ví dụ truyền --dart-define=API_BASE_URL= rỗng).
    final uri = Uri.tryParse(net.baseUrl);
    if (uri == null || !uri.hasScheme || uri.host.isEmpty) {
      throw ArgumentError.value(
        net.baseUrl,
        'baseUrl',
        'phải là URL tuyệt đối dạng https://host/v1',
      );
    }

    final dio = Dio(baseOptions(config, net));
    // Client "trần" chỉ để gọi refresh token (không có AuthInterceptor -> không lặp vô hạn).
    final refreshDio = Dio(baseOptions(config, net));

    // THỨ TỰ QUAN TRỌNG: Log đứng đầu để thấy cả lần 401 trước khi
    // AuthInterceptor refresh token và gửi lại request.
    dio.interceptors.addAll([
      if (net.enableLogging) _logger(net),
      AuthInterceptor(
        dio: dio,
        refreshDio: refreshDio,
        storage: storage,
        onSessionExpired: onSessionExpired,
      ),
      ErrorInterceptor(),
    ]);
    // Log cả lời gọi refresh (body chứa refresh_token đã được che).
    if (net.enableLogging) refreshDio.interceptors.add(_logger(net));

    return dio;
  }
}
