import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_page_response.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';

class _FakeOrdersService extends DeliveryOrderApiService {
  _FakeOrdersService({
    this.detailResponse,
    this.items = const [],
    this.listError,
    this.detailError,
  }) : super(Dio());

  final DeliveryOrderDetailResponseDto? detailResponse;
  final List<DeliveryOrder> items;
  final Object? listError;
  final Object? detailError;
  final requests = <DeliveryOrderFilterParams>[];

  @override
  Future<DeliveryOrderPageResponse> getMyOrders([
    DeliveryOrderFilterParams params = const DeliveryOrderFilterParams(),
  ]) async {
    requests.add(params);
    if (listError != null) throw listError!;
    return DeliveryOrderPageResponse(
      items: items,
      page: params.page,
      pageSize: params.pageSize,
      totalCount: 0,
    );
  }

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async {
    if (detailError != null) throw detailError!;
    return detailResponse!;
  }
}

Future<void> _pumpPage(
  WidgetTester tester,
  Widget page,
  _FakeOrdersService service,
) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp(home: page),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('C05 renders empty state and resets page when filter changes', (
    tester,
  ) async {
    final service = _FakeOrdersService();
    await _pumpPage(tester, const MyOrdersPage(), service);

    expect(find.text('No orders found.'), findsOneWidget);
    await tester.tap(find.widgetWithText(ChoiceChip, 'PENDING'));
    await tester.pumpAndSettle();

    expect(service.requests.last.status, 'PENDING');
    expect(service.requests.last.page, 1);
  });

  testWidgets('C05 renders order rows and updates search while typing', (
    tester,
  ) async {
    final service = _FakeOrdersService(
      items: const [
        DeliveryOrder(
          id: 'order-1',
          recipientName: 'Recipient',
          recipientPhone: '123456789',
          originStationId: 'origin',
          destinationStationId: 'destination',
          status: 'PENDING',
          trackingNumber: 'TRACK-1',
        ),
      ],
    );
    await _pumpPage(tester, const MyOrdersPage(), service);

    expect(find.text('TRACK-1'), findsOneWidget);
    expect(find.text('Recipient · PENDING'), findsOneWidget);
    await tester.enterText(find.byType(TextField), 'TRACK');
    await tester.pumpAndSettle();

    expect(service.requests.last.searchKeyword, 'TRACK');
    expect(service.requests.last.page, 1);
  });

  testWidgets('C05 maps a server error to the orders load message', (
    tester,
  ) async {
    await _pumpPage(
      tester,
      const MyOrdersPage(),
      _FakeOrdersService(
        listError: const ApiException(message: 'Server error', statusCode: 500),
      ),
    );

    expect(
      find.text('Unable to load orders. Please try again.'),
      findsOneWidget,
    );
    expect(find.text('Retry'), findsOneWidget);
  });

  testWidgets('pull-to-refresh reloads the order list', (tester) async {
    final service = _FakeOrdersService();
    await _pumpPage(tester, const MyOrdersPage(), service);
    expect(service.requests.length, 1);

    await tester.drag(find.text('No orders found.'), const Offset(0, 400));
    await tester.pumpAndSettle();

    expect(service.requests.length, greaterThanOrEqualTo(2));
  });

  testWidgets('C10 renders status history chronologically', (tester) async {
    final service = _FakeOrdersService(
      detailResponse: DeliveryOrderDetailResponseDto.fromJson({
        'id': 'order-1',
        'trackingNumber': 'TRACK-1',
        'recipientName': 'Recipient',
        'recipientPhone': '123456789',
        'originStationId': 'origin',
        'destinationStationId': 'destination',
        'status': 'IN_TRANSIT',
        'statusLogs': [
          {
            'fromStatus': 'APPROVED',
            'toStatus': 'IN_TRANSIT',
            'remarks': 'Drone departed',
            'createdAt': '2026-10-22T10:02:00Z',
          },
          {
            'fromStatus': null,
            'toStatus': 'PENDING',
            'remarks': 'Order created',
            'createdAt': '2026-10-22T10:00:00Z',
          },
        ],
      }),
    );
    await _pumpPage(tester, const OrderDetailPage(orderId: 'order-1'), service);

    expect(find.text('Status history'), findsOneWidget);
    expect(find.text('— → PENDING'), findsOneWidget);
    expect(find.text('PENDING → APPROVED'), findsNothing);
    expect(find.text('APPROVED → IN_TRANSIT'), findsOneWidget);
    expect(find.text('Order created'), findsOneWidget);
    expect(find.text('Drone departed'), findsOneWidget);
    expect(
      tester.getTopLeft(find.text('— → PENDING')).dy,
      lessThan(tester.getTopLeft(find.text('APPROVED → IN_TRANSIT')).dy),
    );
  });

  testWidgets('C06 maps a private or missing order to the privacy message', (
    tester,
  ) async {
    await _pumpPage(
      tester,
      const OrderDetailPage(orderId: 'foreign-order'),
      _FakeOrdersService(
        detailError: const ApiException(
          message: 'Not found',
          statusCode: 404,
          code: 'MSG-P-CO-25',
        ),
      ),
    );

    expect(
      find.text('Order not found or unavailable to your account.'),
      findsOneWidget,
    );
  });
}
