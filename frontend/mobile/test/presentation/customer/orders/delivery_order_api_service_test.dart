import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/update_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/data/services/landing_station_api_service.dart';

import '../../../support/fake_http_adapter.dart';

void main() {
  test(
    'posts only allowed create-order fields and parses the one-time code',
    () async {
      RequestOptions? captured;
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter((options) {
          captured = options;
          return jsonBody({
            'id': 'order-1',
            'trackingNumber': 'SDD20261104AB12',
            'status': 'PENDING',
            'pickupSecureCode': '839201',
            'recipientName': 'Customer Name',
            'recipientPhone': '123456789',
            'originStationId': '11111111-1111-1111-1111-111111111111',
            'destinationStationId': '22222222-2222-2222-2222-222222222222',
            'package': {'id': 'package-1', 'packageName': 'Parcel'},
            'originStation': {
              'id': '11111111-1111-1111-1111-111111111111',
              'name': 'Origin',
            },
            'destinationStation': {
              'id': '22222222-2222-2222-2222-222222222222',
              'name': 'Destination',
            },
            'deliveryAddress': null,
          }, 201);
        });
      final service = DeliveryOrderApiService(dio);

      final result = await service.createOrder(
        const CreateDeliveryOrderDto(
          packageId: 'package-1',
          originStationId: '11111111-1111-1111-1111-111111111111',
          destinationStationId: '22222222-2222-2222-2222-222222222222',
          recipientName: 'Customer Name',
          recipientPhone: '123456789',
        ),
      );

      expect(captured?.method, 'POST');
      expect(captured?.path, '/delivery-orders');
      expect(captured?.data, {
        'packageId': 'package-1',
        'originStationId': '11111111-1111-1111-1111-111111111111',
        'destinationStationId': '22222222-2222-2222-2222-222222222222',
        'deliveryAddressId': null,
        'recipientName': 'Customer Name',
        'recipientPhone': '123456789',
      });
      expect(captured?.data.keys, isNot(contains('customerId')));
      expect(captured?.data.keys, isNot(contains('status')));
      expect(captured?.data.keys, isNot(contains('trackingNumber')));
      expect(captured?.data.keys, isNot(contains('pickupSecureCode')));
      expect(result.status, 'PENDING');
      expect(result.trackingNumber, 'SDD20261104AB12');
      expect(result.pickupSecureCode, '839201');
      expect(result.package?.name, 'Parcel');
      expect(result.originStation?.name, 'Origin');
      expect(result.destinationStation?.name, 'Destination');
    },
  );

  test('loads operational stations for customer station selection', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody([
          {
            'id': '11111111-1111-1111-1111-111111111111',
            'stationCode': 'OR-01',
            'name': 'Origin',
            'address': 'Origin address',
            'latitude': 10.0,
            'longitude': 106.0,
            'status': 'OPERATIONAL',
          },
          {
            'id': '33333333-3333-3333-3333-333333333333',
            'stationCode': 'CL-01',
            'name': 'Closed Hub',
            'address': 'Closed address',
            'status': 'MAINTENANCE',
          },
        ]);
      });

    final stations = await LandingStationApiService(dio)
        .getOperationalStations();

    expect(captured?.method, 'GET');
    expect(captured?.path, '/landing-stations');
    expect(captured?.queryParameters, {'status': 'OPERATIONAL'});
    expect(stations, hasLength(1));
    expect(stations.single, isA<LandingStation>());
    expect(stations.single.name, 'Origin');
    expect(stations.single.stationCode, 'OR-01');
  });

  test('maps RFC 7807 409 to package-already-linked message', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => jsonBody({'type': 'about:blank', 'detail': 'Conflict'}, 409),
      );

    await expectLater(
      DeliveryOrderApiService(dio).createOrder(
        const CreateDeliveryOrderDto(
          packageId: 'package-1',
          originStationId: '11111111-1111-1111-1111-111111111111',
          destinationStationId: '22222222-2222-2222-2222-222222222222',
          recipientName: 'Customer Name',
          recipientPhone: '123456789',
        ),
      ),
      throwsA(
        isA<ApiException>()
            .having((error) => error.statusCode, 'statusCode', 409)
            .having((error) => error.code, 'code', 'MSG-P-CO-11'),
      ),
    );
  });

  for (final (statusCode, detail) in [
    (400, 'The selected station is unavailable.'),
    (404, 'Package or station not found.'),
  ]) {
    test('preserves RFC 7807 detail for HTTP $statusCode', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) =>
              jsonBody({'type': 'about:blank', 'detail': detail}, statusCode),
        );

      await expectLater(
        DeliveryOrderApiService(dio).createOrder(
          const CreateDeliveryOrderDto(
            packageId: 'package-1',
            originStationId: '11111111-1111-1111-1111-111111111111',
            destinationStationId: '22222222-2222-2222-2222-222222222222',
            recipientName: 'Customer Name',
            recipientPhone: '123456789',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', statusCode)
              .having((error) => error.message, 'message', detail),
        ),
      );
    });
  }

  test(
    'sends update to the order route with only allowed JSON fields',
    () async {
      RequestOptions? captured;
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter((options) {
          captured = options;
          return jsonBody({});
        });
      final service = DeliveryOrderApiService(dio);

      await service.updateOrder(
        'order-1',
        const UpdateDeliveryOrderDto(
          recipientName: 'Sam Recipient',
          recipientPhone: '123456789',
          destinationStationId: '22222222-2222-2222-2222-222222222222',
        ),
      );

      expect(captured?.method, 'PUT');
      expect(captured?.path, '/delivery-orders/order-1');
      expect(captured?.data, {
        'recipientName': 'Sam Recipient',
        'recipientPhone': '123456789',
        'destinationStationId': '22222222-2222-2222-2222-222222222222',
        'deliveryAddressId': null,
      });
    },
  );

  test(
    'posts cancellation reason and reads RFC 7807 conflict details',
    () async {
      RequestOptions? captured;
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter((options) {
          captured = options;
          return jsonBody({
            'type': 'about:blank',
            'title': 'Conflict',
            'detail': 'State changed',
          }, 409);
        });
      final service = DeliveryOrderApiService(dio);

      await expectLater(
        service.cancelOrder(
          'order-1',
          const CancelDeliveryOrderDto(cancellationReason: 'Changed plans'),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', 409)
              .having((error) => error.message, 'message', 'State changed'),
        ),
      );

      expect(captured?.method, 'POST');
      expect(captured?.path, '/delivery-orders/order-1/cancel');
      expect(captured?.data, {'cancellationReason': 'Changed plans'});
    },
  );

  for (final status in [400, 404, 409]) {
    test('maps update HTTP $status to ApiException', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) =>
              jsonBody({'title': 'Problem', 'detail': 'Update failed'}, status),
        );
      final service = DeliveryOrderApiService(dio);

      await expectLater(
        service.updateOrder(
          'order-1',
          const UpdateDeliveryOrderDto(
            recipientName: 'Sam Recipient',
            recipientPhone: '123456789',
            destinationStationId: '22222222-2222-2222-2222-222222222222',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', status)
              .having((error) => error.message, 'message', 'Update failed'),
        ),
      );
    });

    test('maps cancellation HTTP $status to ApiException', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) =>
              jsonBody({'title': 'Problem', 'detail': 'Cancel failed'}, status),
        );
      final service = DeliveryOrderApiService(dio);

      await expectLater(
        service.cancelOrder(
          'order-1',
          const CancelDeliveryOrderDto(cancellationReason: 'Changed plans'),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', status)
              .having((error) => error.message, 'message', 'Cancel failed'),
        ),
      );
    });
  }
}
