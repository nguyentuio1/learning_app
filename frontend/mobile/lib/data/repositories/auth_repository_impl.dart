import 'package:dio/dio.dart';

import '../../core/constants/api_endpoints.dart';
import '../../core/network/auth_interceptor.dart';
import '../../core/network/error_interceptor.dart';
import '../../core/storage/auth_tokens.dart';
import '../../core/storage/token_storage.dart';
import '../../core/storage/user_storage.dart';
import '../../domain/entities/user.dart';
import '../../domain/repositories/auth_repository.dart';
import '../models/auth_response_model.dart';
import '../models/user_model.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._dio, [this._storage, this._userStorage]);
  final Dio _dio;
  final TokenStorage? _storage;
  final UserStorage? _userStorage;

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
      final authResponse = AuthResponseModel.fromJson(d);
      if (authResponse.user != null) {
        await _userStorage?.write(authResponse.user!.toEntity());
      }
      return authResponse.toTokens();
    } on DioException catch (e) {
      throw e.apiException; // repository chỉ ném ApiException
    }
  }

  @override
  Future<User> getMe() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.me,
      );
      final user = UserModel.fromJson(res.data!).toEntity();
      await _userStorage?.write(user);
      return user;
    } on DioException catch (e) {
      throw e.apiException;
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
    } finally {
      await _userStorage?.clear();
    }
  }
}
