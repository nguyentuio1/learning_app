import '../../core/storage/auth_tokens.dart';
import '../entities/user.dart';
import '../entities/registered_session.dart';

/// Hợp đồng – presentation/usecase chỉ biết interface này, không biết Dio.
abstract interface class AuthRepository {
  Future<AuthTokens> login({required String email, required String password});
  Future<RegisteredSession> register({
    required String email,
    required String password,
    required String fullName,
    required String phoneNumber,
  });
  Future<void> logout();
  Future<User> getMe();
}
