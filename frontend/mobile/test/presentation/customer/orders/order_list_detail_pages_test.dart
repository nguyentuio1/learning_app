import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/paged_result.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _order = DeliveryOrder(
  id: 'order-1',
  recipientName: 'Customer',
  recipientPhone: '123456789',
  originStationId: 'origin-1',
  destinationStationId: 'destination-1',
  status: 'IN_TRANSIT',
  trackingNumber: 'TRACK-1',
);

final _detail = DeliveryOrderDetailResponseDto.fromJson({
  'id': 'order-1',
  'trackingNumber': 'TRACK-1',
  'recipientName': 'Customer',
  'recipientPhone': '123456789',
  'originStationId': 'origin-1',
  'destinationStationId': 'destination-1',
  'status': 'IN_TRANSIT',
  'package': {'packageName': 'Books', 'weightGram': 450},
  'originStation': {'name': 'Origin Hub'},
  'destinationStation': {'name': 'Destination Hub'},
  'deliveryAddress': {'streetAddress': '1 Main Street'},
  'aiEtaPrediction': {'predictedDurationMinutes': 24},
  'statusLogs': [
    {
      'fromStatus': 'APPROVED',
      'toStatus': 'IN_TRANSIT',
      'remarks': 'Drone departed',
      'createdAt': '2026-11-04T08:02:00Z',
    },
    {
      'fromStatus': null,
      'toStatus': 'PENDING',
      'remarks': 'Order created',
      'createdAt': '2026-11-04T08:00:00Z',
    },
  ],
});

class _FakeOrderService extends DeliveryOrderApiService {
  _FakeOrderService({this.orders = const [_order]}) : super(Dio());

  final List<DeliveryOrder> orders;
  final requests = <DeliveryOrderFilterParams>[];

  @override
  Future<PagedResult<DeliveryOrder>> getOrders(
    DeliveryOrderFilterParams params,
  ) async {
    requests.add(params);
    return PagedResult(
      items: orders,
      page: params.page,
      pageSize: params.pageSize,
      totalCount: orders.length,
      totalPages: orders.isEmpty ? 0 : 1,
    );
  }

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async =>
      _detail;
}

Future<void> _pumpOrders(WidgetTester tester, _FakeOrderService service) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final router = GoRouter(
    initialLocation: RoutePaths.customerOrders,
    routes: [
      GoRoute(
        path: RoutePaths.customerOrders,
        builder: (_, _) => const MyOrdersPage(),
      ),
      GoRoute(
        path: RoutePaths.customerOrderDetails,
        builder: (_, state) => OrderDetailPage(
          orderId: state.pathParameters['orderId'],
          order: state.extra as DeliveryOrder?,
        ),
      ),
    ],
  );
  addTearDown(router.dispose);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('renders orders, changes status filter, and refreshes', (
    tester,
  ) async {
    final service = _FakeOrderService();
    await _pumpOrders(tester, service);
    expect(find.text('TRACK-1'), findsOneWidget);

    await tester.tap(find.widgetWithText(ChoiceChip, 'IN TRANSIT'));
    await tester.pumpAndSettle();
    expect(service.requests.last.status, 'IN_TRANSIT');
    expect(service.requests.last.page, 1);

    final listView = find.byType(ListView).last;
    await tester.drag(listView, const Offset(0, 500));
    await tester.pumpAndSettle();
    expect(service.requests.length, greaterThanOrEqualTo(3));
  });

  testWidgets('shows empty state for an empty page', (tester) async {
    await _pumpOrders(tester, _FakeOrderService(orders: const []));
    expect(find.text('No orders found.'), findsOneWidget);
  });

  testWidgets('selecting a date range reloads orders with ISO dates', (
    tester,
  ) async {
    final service = _FakeOrderService();
    await _pumpOrders(tester, service);
    await tester.tap(find.byKey(const Key('order-date-range-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Switch to input'));
    await tester.pumpAndSettle();

    final today = DateTime.now();
    final fromDate = DateTime(today.year, today.month, today.day - 2);
    final toDate = DateTime(today.year, today.month, today.day - 1);
    final startText =
        '${fromDate.month.toString().padLeft(2, '0')}/'
        '${fromDate.day.toString().padLeft(2, '0')}/${fromDate.year}';
    final endText =
        '${toDate.month.toString().padLeft(2, '0')}/'
        '${toDate.day.toString().padLeft(2, '0')}/${toDate.year}';
    await tester.enterText(
      find.byWidgetPredicate(
        (widget) =>
            widget is TextField && widget.decoration?.labelText == 'Start Date',
      ),
      startText,
    );
    await tester.enterText(
      find.byWidgetPredicate(
        (widget) =>
            widget is TextField && widget.decoration?.labelText == 'End Date',
      ),
      endText,
    );
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();

    expect(service.requests.last.fromDate, fromDate.toIso8601String());
    expect(service.requests.last.toDate, toDate.toIso8601String());
    expect(
      find.text(
        '${fromDate.day.toString().padLeft(2, '0')}/'
        '${fromDate.month.toString().padLeft(2, '0')}/${fromDate.year} - '
        '${toDate.day.toString().padLeft(2, '0')}/'
        '${toDate.month.toString().padLeft(2, '0')}/${toDate.year}',
      ),
      findsOneWidget,
    );
    await tester.tap(find.byTooltip('Clear date range'));
    await tester.pumpAndSettle();
    expect(service.requests.last.fromDate, isNull);
    expect(service.requests.last.toDate, isNull);
  });

  testWidgets('opens detail and renders ordered status history', (
    tester,
  ) async {
    await _pumpOrders(tester, _FakeOrderService());
    await tester.tap(find.text('TRACK-1'));
    await tester.pumpAndSettle();

    expect(find.text('Books'), findsOneWidget);
    expect(find.text('Origin Hub'), findsOneWidget);
    expect(find.text('Destination Hub'), findsOneWidget);
    expect(find.text('1 Main Street'), findsOneWidget);
    expect(find.text('24 minutes'), findsOneWidget);
    expect(find.text('— → PENDING'), findsOneWidget);
    expect(find.text('APPROVED → IN_TRANSIT'), findsOneWidget);
    expect(find.text('Order created'), findsOneWidget);
    expect(find.text('Drone departed'), findsOneWidget);
    expect(
      tester.getTopLeft(find.text('— → PENDING')).dy,
      lessThan(tester.getTopLeft(find.text('APPROVED → IN_TRANSIT')).dy),
    );
  });
}
