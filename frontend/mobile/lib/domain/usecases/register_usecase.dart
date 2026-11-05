import '../entities/registered_session.dart';
import '../repositories/auth_repository.dart';

class RegisterUseCase {
  const RegisterUseCase(this._repository);
  final AuthRepository _repository;

  Future<RegisteredSession> call({
    required String email,
    required String password,
    required String fullName,
    required String phoneNumber,
  }) => _repository.register(
    email: email.trim(),
    password: password,
    fullName: fullName.trim(),
    phoneNumber: phoneNumber,
  );
}
