import '../../core/storage/auth_tokens.dart';

/// Hợp đồng – presentation/usecase chỉ biết interface này, không biết Dio.
abstract interface class AuthRepository {
  Future<AuthTokens> login({required String email, required String password});
  Future<void> logout();
}
