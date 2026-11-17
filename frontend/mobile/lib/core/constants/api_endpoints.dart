/// Tập trung đường dẫn API khớp với tài liệu backend (docs/api/auth-api.md, docs/api/station-api.md, docs/api/notification-api.md).
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
  static const failedOrders = '/dispatch/failed-orders';

  static String landingStation(String id) => '$landingStations/$id';
  static String landingStationAvailability(String id) =>
      '$landingStations/$id/availability';
  static String stationSlots(String stationId) =>
      '$landingStations/$stationId/slots';
  static String expiredParcels(String stationId) =>
      '$landingStations/$stationId/operations/expired-parcels';
  static String failedOrderDetail(String id) => '$failedOrders/$id';
  static String reportDeliveryFailure(String id) => '$deliveryOrders/$id/fail';
  static String resolveDeliveryFailure(String id) =>
      '$deliveryOrders/$id/resolve-failure';

  // Station Operations - Package Pickup (LA-29 / LA-126)
  static String stationOrderLookup(String stationId, String trackingNumber) =>
      '$landingStations/$stationId/operations/orders/$trackingNumber';
  static String stationPickup(String stationId) =>
      '$landingStations/$stationId/operations/pickup';
  static String clearExpiredParcel(String stationId) =>
      '$landingStations/$stationId/operations/clear-expired-parcel';

  // Customer Delivery Confirmation (LA-37 / LA-151)
  static String confirmDelivery(String id) =>
      '$deliveryOrders/$id/confirm-delivery';

  // Customer Notifications (LA-326)
  static const notifications = '/notifications';
  static const notificationsUnreadCount = '/notifications/unread-count';
  static String notificationRead(String id) => '$notifications/$id/read';
  static const notificationsReadAll = '/notifications/read-all';

  // Customer AI Assistant (LA-42 / LA-167)
  static const aiAssistantMessages = '/ai-assistant/messages';
  static const aiAssistantSessions = '/ai-assistant/sessions';
  static String aiAssistantSessionMessages(String sessionId) =>
      '$aiAssistantSessions/${Uri.encodeComponent(sessionId)}/messages';
}
