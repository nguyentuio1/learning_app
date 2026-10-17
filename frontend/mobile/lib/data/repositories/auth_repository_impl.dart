import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/auth_interceptor.dart';
import '../../core/network/error_interceptor.dart';
import '../../core/storage/auth_tokens.dart';
import '../../core/storage/token_storage.dart';
import '../../domain/repositories/auth_repository.dart';
import '../models/auth_response_model.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._dio, [this._storage]);
  final Dio _dio;
  final TokenStorage? _storage;

  // Gọi public: không gắn bearer token, không thử refresh khi 401.
  static final _public = Options(extra: {AuthInterceptor.skipAuth: true});

  @override
  Future<AuthTokens> login({
    required String email,
    required String password,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.login,
        data: {'email': email, 'password': password},
        options: _public,
      );
      final d = res.data!;
      return AuthResponseModel.fromJson(d).toTokens();
    } on DioException catch (e) {
      throw e.apiException; // repository chỉ ném ApiException
    }
  }

  @override
  Future<void> logout() async {
    try {
      final tokens = await _storage?.read();
      await _dio.post<void>(
        ApiEndpoints.logout,
        data: {
          if (tokens != null) 'refreshToken': tokens.refreshToken,
        },
      );
    } on DioException {
      // Best-effort: dù server lỗi, app vẫn xoá token cục bộ.
    }
  }
}
