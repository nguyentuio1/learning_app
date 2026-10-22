/// Tất cả đường dẫn route ở một chỗ. Màn hình điều hướng bằng
/// `context.go(RoutePaths.login)` thay vì viết chuỗi trực tiếp.
abstract final class RoutePaths {
  static const splash = '/splash';
  static const login = '/login';
  static const customerHome = '/customer';
  static const customerAddresses = '/customer/addresses';
  static const customerPackages = '/customer/packages';
  static const customerPackagesAdd = '/customer/packages/add';
  static const customerPackagesEdit = '/customer/packages/edit';
  static const customerOrders = '/customer/orders';
  static const customerOrderDetails = '/customer/orders/:orderId';
  static const customerOrdersEdit = '/customer/orders/edit';
  static const operatorHome = '/operator';
}
