import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_notifier.dart';

const _originId = '11111111-1111-1111-1111-111111111111';
const _destinationId = '22222222-2222-2222-2222-222222222222';
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
  CreateDeliveryOrderDto? request;

  @override
  Future<CreateDeliveryOrderResponseDto> createOrder(
    CreateDeliveryOrderDto dto,
  ) async {
    request = dto;
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
    });
  }
}

void _fillOrder(ProviderContainer container) {
  container.read(createOrderNotifierProvider.notifier)
    ..selectPackage(_package)
    ..setOriginStationId(_originId)
    ..setDestinationStationId(_destinationId)
    ..setRecipientName('Customer Name')
    ..setRecipientPhone('123456789');
}

void main() {
  late _FakeOrderService service;
  late ProviderContainer container;

  setUp(() {
    service = _FakeOrderService();
    container = ProviderContainer(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      retry: (_, _) => null,
    );
  });

  tearDown(() => container.dispose());

  test('rejects duplicate origin and destination before calling API', () async {
    _fillOrder(container);
    container
        .read(createOrderNotifierProvider.notifier)
        .setDestinationStationId(_originId);

    final result = await container
        .read(createOrderNotifierProvider.notifier)
        .submit();

    expect(result, isNull);
    expect(
      container.read(createOrderNotifierProvider).error?.code,
      'MSG-P-CO-14',
    );
    expect(service.request, isNull);
  });

  test('requires recipient name and phone and checks phone length', () async {
    _fillOrder(container);
    final notifier = container.read(createOrderNotifierProvider.notifier);

    notifier.setRecipientName('  ');
    expect(notifier.validate()?.message, 'Recipient name is required.');

    notifier.setRecipientName('Customer Name');
    notifier.setRecipientPhone('');
    expect(notifier.validate()?.code, 'MSG-API-009');

    notifier.setRecipientPhone('12345678');
    expect(notifier.validate()?.code, 'MSG-API-010');

    notifier.setRecipientPhone('123456789012');
    expect(notifier.validate()?.code, 'MSG-API-010');
  });

  test(
    'serializes only request fields and returns created response details',
    () async {
      _fillOrder(container);

      final result = await container
          .read(createOrderNotifierProvider.notifier)
          .submit();

      expect(result?.status, 'PENDING');
      expect(result?.trackingNumber, 'SDD20261104AB12');
      expect(result?.pickupSecureCode, '839201');
      expect(service.request?.toJson(), {
        'packageId': _package.id,
        'originStationId': _originId,
        'destinationStationId': _destinationId,
        'deliveryAddressId': null,
        'recipientName': 'Customer Name',
        'recipientPhone': '123456789',
      });
    },
  );

  test('maps 409 package conflict to the customer-facing message', () async {
    service.error = const ApiException(message: 'Conflict', statusCode: 409);
    _fillOrder(container);

    await container.read(createOrderNotifierProvider.notifier).submit();

    final error = container.read(createOrderNotifierProvider).error!;
    expect(error.code, 'MSG-P-CO-11');
    expect(error.message, 'This package is already linked to an order.');
  });
}
