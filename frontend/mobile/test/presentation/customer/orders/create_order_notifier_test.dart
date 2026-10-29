import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/create_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_notifier.dart';

import '../../../support/fake_http_adapter.dart';

const _originId = '11111111-1111-1111-1111-111111111111';
const _destinationId = '22222222-2222-2222-2222-222222222222';

class _FakeDeliveryOrderApiService extends DeliveryOrderApiService {
  _FakeDeliveryOrderApiService() : super(Dio());

  Object? error;
  CreateDeliveryOrderDto? request;

  @override
  Future<DeliveryOrderDetailResponseDto> createDeliveryOrder(
    CreateDeliveryOrderDto dto,
  ) async {
    request = dto;
    if (error case final failure?) throw failure;
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

const _package = Package(
  id: 'package-1',
  packageName: 'Small parcel',
  weightGram: 3000,
  lengthCm: 30,
  widthCm: 20,
  heightCm: 15,
  isFragile: false,
);

void _fillRequiredFields(ProviderContainer container) {
  final notifier = container.read(createOrderNotifierProvider.notifier);
  notifier
    ..selectPackage(_package)
    ..setOriginStationId(_originId)
    ..setDestinationStationId(_destinationId)
    ..setRecipientName('Sam Recipient')
    ..setRecipientPhone('123456789');
}

void main() {
  late _FakeDeliveryOrderApiService service;
  late ProviderContainer container;

  setUp(() {
    service = _FakeDeliveryOrderApiService();
    container = ProviderContainer(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      retry: (_, _) => null,
    );
  });

  tearDown(() => container.dispose());

  test('validates duplicate stations with MSG-P-CO-14', () async {
    _fillRequiredFields(container);
    container
        .read(createOrderNotifierProvider.notifier)
        .setDestinationStationId(_originId);

    await container.read(createOrderNotifierProvider.notifier).submit();

    final error = container.read(createOrderNotifierProvider).error!;
    expect(error.code, 'MSG-P-CO-14');
    expect(error.message, 'Select different origin and destination stations.');
    expect(service.request, isNull);
  });

  test('maps missing and invalid recipient phones to API messages', () async {
    _fillRequiredFields(container);
    final notifier = container.read(createOrderNotifierProvider.notifier);

    notifier.setRecipientPhone('');
    await notifier.submit();
    expect(
      container.read(createOrderNotifierProvider).error?.code,
      'MSG-API-009',
    );

    notifier.setRecipientPhone('12345');
    await notifier.submit();
    expect(
      container.read(createOrderNotifierProvider).error?.code,
      'MSG-API-010',
    );
    expect(service.request, isNull);
  });

  test(
    'creates an order and exposes its tracking number and secure code',
    () async {
      _fillRequiredFields(container);

      final result = await container
          .read(createOrderNotifierProvider.notifier)
          .submit();

      expect(result?.trackingNumber, 'SDD20261015X8K9');
      expect(result?.pickupSecureCode, '839201');
      expect(service.request?.packageId, 'package-1');
      expect(service.request?.toJson().keys, [
        'packageId',
        'originStationId',
        'destinationStationId',
        'deliveryAddressId',
        'recipientName',
        'recipientPhone',
      ]);
    },
  );

  for (final (statusCode, code, message) in [
    (400, 'MSG-P-CO-10', "This package exceeds the selected drone's capacity."),
    (409, 'MSG-P-CO-11', 'This package is already linked to an order.'),
    (
      400,
      'MSG-P-CO-15',
      'The selected station is unavailable. Choose another station.',
    ),
  ]) {
    test('maps HTTP $statusCode creation errors to $code', () async {
      service.error = ApiException(
        message: code == 'MSG-P-CO-15' ? 'Station is not operational' : 'Error',
        statusCode: statusCode,
        code: code == 'MSG-P-CO-15' ? null : code,
      );
      _fillRequiredFields(container);

      await container.read(createOrderNotifierProvider.notifier).submit();

      final error = container.read(createOrderNotifierProvider).error!;
      expect(error.code, code);
      expect(error.message, message);
    });
  }

  for (final (statusCode, detail, code, message) in [
    (
      400,
      'Package exceeds drone capacity.',
      'MSG-P-CO-10',
      "This package exceeds the selected drone's capacity.",
    ),
    (
      400,
      'Station is not operational.',
      'MSG-P-CO-15',
      'The selected station is unavailable. Choose another station.',
    ),
    (
      409,
      'Package is already linked to an order.',
      'MSG-P-CO-11',
      'This package is already linked to an order.',
    ),
  ]) {
    test('maps HTTP response $statusCode "$detail" to $code', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({'detail': detail}, statusCode),
        );
      final httpContainer = ProviderContainer(
        overrides: [
          deliveryOrderApiServiceProvider.overrideWithValue(
            DeliveryOrderApiService(dio),
          ),
        ],
        retry: (_, _) => null,
      );
      addTearDown(httpContainer.dispose);
      httpContainer.listen(createOrderNotifierProvider, (_, _) {});
      _fillRequiredFields(httpContainer);

      await httpContainer.read(createOrderNotifierProvider.notifier).submit();

      final error = httpContainer.read(createOrderNotifierProvider).error!;
      expect(error.code, code);
      expect(error.message, message);
      expect(error.statusCode, statusCode);
    });
  }

  test(
    'rejects packages over any drone capacity limit before submission',
    () async {
      _fillRequiredFields(container);
      container
          .read(createOrderNotifierProvider.notifier)
          .selectPackage(
            const Package(
              id: 'oversized',
              packageName: 'Oversized',
              weightGram: 3001,
              lengthCm: 30,
              widthCm: 20,
              heightCm: 15,
              isFragile: false,
            ),
          );

      await container.read(createOrderNotifierProvider.notifier).submit();

      expect(
        container.read(createOrderNotifierProvider).error?.code,
        'MSG-P-CO-10',
      );
      expect(service.request, isNull);
    },
  );
}
