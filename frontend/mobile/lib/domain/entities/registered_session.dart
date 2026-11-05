import '../../core/storage/auth_tokens.dart';
import 'user.dart';

class RegisteredSession {
  const RegisteredSession({required this.tokens, required this.user});

  final AuthTokens tokens;
  final User user;
}
