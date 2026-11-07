/// Tập trung đường dẫn API khớp với tài liệu backend (docs/api/auth-api.md, docs/api/station-api.md).
/// Ghép với baseUrl (mặc định: http://localhost:5055/api).
abstract final class ApiEndpoints {
  static const register = '/auth/register';
  static const login = '/auth/login';
  static const refresh = '/auth/refresh';
  static const logout = '/auth/logout';
  static const me = '/users/me';
  static const changePassword = '/users/me/password';
  static const uploadAvatar = '/users/me/avatar';
  static const customerAddresses = '/customer-addresses';
  static const landingStations = '/landing-stations';
  static const packages = '/packages';
  static const deliveryOrders = '/delivery-orders';

  // Station Operations (LA-29)
  static String stationOperations(String stationId) =>
      '$landingStations/$stationId/operations';
  static String stationOrderLookup(String stationId, String trackingNumber) =>
      '$landingStations/$stationId/operations/orders/$trackingNumber';
  static String stationIntake(String stationId) =>
      '$landingStations/$stationId/operations/intake';
  static String stationIntakeReject(String stationId) =>
      '$landingStations/$stationId/operations/intake-reject';
  static String stationPickup(String stationId) =>
      '$landingStations/$stationId/operations/pickup';
  static String stationValidateCode(String stationId) =>
      '$landingStations/$stationId/operations/validate-code';
}
