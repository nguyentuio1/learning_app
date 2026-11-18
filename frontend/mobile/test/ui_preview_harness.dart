import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/config/app_config.dart';
import 'package:smart_drone_delivery/core/session/auth_controller.dart';
import 'package:smart_drone_delivery/core/theme/app_theme.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';
import 'package:smart_drone_delivery/routes/app_router.dart';

class _PreviewAuthController extends AuthController {
  @override
  AuthStatus build() => AuthStatus.authenticated;
}

class _PreviewUserController extends CurrentUserController {
  @override
  User? build() => User(
        id: 'preview-user-1',
        email: 'minhanh@example.com',
        fullName: 'Nguyen Minh Anh',
        phoneNumber: '0901234567',
        roleCode: 'CUSTOMER',
        status: 'ACTIVE',
        createdAt: DateTime.utc(2026, 11, 1),
      );
}

class _PreviewMyOrdersNotifier extends MyOrdersNotifier {
  final List<DeliveryOrder> initialOrders;
  _PreviewMyOrdersNotifier(this.initialOrders);

  @override
  Future<List<DeliveryOrder>> build() async => initialOrders;
}

void main() {
  final sampleOrders = [
    const DeliveryOrder(
      id: 'ord-1',
      trackingNumber: 'DD-202610-0985',
      status: 'IN_TRANSIT',
      recipientName: 'Nguyen Minh Anh',
      recipientPhone: '0901234567',
      originStationId: 'st-1',
      destinationStationId: 'st-2',
    ),
    const DeliveryOrder(
      id: 'ord-2',
      trackingNumber: 'DD-202610-0988',
      status: 'ARRIVED',
      recipientName: 'Tran Bao Linh',
      recipientPhone: '0907654321',
      originStationId: 'st-1',
      destinationStationId: 'st-2',
    ),
    const DeliveryOrder(
      id: 'ord-3',
      trackingNumber: 'DD-202610-0970',
      status: 'DELIVERED',
      recipientName: 'Le Quoc Bao',
      recipientPhone: '0912345678',
      originStationId: 'st-2',
      destinationStationId: 'st-1',
    ),
  ];

  runApp(
    ProviderScope(
      overrides: [
        appConfigProvider.overrideWithValue(AppConfig.customer()),
        authControllerProvider.overrideWith(_PreviewAuthController.new),
        currentUserProvider.overrideWith(_PreviewUserController.new),
        myOrdersProvider.overrideWith(() => _PreviewMyOrdersNotifier(sampleOrders)),
        unreadNotificationsCountProvider.overrideWith((_) async => 2),
      ],
      child: const _C1ToC5PreviewApp(),
    ),
  );
}

class _C1ToC5PreviewApp extends ConsumerStatefulWidget {
  const _C1ToC5PreviewApp();

  @override
  ConsumerState<_C1ToC5PreviewApp> createState() => _C1ToC5PreviewAppState();
}

class _C1ToC5PreviewAppState extends ConsumerState<_C1ToC5PreviewApp> {
  String _selected = 'C01';

  List<String> _collectPaths(List<RouteBase> routes, [String prefix = '']) {
    final result = <String>[];
    for (final r in routes) {
      if (r is GoRoute) {
        final full = r.path.startsWith('/')
            ? r.path
            : '${prefix.endsWith('/') ? prefix : '$prefix/'}${r.path}'
                .replaceAll('//', '/');
        result.add(full);
        result.addAll(_collectPaths(r.routes, full));
      } else if (r is ShellRoute) {
        result.addAll(_collectPaths(r.routes, prefix));
      }
    }
    return result;
  }

  String? _findRoute(List<String> paths, List<String> keywords) {
    for (final kw in keywords) {
      for (final p in paths) {
        if (p.toLowerCase().contains(kw) && !p.contains(':')) {
          return p;
        }
      }
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(routerProvider);
    final paths = _collectPaths(router.configuration.routes);

    final screens = <String, String?>{
      'C01 Login': _findRoute(paths, ['login', 'sign-in', 'auth']),
      'C02 Register': _findRoute(paths, ['register', 'sign-up', 'signup']),
      'C03 Home': _findRoute(paths, ['home']),
      'C04 Splash/Onboard': _findRoute(paths, ['splash', 'onboard', 'welcome']),
      'C05 Orders': _findRoute(paths, ['orders']),
    };

    return MaterialApp.router(
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(AppFlavor.customer),
      routerConfig: router,
      builder: (context, child) {
        return ColoredBox(
          color: const Color(0xFF1E2229),
          child: Column(
            children: [
              Material(
                color: const Color(0xFF111418),
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                  child: Row(
                    children: screens.entries.map((entry) {
                      final code = entry.key.split(' ').first;
                      final isSelected = _selected == code;
                      return Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: ChoiceChip(
                          label: Text(
                            entry.key,
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: isSelected ? Colors.white : Colors.black87,
                            ),
                          ),
                          selected: isSelected,
                          selectedColor: const Color(0xFF2B6341),
                          onSelected: (_) {
                            setState(() => _selected = code);
                            if (entry.value != null) {
                              router.go(entry.value!);
                            }
                          },
                        ),
                      );
                    }).toList(),
                  ),
                ),
              ),
              Expanded(
                child: Center(
                  child: Container(
                    width: 390,
                    height: 844,
                    clipBehavior: Clip.antiAlias,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(28),
                      boxShadow: const [
                        BoxShadow(color: Colors.black54, blurRadius: 24),
                      ],
                    ),
                    child: child,
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
