import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/config/app_config.dart';
import '../core/session/auth_controller.dart';
import '../presentation/auth/login_page.dart';
import '../presentation/common/splash_page.dart';
import '../presentation/customer/home/customer_home_page.dart';
import '../presentation/operator/home/operator_home_page.dart';
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
    ],
  );

  ref.onDispose(() {
    router.dispose();
    refresh.dispose();
  });
  return router;
}, name: 'router');
