import '../../core/storage/auth_tokens.dart';
import 'user_model.dart';

/// DTO phản hồi xác thực (`AuthResponse`) từ server.
/// Hỗ trợ cả camelCase (chuẩn backend) lẫn snake_case (legacy/mock).
class AuthResponseModel {
  const AuthResponseModel({
    required this.accessToken,
    required this.refreshToken,
    this.expiresIn = 900,
    this.user,
  });

  final String accessToken;
  final String refreshToken;
  final int expiresIn;
  final UserModel? user;

  factory AuthResponseModel.fromJson(Map<String, dynamic> json) {
    final access = json['accessToken'] as String;
    final refresh = json['refreshToken'] as String;
    final expires = json['expiresIn'] as int? ?? 900;
    final userMap = json['user'] as Map<String, dynamic>?;

    return AuthResponseModel(
      accessToken: access,
      refreshToken: refresh,
      expiresIn: expires,
      user: userMap != null ? UserModel.fromJson(userMap) : null,
    );
  }

  AuthTokens toTokens() => AuthTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
        expiresIn: expiresIn,
      );
}
