import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/config/app_config.dart';
import 'package:smart_drone_delivery/core/theme/app_theme.dart';
import 'package:smart_drone_delivery/data/models/customer_address.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/presentation/customer/home/customer_home_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_submitted_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _sampleOrders = [
  DeliveryOrder(
    id: '550e8400-e29b-41d4-a716-446655440001',
    recipientName: 'Nguyen Minh Anh',
    recipientPhone: '0901234567',
    originStationId: '550e8400-e29b-41d4-a716-446655440011',
    destinationStationId: '550e8400-e29b-41d4-a716-446655440012',
    status: 'IN_TRANSIT',
    trackingNumber: 'LA363-2026-001',
  ),
  DeliveryOrder(
    id: '550e8400-e29b-41d4-a716-446655440002',
    recipientName: 'Tran Bao Linh',
    recipientPhone: '0912345678',
    originStationId: '550e8400-e29b-41d4-a716-446655440011',
    destinationStationId: '550e8400-e29b-41d4-a716-446655440012',
    status: 'ARRIVED',
    trackingNumber: 'LA363-2026-002',
  ),
  DeliveryOrder(
    id: '550e8400-e29b-41d4-a716-446655440003',
    recipientName: 'Le Quoc Bao',
    recipientPhone: '0987654321',
    originStationId: '550e8400-e29b-41d4-a716-446655440012',
    destinationStationId: '550e8400-e29b-41d4-a716-446655440011',
    status: 'DELIVERED',
    trackingNumber: 'LA363-2026-003',
  ),
];

const _samplePackages = [
  PackageModel(
    id: '550e8400-e29b-41d4-a716-446655440021',
    packageName: 'Hộp quà sinh nhật',
    weightGram: 850,
    lengthCm: 24,
    widthCm: 18,
    heightCm: 12,
    isFragile: true,
  ),
  PackageModel(
    id: '550e8400-e29b-41d4-a716-446655440022',
    packageName: 'Tài liệu công ty',
    weightGram: 320,
    lengthCm: 32,
    widthCm: 24,
    heightCm: 2,
    isFragile: false,
  ),
];

const _sampleStations = [
  LandingStation(
    id: '550e8400-e29b-41d4-a716-446655440011',
    stationCode: 'SGN-01',
    name: 'Bưu cục Bến Thành',
    address: 'Quận 1, TP. Hồ Chí Minh',
    status: 'OPERATIONAL',
    latitude: 10.7725,
    longitude: 106.698,
    totalSlots: 20,
    availableSlots: 8,
  ),
  LandingStation(
    id: '550e8400-e29b-41d4-a716-446655440012',
    stationCode: 'SGN-02',
    name: 'Bưu cục Thảo Điền',
    address: 'TP. Thủ Đức, TP. Hồ Chí Minh',
    status: 'OPERATIONAL',
    latitude: 10.801,
    longitude: 106.735,
    totalSlots: 16,
    availableSlots: 5,
  ),
];

const _sampleAddresses = [
  CustomerAddress(
    id: '550e8400-e29b-41d4-a716-446655440031',
    label: 'Nhà riêng',
    contactName: 'Nguyen Minh Anh',
    contactPhone: '0901234567',
    addressLine: '12 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh',
    isDefault: true,
    latitude: 10.7731,
    longitude: 106.703,
  ),
  CustomerAddress(
    id: '550e8400-e29b-41d4-a716-446655440032',
    label: 'Văn phòng',
    contactName: 'Tran Bao Linh',
    contactPhone: '0912345678',
    addressLine: '35 Lê Văn Miến, TP. Thủ Đức, TP. Hồ Chí Minh',
    isDefault: false,
    latitude: 10.802,
    longitude: 106.735,
  ),
];

const _submittedOrder = CreateDeliveryOrderResponseDto(
  id: '550e8400-e29b-41d4-a716-446655440041',
  trackingNumber: 'LA363-2026-004',
  status: 'PENDING',
  pickupSecureCode: '849201',
  recipientName: 'Nguyen Minh Anh',
  recipientPhone: '0901234567',
  originStationId: '550e8400-e29b-41d4-a716-446655440011',
  destinationStationId: '550e8400-e29b-41d4-a716-446655440012',
  package: DeliveryOrderResourceSummary(
    id: '550e8400-e29b-41d4-a716-446655440021',
    name: 'Hộp quà sinh nhật',
  ),
  originStation: DeliveryOrderResourceSummary(
    id: '550e8400-e29b-41d4-a716-446655440011',
    name: 'Bưu cục Bến Thành',
  ),
  destinationStation: DeliveryOrderResourceSummary(
    id: '550e8400-e29b-41d4-a716-446655440012',
    name: 'Bưu cục Thảo Điền',
  ),
  deliveryAddress: DeliveryOrderResourceSummary(
    id: '550e8400-e29b-41d4-a716-446655440031',
    label: 'Nhà riêng',
  ),
  deliveryAddressId: '550e8400-e29b-41d4-a716-446655440031',
);

class _PreviewOrdersNotifier extends MyOrdersNotifier {
  @override
  Future<List<DeliveryOrder>> build() async => _sampleOrders;
}

void main() {
  runApp(
    ProviderScope(
      overrides: [
        myOrdersProvider.overrideWith(_PreviewOrdersNotifier.new),
        unreadNotificationsCountProvider.overrideWith((ref) async => 2),
        orderPackagesProvider.overrideWith((ref) async => _samplePackages),
        orderStationsProvider.overrideWith((ref) async => _sampleStations),
        orderAddressesProvider.overrideWith((ref) async => _sampleAddresses),
      ],
      child: const CustomerPreviewApp(),
    ),
  );
}

class CustomerPreviewApp extends StatefulWidget {
  const CustomerPreviewApp({super.key});

  @override
  State<CustomerPreviewApp> createState() => _CustomerPreviewAppState();
}

class _CustomerPreviewAppState extends State<CustomerPreviewApp> {
  late final GoRouter _router = GoRouter(
    initialLocation: RoutePaths.customerHome,
    routes: [
      ShellRoute(
        builder: (context, state, child) =>
            _CustomerPreviewShell(location: state.uri.path, child: child),
        routes: [
          GoRoute(
            path: RoutePaths.customerHome,
            builder: (context, state) => const CustomerHomePage(),
          ),
          GoRoute(
            path: RoutePaths.customerOrdersCreate,
            builder: (context, state) => const CreateOrderPage(),
          ),
          GoRoute(
            path: RoutePaths.customerOrderSubmitted,
            builder: (context, state) =>
                const OrderSubmittedPage(order: _submittedOrder),
          ),
          GoRoute(
            path: RoutePaths.customerOrders,
            builder: (context, state) => const CustomerHomePage(),
          ),
          GoRoute(
            path: RoutePaths.customerOrdersReview,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Order review'),
          ),
          GoRoute(
            path: RoutePaths.customerAddresses,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Saved addresses'),
          ),
          GoRoute(
            path: RoutePaths.customerPackages,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Saved packages'),
          ),
          GoRoute(
            path: RoutePaths.customerPackagesAdd,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Add package'),
          ),
          GoRoute(
            path: RoutePaths.customerPackagesEdit,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Edit package'),
          ),
          GoRoute(
            path: RoutePaths.customerNotifications,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Notifications'),
          ),
          GoRoute(
            path: RoutePaths.customerAssistant,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'AI Assistant'),
          ),
          GoRoute(
            path: RoutePaths.customerOrderTracking,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Order tracking'),
          ),
          GoRoute(
            path: RoutePaths.customerOrderDetails,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Order details'),
          ),
          GoRoute(
            path: RoutePaths.customerOrderHistory,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Order history'),
          ),
          GoRoute(
            path: RoutePaths.customerOrderConfirmDelivery,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Confirm delivery'),
          ),
          GoRoute(
            path: RoutePaths.profile,
            builder: (context, state) =>
                const _PreviewDestinationPage(title: 'Profile & settings'),
          ),
        ],
      ),
    ],
  );

  @override
  void dispose() {
    _router.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => MaterialApp.router(
    title: 'LA-363 Customer Preview',
    theme: AppTheme.light(AppFlavor.customer),
    routerConfig: _router,
  );
}

class _CustomerPreviewShell extends StatelessWidget {
  const _CustomerPreviewShell({required this.location, required this.child});

  final String location;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final selectedIndex = switch (location) {
      RoutePaths.customerOrdersCreate => 1,
      RoutePaths.customerOrderSubmitted => 2,
      _ => 0,
    };

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: selectedIndex,
        onDestinationSelected: (index) {
          final destination = switch (index) {
            1 => RoutePaths.customerOrdersCreate,
            2 => RoutePaths.customerOrderSubmitted,
            _ => RoutePaths.customerHome,
          };
          context.go(destination);
        },
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Customer Home',
          ),
          NavigationDestination(
            icon: Icon(Icons.add_box_outlined),
            selectedIcon: Icon(Icons.add_box),
            label: 'Create Order',
          ),
          NavigationDestination(
            icon: Icon(Icons.task_alt_outlined),
            selectedIcon: Icon(Icons.task_alt),
            label: 'Order Submitted',
          ),
        ],
      ),
    );
  }
}

class _PreviewDestinationPage extends StatelessWidget {
  const _PreviewDestinationPage({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(title)),
    body: Center(
      child: Text(
        'Preview destination',
        style: Theme.of(context).textTheme.titleMedium,
      ),
    ),
  );
}
