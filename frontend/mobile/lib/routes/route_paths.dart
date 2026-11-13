/// Tất cả đường dẫn route ở một chỗ. Màn hình điều hướng bằng
/// `context.go(RoutePaths.login)` thay vì viết chuỗi trực tiếp.
abstract final class RoutePaths {
  static const splash = '/splash';
  static const login = '/login';
  static const register = '/register';
  static const customerHome = '/customer';
  static const customerAddresses = '/customer/addresses';
  static const customerPackages = '/customer/packages';
  static const customerPackagesAdd = '/customer/packages/add';
  static const customerPackagesEdit = '/customer/packages/edit';
  static const customerOrders = '/customer/orders';
  static const customerOrderDetails = '/customer/orders/:orderId';
  static const customerOrdersCreate = '/customer/orders/create';
  static const customerOrdersReview = '/customer/orders/create/review';
  static const customerOrderSubmitted = '/customer/orders/create/submitted';
  static const customerOrdersEdit = '/customer/orders/edit';
  static const operatorHome = '/operator';
  static const operatorPickup = '/operator/pickup';
  static const operatorFailedOrders = '/operator/failed-orders';
  static const operatorFailedOrderDetail = '/operator/failed-orders/:orderId';
  static String operatorFailedOrderDetailFor(String orderId) =>
      '/operator/failed-orders/$orderId';
  static const profile = '/profile';
  static const editProfile = '/profile/edit';
  static const changePassword = '/profile/change-password';
}
