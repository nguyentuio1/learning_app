import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';

const _order = DeliveryOrder(
  id: 'order-1',
  recipientName: 'Sam Recipient',
  recipientPhone: '123456789',
  originStationId: 'origin-1',
  destinationStationId: 'destination-1',
  status: 'PENDING',
);

class _FakeDeliveryOrderApiService extends DeliveryOrderApiService {
  _FakeDeliveryOrderApiService({this.cancelError, this.pendingCancellation})
    : super(Dio());

  final Object? cancelError;
  final Completer<void>? pendingCancellation;
  CancelDeliveryOrderDto? request;

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async =>
      DeliveryOrderDetailResponseDto(
        id: id,
        recipientName: _order.recipientName,
        recipientPhone: _order.recipientPhone,
        originStationId: _order.originStationId,
        destinationStationId: _order.destinationStationId,
        status: _order.status,
        statusLogs: const [],
      );

  @override
  Future<void> cancelOrder(
    String id,
    CancelDeliveryOrderDto cancellation,
  ) async {
    request = cancellation;
    if (pendingCancellation != null) return pendingCancellation!.future;
    if (cancelError != null) throw cancelError!;
  }
}

Future<void> _pumpPage(
  WidgetTester tester,
  _FakeDeliveryOrderApiService service,
) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      child: const MaterialApp(home: OrderDetailPage(order: _order)),
    ),
  );
  await tester.pumpAndSettle();
  await tester.tap(find.text('Cancel order'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('rejects empty and whitespace-only reasons inline', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService();
    await _pumpPage(tester, service);
    await tester.enterText(find.byType(TextFormField), ' \n ');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a cancellation reason.'), findsOneWidget);
    expect(service.request, isNull);
  });

  testWidgets('rejects a reason longer than 500 characters', (tester) async {
    final service = _FakeDeliveryOrderApiService();
    await _pumpPage(tester, service);
    await tester.enterText(find.byType(TextFormField), 'r' * 501);
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(
      find.text('Cancellation reason must be at most 500 characters.'),
      findsOneWidget,
    );
    expect(service.request, isNull);
  });

  testWidgets('disables controls and shows progress while request is pending', (
    tester,
  ) async {
    final pending = Completer<void>();
    final service = _FakeDeliveryOrderApiService(pendingCancellation: pending);
    await _pumpPage(tester, service);
    await tester.enterText(find.byType(TextFormField), 'Changed plans');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pump();

    expect(
      tester.widget<TextFormField>(find.byType(TextFormField)).enabled,
      isFalse,
    );
    final confirmButton = find.byKey(const Key('cancel-order-confirm'));
    final keepOrderButton = find.byKey(const Key('cancel-order-dismiss'));
    expect(tester.widget<FilledButton>(confirmButton).onPressed, isNull);
    expect(tester.widget<TextButton>(keepOrderButton).onPressed, isNull);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    pending.complete();
    await tester.pumpAndSettle();
  });

  testWidgets('shows a 409 conflict message inline in the sheet', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService(
      cancelError: const ApiException(
        message: 'Conflict',
        statusCode: 409,
        code: 'MSG-P-CO-22',
      ),
    );
    await _pumpPage(tester, service);
    await tester.enterText(find.byType(TextFormField), 'Changed plans');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(find.text('This order can no longer be cancelled.'), findsOneWidget);
    expect(find.byKey(const Key('cancel-order-error')), findsOneWidget);
    expect(find.text('Cancel this order?'), findsOneWidget);
  });

  testWidgets('submits a valid reason and closes the sheet on success', (
    tester,
  ) async {
    final service = _FakeDeliveryOrderApiService();
    await _pumpPage(tester, service);
    await tester.enterText(find.byType(TextFormField), '  Changed plans  ');
    await tester.tap(find.text('Confirm cancellation'));
    await tester.pumpAndSettle();

    expect(service.request?.cancellationReason, 'Changed plans');
    expect(find.text('Cancel this order?'), findsNothing);
    expect(find.text('Order cancelled successfully.'), findsOneWidget);
  });
}
