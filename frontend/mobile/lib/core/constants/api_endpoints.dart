/// Tập trung đường dẫn API khớp với tài liệu backend (docs/api/auth-api.md).
/// Ghép với baseUrl (mặc định: http://localhost:5055/api).
abstract final class ApiEndpoints {
  static const register = '/auth/register';
  static const login = '/auth/login';
  static const refresh = '/auth/refresh';
  static const logout = '/auth/logout';
  static const me = '/users/me';
}
