import '../../core/storage/auth_tokens.dart';
import '../repositories/auth_repository.dart';

/// Một hành động nghiệp vụ. Đặt quy tắc nghiệp vụ (chuẩn hoá, kiểm tra) ở đây.
class LoginUseCase {
  const LoginUseCase(this._repository);
  final AuthRepository _repository;

  Future<AuthTokens> call({required String email, required String password}) =>
      _repository.login(email: email.trim(), password: password);
}
