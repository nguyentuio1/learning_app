import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_submitted_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/review_order_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _originId = '11111111-1111-1111-1111-111111111111';
const _destinationId = '22222222-2222-2222-2222-222222222222';
const _stations = [
  LandingStation(
    id: _originId,
    stationCode: 'OR-01',
    name: 'Origin',
    address: 'Origin address',
    status: 'OPERATIONAL',
  ),
  LandingStation(
    id: _destinationId,
    stationCode: 'DS-01',
    name: 'Destination',
    address: 'Destination address',
    status: 'OPERATIONAL',
  ),
];
const _package = Package(
  id: '33333333-3333-3333-3333-333333333333',
  packageName: 'Parcel',
  weightGram: 2000,
  lengthCm: 20,
  widthCm: 10,
  heightCm: 8,
  isFragile: false,
);

class _FakeOrderService extends DeliveryOrderApiService {
  _FakeOrderService() : super(Dio());

  Object? error;

  @override
  Future<CreateDeliveryOrderResponseDto> createOrder(
    CreateDeliveryOrderDto dto,
  ) async {
    if (error case final failure?) throw failure;
    return CreateDeliveryOrderResponseDto.fromJson({
      'id': 'order-1',
      'trackingNumber': 'SDD20261104AB12',
      'status': 'PENDING',
      'pickupSecureCode': '839201',
      'recipientName': 'Customer Name',
      'recipientPhone': '123456789',
      'originStationId': _originId,
      'destinationStationId': _destinationId,
      'package': {'id': _package.id, 'packageName': _package.packageName},
      'originStation': {'id': _originId, 'name': 'Origin'},
      'destinationStation': {'id': _destinationId, 'name': 'Destination'},
    });
  }
}

Future<void> _pumpFlow(
  WidgetTester tester, {
  required _FakeOrderService service,
}) async {
  final router = GoRouter(
    initialLocation: RoutePaths.customerOrdersCreate,
    routes: [
      GoRoute(
        path: RoutePaths.customerOrdersCreate,
        builder: (_, _) => const CreateOrderPage(),
      ),
      GoRoute(
        path: RoutePaths.customerOrdersReview,
        builder: (_, _) => const ReviewOrderPage(),
      ),
      GoRoute(
        path: RoutePaths.customerOrderSubmitted,
        builder: (_, state) => OrderSubmittedPage(
          order: state.extra! as CreateDeliveryOrderResponseDto,
        ),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        deliveryOrderApiServiceProvider.overrideWithValue(service),
        orderPackagesProvider.overrideWith((ref) async => [_package]),
        orderStationsProvider.overrideWith((ref) async => _stations),
        orderAddressesProvider.overrideWith((ref) async => []),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _enter(WidgetTester tester, String key, String value) async {
  await tester.ensureVisible(find.byKey(Key(key)));
  await tester.enterText(find.byKey(Key(key)), value);
  await tester.pump();
}

Future<void> _completeForm(WidgetTester tester, {String? phone}) async {
  await tester.tap(find.byKey(const Key('order-package')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Parcel').last);
  await tester.pumpAndSettle();
  await _selectStation(tester, 'order-origin', 'Origin · OR-01');
  await _selectStation(tester, 'order-destination', 'Destination · DS-01');
  await _enter(tester, 'order-recipient-name', 'Customer Name');
  await _enter(tester, 'order-recipient-phone', phone ?? '123456789');
  await tester.ensureVisible(find.byKey(const Key('order-review')));
  await tester.tap(find.byKey(const Key('order-review')));
  await tester.pumpAndSettle();
}

Future<void> _selectStation(
  WidgetTester tester,
  String key,
  String option,
) async {
  await tester.tap(find.byKey(Key(key)));
  await tester.pumpAndSettle();
  await tester.tap(find.text(option).last);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('C14 to C16 to C17 displays status, tracking and one-time code', (
    tester,
  ) async {
    await _pumpFlow(tester, service: _FakeOrderService());
    await _completeForm(tester);

    expect(find.text('Review order'), findsOneWidget);
    await tester.tap(find.byKey(const Key('order-submit')));
    await tester.pumpAndSettle();

    expect(find.text('PENDING'), findsOneWidget);
    expect(find.text('SDD20261104AB12'), findsOneWidget);
    expect(find.text('839201'), findsOneWidget);
  });

  testWidgets('shows inline validation when recipient phone is invalid', (
    tester,
  ) async {
    await _pumpFlow(tester, service: _FakeOrderService());
    await _completeForm(tester, phone: '12345678');

    expect(find.text('Phone number must be 9 to 11 digits.'), findsOneWidget);
    expect(find.byKey(const Key('order-submit')), findsNothing);
  });

  testWidgets('offers named stations instead of requiring station UUIDs', (
    tester,
  ) async {
    await _pumpFlow(tester, service: _FakeOrderService());

    await tester.tap(find.byKey(const Key('order-origin')));
    await tester.pumpAndSettle();

    expect(find.text('Origin · OR-01'), findsOneWidget);
    expect(find.text(_originId), findsNothing);
  });

  testWidgets('shows the 409 package conflict on review page', (tester) async {
    final service = _FakeOrderService()
      ..error = const ApiException(
        message: 'This package is already linked to an order.',
        statusCode: 409,
        code: 'MSG-P-CO-11',
      );
    await _pumpFlow(tester, service: service);
    await _completeForm(tester);
    await tester.tap(find.byKey(const Key('order-submit')));
    await tester.pumpAndSettle();

    expect(
      find.text('This package is already linked to an order.'),
      findsOneWidget,
    );
  });
}
