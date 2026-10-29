import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/data/models/create_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/customer_address.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/address_providers.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/providers/package_providers.dart';
import 'package:smart_drone_delivery/data/services/address_api_service.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/data/services/package_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_delivery_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_submitted_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/review_delivery_order_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _originId = '11111111-1111-1111-1111-111111111111';
const _destinationId = '22222222-2222-2222-2222-222222222222';

class _FakePackageApiService extends PackageApiService {
  _FakePackageApiService() : super(Dio());

  @override
  Future<List<Package>> getPackages() async => const [
    Package(
      id: '33333333-3333-3333-3333-333333333333',
      packageName: 'Small parcel',
      weightGram: 3000,
      lengthCm: 30,
      widthCm: 20,
      heightCm: 15,
      isFragile: false,
    ),
  ];
}

class _FakeAddressApiService extends AddressApiService {
  _FakeAddressApiService() : super(Dio());

  @override
  Future<List<CustomerAddress>> getAddresses() async => const [
    CustomerAddress(
      id: '44444444-4444-4444-4444-444444444444',
      addressLabel: 'Home',
      recipientName: 'Sam Recipient',
      recipientPhone: '123456789',
      streetAddress: '123 Main St',
      isDefault: true,
    ),
  ];
}

class _FakeDeliveryOrderApiService extends DeliveryOrderApiService {
  _FakeDeliveryOrderApiService() : super(Dio());

  CreateDeliveryOrderDto? request;

  @override
  Future<DeliveryOrderDetailResponseDto> createDeliveryOrder(
    CreateDeliveryOrderDto dto,
  ) async {
    request = dto;
    return DeliveryOrderDetailResponseDto.fromJson({
      'id': 'order-1',
      'trackingNumber': 'SDD20261015X8K9',
      'pickupSecureCode': '839201',
      'recipientName': 'Sam Recipient',
      'recipientPhone': '123456789',
      'originStationId': _originId,
      'destinationStationId': _destinationId,
      'status': 'PENDING',
      'statusLogs': [],
    }, includePickupSecureCode: true);
  }
}

Future<void> _pumpCreateFlow(
  WidgetTester tester,
  _FakeDeliveryOrderApiService orderService,
) async {
  final router = GoRouter(
    initialLocation: RoutePaths.customerOrdersCreate,
    routes: [
      GoRoute(
        path: RoutePaths.customerOrdersCreate,
        builder: (_, _) => const CreateDeliveryOrderPage(),
      ),
      GoRoute(
        path: RoutePaths.customerOrdersReview,
        builder: (_, _) => const ReviewDeliveryOrderPage(),
      ),
      GoRoute(
        path: RoutePaths.customerOrderSubmitted,
        builder: (_, _) => const OrderSubmittedPage(),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        packageApiServiceProvider.overrideWithValue(_FakePackageApiService()),
        addressApiServiceProvider.overrideWithValue(_FakeAddressApiService()),
        deliveryOrderApiServiceProvider.overrideWithValue(orderService),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _enterField(WidgetTester tester, String key, String value) async {
  await tester.ensureVisible(find.byKey(Key(key)));
  await tester.enterText(find.byKey(Key(key)), value);
  await tester.pump();
}

void main() {
  testWidgets('C14 form, C16 review, and C17 submitted flow', (tester) async {
    final orderService = _FakeDeliveryOrderApiService();
    await _pumpCreateFlow(tester, orderService);

    expect(find.text('Create delivery order'), findsOneWidget);
    await tester.tap(find.byKey(const Key('create-order-package')));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Small parcel').last);
    await tester.pumpAndSettle();

    await _enterField(tester, 'create-order-origin', _originId);
    await _enterField(tester, 'create-order-destination', _destinationId);
    await tester.tap(find.byKey(const Key('create-order-address')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Home · 123 Main St').last);
    await tester.pumpAndSettle();
    await _enterField(tester, 'create-order-recipient-name', 'Sam Recipient');
    await _enterField(tester, 'create-order-recipient-phone', '123456789');
    await tester.ensureVisible(find.byKey(const Key('create-order-review')));
    await tester.tap(find.byKey(const Key('create-order-review')));
    await tester.pumpAndSettle();

    expect(find.text('Review delivery order'), findsOneWidget);
    expect(find.text('Small parcel'), findsOneWidget);
    expect(find.text(_originId), findsOneWidget);
    expect(find.text('123 Main St'), findsOneWidget);
    expect(find.text('Sam Recipient'), findsOneWidget);
    await tester.tap(find.byKey(const Key('create-order-submit')));
    await tester.pumpAndSettle();

    expect(find.text('Order submitted'), findsOneWidget);
    expect(
      find.text('Order SDD20261015X8K9 submitted successfully.'),
      findsOneWidget,
    );
    expect(find.text('839201'), findsOneWidget);
    expect(
      orderService.request?.packageId,
      '33333333-3333-3333-3333-333333333333',
    );
    expect(
      orderService.request?.deliveryAddressId,
      '44444444-4444-4444-4444-444444444444',
    );
  });

  testWidgets('C15 displays duplicate station validation inline', (
    tester,
  ) async {
    await _pumpCreateFlow(tester, _FakeDeliveryOrderApiService());
    await tester.tap(find.byKey(const Key('create-order-package')));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Small parcel').last);
    await tester.pumpAndSettle();
    await _enterField(tester, 'create-order-origin', _originId);
    await _enterField(tester, 'create-order-destination', _originId);
    await _enterField(tester, 'create-order-recipient-name', 'Sam Recipient');
    await _enterField(tester, 'create-order-recipient-phone', '123456789');
    await tester.ensureVisible(find.byKey(const Key('create-order-review')));
    await tester.tap(find.byKey(const Key('create-order-review')));
    await tester.pumpAndSettle();

    expect(
      find.text('Select different origin and destination stations.'),
      findsOneWidget,
    );
    expect(find.text('Review delivery order'), findsNothing);
  });
}
