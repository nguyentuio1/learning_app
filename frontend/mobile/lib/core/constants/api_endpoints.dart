/// Tập trung đường dẫn API để không rải chuỗi "/auth/..." khắp nơi.
abstract final class ApiEndpoints {
  static const login = '/auth/login';
  static const logout = '/auth/logout';
  static const refresh = '/auth/refresh';
  static const userProfile = '/users/me';
  static const changePassword = '/users/me/password';
  static const uploadAvatar = '/users/me/avatar';
}
