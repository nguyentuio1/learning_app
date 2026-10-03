import 'package:dio/dio.dart';

import '../storage/token_storage.dart';

/// Attaches the bearer token and transparently refreshes it on 401.
///
/// - Concurrent 401s share ONE in-flight refresh (single-flight).
/// - A request whose token was already rotated by another refresh is simply
///   retried with the new token.
/// - Refresh is rejected (400/401/403) -> tokens cleared, [onSessionExpired].
/// - Refresh fails for network reasons -> user stays logged in, original error
///   is surfaced.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required this.dio,
    required this.refreshDio,
    required this.storage,
    required this.onSessionExpired,
  });

  /// Main client; used to replay the failed request.
  final Dio dio;

  /// Bare client (no interceptors) used for the refresh call.
  final Dio refreshDio;
  final TokenStorage storage;
  final void Function() onSessionExpired;

  /// Set `extra: {AuthInterceptor.skipAuth: true}` on login/refresh/public calls.
  static const skipAuth = 'skipAuth';
  static const _retried = 'authRetried';

  Future<AuthTokens?>? _refreshing;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (options.extra[skipAuth] != true) {
      final tokens = await storage.read();
      if (tokens != null) {
        options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
      }
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final req = err.requestOptions;
    final shouldRefresh = err.response?.statusCode == 401 &&
        req.extra[skipAuth] != true &&
        req.extra[_retried] != true;
    if (!shouldRefresh) return handler.next(err);

    final current = await storage.read();
    if (current == null) return handler.next(err);

    try {
      var accessToken = current.accessToken;

      // Only refresh if the token we sent is still the live one.
      if (req.headers['Authorization'] == 'Bearer ${current.accessToken}') {
        final refreshed = await (_refreshing ??=
            _refresh(current.refreshToken).whenComplete(() => _refreshing = null));
        if (refreshed == null) {
          await storage.clear();
          onSessionExpired();
          return handler.next(err);
        }
        accessToken = refreshed.accessToken;
      }

      req.headers['Authorization'] = 'Bearer $accessToken';
      req.extra[_retried] = true;
      handler.resolve(await dio.fetch<dynamic>(req));
    } on DioException catch (e) {
      handler.next(e);
    } catch (_) {
      handler.next(err);
    }
  }

  /// Returns new tokens, `null` if the refresh token is no longer valid,
  /// and throws on transient (network/5xx) failures.
  Future<AuthTokens?> _refresh(String refreshToken) async {
    try {
      final res = await refreshDio.post<Map<String, dynamic>>(
        '/auth/refresh', // TODO: match your backend contract
        data: {'refresh_token': refreshToken},
        options: Options(extra: {skipAuth: true}),
      );
      final d = res.data!;
      final tokens = AuthTokens(
        accessToken: d['access_token'] as String,
        refreshToken: d['refresh_token'] as String,
      );
      await storage.write(tokens);
      return tokens;
    } on DioException catch (e) {
      final code = e.response?.statusCode;
      if (code == 400 || code == 401 || code == 403) return null;
      rethrow;
    }
  }
}
