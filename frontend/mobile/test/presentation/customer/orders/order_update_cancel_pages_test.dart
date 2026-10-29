import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/update_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/edit_delivery_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';

class _FakeDeliveryOrderApiService extends DeliveryOrderApiService {
  _FakeDeliveryOrderApiService({this.updateError, this.cancelError})
    : super(Dio());

  Object? updateError;
  Object? cancelError;
  String? cancellationReason;

  @override
  Future<void> updateOrder(String id, UpdateDeliveryOrderDto update) async {
    if (updateError case final error?) throw error;
  }

  @override
  Future<void> cancelOrder(
    String id,
    CancelDeliveryOrderDto cancellation,
  ) async {
    cancellationReason = cancellation.cancellationReason;
    if (cancelError case final error?) throw error;
  }
}

const _order = DeliveryOrder(
  id: 'order-1',
  recipientName: 'Sam Recipient',
  recipientPhone: '123456789',
  originStationId: '11111111-1111-1111-1111-111111111111',
  destinationStationId: '22222222-2222-2222-2222-222222222222',
  status: 'PENDING',
);

Future<void> _pumpOrderPage(
  WidgetTester tester,
  Widget page,
  _FakeDeliveryOrderApiService service,
) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp(
        home: Scaffold(
          body: Navigator(
            onGenerateRoute: (_) =>
                MaterialPageRoute<void>(builder: (_) => page),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('C07 shows Edit Order only for PENDING orders', (tester) async {
    await _pumpOrderPage(
      tester,
      const OrderDetailPage(order: _order),
      _FakeDeliveryOrderApiService(),
    );
    expect(find.text('Edit Order'), findsOneWidget);
  });

  for (final status in [
    'APPROVED',
    'READY_FOR_TAKEOFF',
    'IN_TRANSIT',
    'ARRIVED',
    'DELIVERED',
    'CANCELLED',
    'FAILED',
  ]) {
    testWidgets('C07 hides Edit Order for $status orders', (tester) async {
      final order = DeliveryOrder(
        id: _order.id,
        recipientName: _order.recipientName,
        recipientPhone: _order.recipientPhone,
        originStationId: _order.originStationId,
        destinationStationId: _order.destinationStationId,
        status: status,
      );
      await _pumpOrderPage(
        tester,
        OrderDetailPage(order: order),
        _FakeDeliveryOrderApiService(),
      );

      expect(find.text('Edit Order'), findsNothing);
    });
  }

  testWidgets('C09 validates an empty cancellation reason inline', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService();
    await _pumpOrderPage(tester, const OrderDetailPage(order: _order), service);

    await tester.tap(find.text('Cancel order'));
    await tester.pumpAndSettle();
    expect(find.text('Cancel this order?'), findsOneWidget);
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a cancellation reason.'), findsOneWidget);
    expect(service.cancellationReason, isNull);
  });

  testWidgets('C09 submits a valid reason and confirms cancellation', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService();
    await _pumpOrderPage(tester, const OrderDetailPage(order: _order), service);

    await tester.tap(find.text('Cancel order'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), 'Changed plans');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(service.cancellationReason, 'Changed plans');
    expect(find.text('Order cancelled successfully.'), findsOneWidget);
    expect(find.text('CANCELLED'), findsOneWidget);
    expect(find.text('Cancel order'), findsNothing);
  });

  testWidgets('C09 only offers cancellation for PENDING and APPROVED', (
    tester,
  ) async {
    final approved = DeliveryOrder(
      id: _order.id,
      recipientName: _order.recipientName,
      recipientPhone: _order.recipientPhone,
      originStationId: _order.originStationId,
      destinationStationId: _order.destinationStationId,
      status: 'APPROVED',
    );
    await _pumpOrderPage(
      tester,
      OrderDetailPage(order: approved),
      _FakeDeliveryOrderApiService(),
    );
    expect(find.text('Cancel order'), findsOneWidget);

    for (final status in [
      'READY_FOR_TAKEOFF',
      'IN_TRANSIT',
      'ARRIVED',
      'DELIVERED',
      'CANCELLED',
      'FAILED',
    ]) {
      await tester.pumpWidget(const SizedBox.shrink());
      final blockedOrder = DeliveryOrder(
        id: _order.id,
        recipientName: _order.recipientName,
        recipientPhone: _order.recipientPhone,
        originStationId: _order.originStationId,
        destinationStationId: _order.destinationStationId,
        status: status,
      );
      await _pumpOrderPage(
        tester,
        OrderDetailPage(order: blockedOrder),
        _FakeDeliveryOrderApiService(),
      );
      expect(find.text('Cancel order'), findsNothing, reason: status);
    }
  });

  testWidgets('C09 maps a cancellation 409 to the catalog snackbar', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService(
      cancelError: const ApiException(message: 'Conflict', statusCode: 409),
    );
    await _pumpOrderPage(tester, const OrderDetailPage(order: _order), service);

    await tester.tap(find.text('Cancel order'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), 'No longer needed');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(find.text('This order can no longer be cancelled.'), findsOneWidget);
  });

  testWidgets('C09 maps a cancellation 404 to the catalog snackbar', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService(
      cancelError: const ApiException(message: 'Not found', statusCode: 404),
    );
    await _pumpOrderPage(tester, const OrderDetailPage(order: _order), service);

    await tester.tap(find.text('Cancel order'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField), 'No longer needed');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(
      find.text('Order not found or unavailable to your account.'),
      findsOneWidget,
    );
  });

  testWidgets('C07 maps an update 409 to the catalog snackbar', (tester) async {
    final service = _FakeDeliveryOrderApiService(
      updateError: const ApiException(message: 'Conflict', statusCode: 409),
    );
    await _pumpOrderPage(
      tester,
      const EditDeliveryOrderPage(order: _order),
      service,
    );

    await tester.ensureVisible(find.text('Save changes'));
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();

    expect(find.text('Only pending orders can be edited.'), findsOneWidget);
  });

  testWidgets('C07 shows the API phone validation message', (tester) async {
    await _pumpOrderPage(
      tester,
      const EditDeliveryOrderPage(order: _order),
      _FakeDeliveryOrderApiService(),
    );
    await tester.enterText(find.byType(TextFormField).at(1), '12345');
    await tester.ensureVisible(find.text('Save changes'));
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();

    expect(find.text('Phone number must be 9 to 11 digits.'), findsOneWidget);
  });

  testWidgets('C07 maps an update 400 to the restricted-fields message', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService(
      updateError: const ApiException(message: 'Bad request', statusCode: 400),
    );
    await _pumpOrderPage(
      tester,
      const EditDeliveryOrderPage(order: _order),
      service,
    );
    await tester.ensureVisible(find.text('Save changes'));
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();

    expect(find.text('These order fields cannot be changed.'), findsOneWidget);
  });

  testWidgets('C07 returns saved recipient details to the order page', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService();
    final router = GoRouter(
      initialLocation: '/customer/orders/detail',
      routes: [
        GoRoute(
          path: '/customer/orders/detail',
          builder: (_, _) => const OrderDetailPage(order: _order),
        ),
        GoRoute(
          path: '/customer/orders/edit',
          builder: (_, state) =>
              EditDeliveryOrderPage(order: state.extra! as DeliveryOrder),
        ),
      ],
    );
    addTearDown(router.dispose);
    tester.view.physicalSize = const Size(800, 1800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
        child: MaterialApp.router(routerConfig: router),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Edit Order'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byType(TextFormField).at(0),
      'Updated Recipient',
    );
    await tester.ensureVisible(find.text('Save changes'));
    await tester.tap(find.text('Save changes'));
    await tester.pumpAndSettle();

    expect(find.text('Updated Recipient'), findsOneWidget);
    expect(find.text('Order updated successfully.'), findsOneWidget);
  });
}
