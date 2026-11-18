import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/presentation/customer/home/customer_home_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_notifier.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_submitted_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _order1 = DeliveryOrder(
  id: 'order-1',
  recipientName: 'Alex Recipient',
  recipientPhone: '0900000001',
  originStationId: 'origin',
  destinationStationId: 'destination',
  status: 'PENDING',
  trackingNumber: 'DD-1001',
);

const _arrivedOrder = DeliveryOrder(
  id: 'order-2',
  recipientName: 'Jamie Recipient',
  recipientPhone: '0900000002',
  originStationId: 'origin',
  destinationStationId: 'destination',
  status: 'ARRIVED',
  trackingNumber: 'DD-1002',
);

const _recentOrder = DeliveryOrder(
  id: 'order-3',
  recipientName: 'Recent Recipient',
  recipientPhone: '0900000003',
  originStationId: 'origin',
  destinationStationId: 'destination',
  status: 'DELIVERED',
  trackingNumber: 'DD-1003',
);

const _submittedOrder = CreateDeliveryOrderResponseDto(
  id: 'created-order-1',
  trackingNumber: 'DD-2001',
  status: 'PENDING',
  pickupSecureCode: 'ONE-TIME-4821',
  recipientName: 'Alex Recipient',
  recipientPhone: '0900000001',
  originStationId: 'origin',
  destinationStationId: 'destination',
);

class _TestOrdersNotifier extends MyOrdersNotifier {
  _TestOrdersNotifier(this.load);

  final Future<List<DeliveryOrder>> Function() load;

  @override
  Future<List<DeliveryOrder>> build() => load();
}

final _stations = [
  const LandingStation(
    id: 'origin',
    stationCode: 'OR-01',
    name: 'Origin',
    address: 'Origin address',
    status: 'OPERATIONAL',
  ),
  const LandingStation(
    id: 'destination',
    stationCode: 'DS-01',
    name: 'Destination',
    address: 'Destination address',
    status: 'OPERATIONAL',
  ),
];

Widget _homeScope({
  required Future<List<DeliveryOrder>> Function() loadOrders,
  required Widget child,
}) => ProviderScope(
  overrides: [
    myOrdersProvider.overrideWith(() => _TestOrdersNotifier(loadOrders)),
    unreadNotificationsCountProvider.overrideWith((ref) async => 0),
  ],
  child: MaterialApp(home: child),
);

void main() {
  group('CustomerHomePage', () {
    testWidgets('shows primary create action and active/recent order actions', (
      tester,
    ) async {
      await tester.pumpWidget(
        _homeScope(
          loadOrders: () async => [_recentOrder, _arrivedOrder, _order1],
          child: const CustomerHomePage(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Customer Home'), findsOneWidget);
      expect(find.text('Find a station'), findsNothing);
      expect(find.text('Create delivery'), findsOneWidget);
      expect(find.text('DD-1001'), findsOneWidget);
      expect(find.text('Recipient: Alex Recipient'), findsOneWidget);
      expect(find.text('DD-1002'), findsOneWidget);
      expect(find.text('DD-1003'), findsOneWidget);
      expect(find.text('Active orders'), findsOneWidget);
      expect(find.text('Recent orders'), findsOneWidget);
      expect(find.text('Track'), findsNWidgets(3));
      expect(find.text('Confirm receipt'), findsOneWidget);
      expect(
        tester.getTopLeft(find.text('Active orders')).dy,
        lessThan(tester.getTopLeft(find.text('Recent orders')).dy),
      );
    });

    testWidgets('shows a compact loading state', (tester) async {
      final completer = Completer<List<DeliveryOrder>>();
      await tester.pumpWidget(
        _homeScope(
          loadOrders: () => completer.future,
          child: const CustomerHomePage(),
        ),
      );
      await tester.pump();

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      completer.complete([]);
    });

    testWidgets('shows the reusable empty state', (tester) async {
      await tester.pumpWidget(
        _homeScope(loadOrders: () async => [], child: const CustomerHomePage()),
      );
      await tester.pumpAndSettle();

      expect(find.text('No orders yet'), findsOneWidget);
      expect(find.text('Create delivery'), findsNWidgets(2));
    });

    testWidgets('shows a friendly error and retries the order request', (
      tester,
    ) async {
      var attempts = 0;
      await tester.pumpWidget(
        _homeScope(
          loadOrders: () async {
            attempts++;
            if (attempts == 1) throw StateError('offline');
            return [_order1];
          },
          child: const CustomerHomePage(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Retry'), findsOneWidget);
      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();
      expect(attempts, 2);
      expect(find.text('DD-1001'), findsOneWidget);
    });
  });

  testWidgets('empty packages can be added without losing the create draft', (
    tester,
  ) async {
    var packageLoads = 0;
    final router = GoRouter(
      initialLocation: RoutePaths.customerOrdersCreate,
      routes: [
        GoRoute(
          path: RoutePaths.customerOrdersCreate,
          builder: (_, _) => const CreateOrderPage(),
        ),
        GoRoute(
          path: RoutePaths.customerPackagesAdd,
          builder: (context, _) => Scaffold(
            body: Center(
              child: TextButton(
                key: const Key('test-return-from-add-package'),
                onPressed: () => context.pop(true),
                child: const Text('Save package'),
              ),
            ),
          ),
        ),
      ],
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          orderPackagesProvider.overrideWith((ref) async {
            packageLoads++;
            return [];
          }),
          orderStationsProvider.overrideWith((ref) async => _stations),
          orderAddressesProvider.overrideWith((ref) async => []),
        ],
        child: MaterialApp.router(routerConfig: router),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      find.text('No packages available yet. Add a package to continue.'),
      findsOneWidget,
    );
    expect(find.text('Add package'), findsOneWidget);

    await tester.tap(find.byKey(const Key('order-origin')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Origin · OR-01').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('order-destination')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Destination · DS-01').last);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('order-recipient-name')),
      'Draft recipient',
    );
    await tester.enterText(
      find.byKey(const Key('order-recipient-phone')),
      '0901234567',
    );

    await tester.ensureVisible(
      find.byKey(const Key('order-add-package-button')),
    );
    await tester.tap(find.byKey(const Key('order-add-package-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('test-return-from-add-package')));
    await tester.pumpAndSettle();

    expect(
      find.text('No packages available yet. Add a package to continue.'),
      findsOneWidget,
    );
    expect(find.text('Draft recipient'), findsOneWidget);
    expect(find.text('0901234567'), findsOneWidget);
    final container = ProviderScope.containerOf(
      tester.element(find.byType(CreateOrderPage)),
    );
    final draft = container.read(createOrderNotifierProvider);
    expect(draft.originStationId, 'origin');
    expect(draft.destinationStationId, 'destination');
    expect(draft.recipientName, 'Draft recipient');
    expect(draft.recipientPhone, '0901234567');
    expect(packageLoads, greaterThanOrEqualTo(2));
  });

  testWidgets('submitted order displays one-time code and opens its detail', (
    tester,
  ) async {
    final router = GoRouter(
      initialLocation: '/submitted',
      routes: [
        GoRoute(
          path: '/submitted',
          builder: (_, _) => const OrderSubmittedPage(order: _submittedOrder),
        ),
        GoRoute(
          path: RoutePaths.customerOrderDetails,
          builder: (_, state) => Scaffold(
            body: Text('Order detail: ${state.pathParameters['orderId']}'),
          ),
        ),
      ],
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(MaterialApp.router(routerConfig: router));
    await tester.pumpAndSettle();

    expect(find.textContaining('one-time pickup code'), findsOneWidget);
    expect(find.text('ONE-TIME-4821'), findsOneWidget);
    await tester.ensureVisible(
      find.byKey(const Key('order-view-detail-button')),
    );
    await tester.tap(find.byKey(const Key('order-view-detail-button')));
    await tester.pumpAndSettle();
    expect(find.text('Order detail: created-order-1'), findsOneWidget);
    expect(
      router.routeInformationProvider.value.uri.path,
      '/customer/orders/created-order-1',
    );
    expect(
      router.routeInformationProvider.value.uri.toString(),
      isNot(contains(_submittedOrder.pickupSecureCode)),
    );
  });
}
