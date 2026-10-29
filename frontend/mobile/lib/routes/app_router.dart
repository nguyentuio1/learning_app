import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/config/app_config.dart';
import '../core/session/auth_controller.dart';
import '../data/models/delivery_order.dart';
import '../presentation/auth/login_page.dart';
import '../presentation/common/splash_page.dart';
import '../presentation/customer/addresses/addresses_list_page.dart';
import '../presentation/customer/home/customer_home_page.dart';
import '../presentation/customer/orders/edit_delivery_order_page.dart';
import '../presentation/customer/orders/my_orders_page.dart';
import '../presentation/customer/orders/order_detail_page.dart';
import '../presentation/customer/packages/add_edit_package_page.dart';
import '../presentation/customer/packages/packages_list_page.dart';
import '../presentation/operator/home/operator_home_page.dart';
import '../presentation/profile/change_password_page.dart';
import '../presentation/profile/edit_profile_page.dart';
import '../presentation/profile/profile_page.dart';
import 'route_paths.dart';

/// Router theo flavor + chặn truy cập theo trạng thái đăng nhập:
///   unknown         -> /splash   (đang khôi phục phiên)
///   unauthenticated -> /login
///   authenticated   -> trang chủ của flavor (nếu đang ở /splash hoặc /login)
final routerProvider = Provider<GoRouter>((ref) {
  final flavor = ref.watch(appConfigProvider).flavor;
  final home = switch (flavor) {
    AppFlavor.customer => RoutePaths.customerHome,
    AppFlavor.operator => RoutePaths.operatorHome,
  };

  // Báo cho GoRouter chạy lại `redirect` mỗi khi AuthStatus đổi.
  final refresh = ValueNotifier<int>(0);
  ref.listen<AuthStatus>(authControllerProvider, (_, _) => refresh.value++);

  final router = GoRouter(
    initialLocation: RoutePaths.splash,
    refreshListenable: refresh,
    redirect: (context, state) {
      final loc = state.matchedLocation;
      switch (ref.read(authControllerProvider)) {
        case AuthStatus.unknown:
          return loc == RoutePaths.splash ? null : RoutePaths.splash;
        case AuthStatus.unauthenticated:
          return loc == RoutePaths.login ? null : RoutePaths.login;
        case AuthStatus.authenticated:
          final onEntry = loc == RoutePaths.splash || loc == RoutePaths.login;
          return onEntry ? home : null;
      }
    },
    routes: [
      GoRoute(path: RoutePaths.splash, builder: (_, _) => const SplashPage()),
      GoRoute(path: RoutePaths.login, builder: (_, _) => const LoginPage()),
      if (flavor == AppFlavor.customer)
        GoRoute(
          path: RoutePaths.customerHome,
          builder: (_, _) => const CustomerHomePage(),
        )
      else
        GoRoute(
          path: RoutePaths.operatorHome,
          builder: (_, _) => const OperatorHomePage(),
        ),
      GoRoute(
        path: RoutePaths.profile,
        builder: (_, _) => const ProfilePage(),
      ),
      GoRoute(
        path: RoutePaths.editProfile,
        builder: (_, _) => const EditProfilePage(),
      ),
      GoRoute(
        path: RoutePaths.changePassword,
        builder: (_, _) => const ChangePasswordPage(),
      ),
      if (flavor == AppFlavor.customer)
        GoRoute(
          path: RoutePaths.customerAddresses,
          builder: (_, _) => const AddressesListPage(),
        ),
      if (flavor == AppFlavor.customer) ...[
        GoRoute(
          path: RoutePaths.customerOrders,
          builder: (_, _) => const MyOrdersPage(),
        ),
        GoRoute(
          path: RoutePaths.customerOrderDetails,
          builder: (_, state) {
            final order = state.extra;
            return order is DeliveryOrder
                ? OrderDetailPage(order: order)
                : const Scaffold(body: Center(child: Text('Order not found.')));
          },
        ),
        GoRoute(
          path: RoutePaths.customerOrdersEdit,
          builder: (_, state) {
            final order = state.extra;
            return order is DeliveryOrder
                ? EditDeliveryOrderPage(order: order)
                : const Scaffold(body: Center(child: Text('Order not found.')));
          },
        ),
        GoRoute(
          path: RoutePaths.customerPackages,
          builder: (_, _) => const PackagesListPage(),
        ),
        GoRoute(
          path: RoutePaths.customerPackagesAdd,
          builder: (_, _) => const AddEditPackagePage(),
        ),
        GoRoute(
          path: RoutePaths.customerPackagesEdit,
          builder: (_, state) {
            return EditPackageRoute(
              packageId: state.uri.queryParameters['id'] ?? '',
            );
          },
        ),
      ],
    ],
  );

  ref.onDispose(() {
    router.dispose();
    refresh.dispose();
  });
  return router;
}, name: 'router');
