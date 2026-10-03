/// Tập trung đường dẫn API để không rải chuỗi "/auth/..." khắp nơi.
abstract final class ApiEndpoints {
  static const login = '/auth/login'; // TODO: khớp backend
  static const logout = '/auth/logout';
  static const refresh = '/auth/refresh';
}
