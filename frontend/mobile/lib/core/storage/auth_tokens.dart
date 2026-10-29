/// Cặp token phiên đăng nhập. Dart thuần (không phụ thuộc Flutter/package)
/// nên domain layer dùng được.
class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.refreshToken,
    this.expiresIn,
  });
  final String accessToken;
  final String refreshToken;
  final int? expiresIn;
}
