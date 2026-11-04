import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/update_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';

import '../../../support/fake_http_adapter.dart';

void main() {
  test('loads paged orders with filter query parameters', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'items': [
            {
              'id': 'order-1',
              'trackingNumber': 'TRACK-1',
              'recipientName': 'Customer',
              'recipientPhone': '123456789',
              'originStationId': 'origin-1',
              'destinationStationId': 'destination-1',
              'status': 'IN_TRANSIT',
              'pickupSecureCode': 'must-not-be-read',
            },
          ],
          'page': 2,
          'pageSize': 10,
          'totalCount': 11,
          'totalPages': 2,
        });
      });

    final result = await DeliveryOrderApiService(dio).getOrders(
      const DeliveryOrderFilterParams(
        page: 2,
        pageSize: 10,
        status: 'IN_TRANSIT',
        searchKeyword: 'TRACK-1',
      ),
    );

    expect(captured?.method, 'GET');
    expect(captured?.path, '/delivery-orders');
    expect(captured?.queryParameters, {
      'page': 2,
      'pageSize': 10,
      'status': 'IN_TRANSIT',
      'searchKeyword': 'TRACK-1',
    });
    expect(result.items.single.trackingNumber, 'TRACK-1');
    expect(result.page, 2);
    expect(result.pageSize, 10);
    expect(result.totalCount, 11);
    expect(result.totalPages, 2);
  });

  test(
    'loads order detail and parses chronological timeline and ETA',
    () async {
      RequestOptions? captured;
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter((options) {
          captured = options;
          return jsonBody({
            'id': 'order-1',
            'trackingNumber': 'TRACK-1',
            'recipientName': 'Customer',
            'recipientPhone': '123456789',
            'originStationId': 'origin-1',
            'destinationStationId': 'destination-1',
            'status': 'IN_TRANSIT',
            'package': {'id': 'package-1', 'packageName': 'Books'},
            'originStation': {'name': 'Origin Hub'},
            'destinationStation': {'name': 'Destination Hub'},
            'deliveryAddress': {'streetAddress': '1 Main Street'},
            'aiEtaPrediction': {
              'calculatedDistanceKm': 12.5,
              'predictedDurationMinutes': 24,
              'weatherCondition': 'Clear',
              'confidenceScore': 0.94,
              'createdAt': '2026-11-04T08:00:00Z',
            },
            'statusLogs': [
              {
                'fromStatus': 'APPROVED',
                'toStatus': 'IN_TRANSIT',
                'remarks': 'Departed',
                'createdAt': '2026-11-04T08:02:00Z',
              },
              {
                'fromStatus': null,
                'toStatus': 'PENDING',
                'remarks': 'Created',
                'createdAt': '2026-11-04T08:00:00Z',
              },
            ],
            'pickupSecureCode': 'must-not-be-read',
          });
        });

      final detail = await DeliveryOrderApiService(dio)
          .getOrderDetail('order-1');

      expect(captured?.method, 'GET');
      expect(captured?.path, '/delivery-orders/order-1');
      expect(detail.statusLogs.map((log) => log.toStatus), [
        'PENDING',
        'IN_TRANSIT',
      ]);
      expect(detail.package?.packageName, 'Books');
      expect(detail.originStation?.name, 'Origin Hub');
      expect(detail.destinationStation?.name, 'Destination Hub');
      expect(detail.deliveryAddress?.streetAddress, '1 Main Street');
      expect(detail.aiEtaPrediction?.predictedDurationMinutes, 24);
    },
  );

  test('maps detail 404 to ownership-safe order message', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => jsonBody({'title': 'Not Found'}, 404),
      );

    await expectLater(
      DeliveryOrderApiService(dio).getOrderDetail('foreign-order'),
      throwsA(
        isA<ApiException>()
            .having((error) => error.statusCode, 'statusCode', 404)
            .having((error) => error.code, 'code', 'MSG-P-CO-25')
            .having(
              (error) => error.message,
              'message',
              'Order not found or unavailable to your account.',
            ),
      ),
    );
  });

  test(
    'keeps ProblemDetails 400 validation message for invalid date range',
    () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({
            'type': 'about:blank',
            'title': 'Bad Request',
            'detail': 'fromDate must be earlier than or equal to toDate.',
          }, 400),
        );

      await expectLater(
        DeliveryOrderApiService(dio).getOrders(
          const DeliveryOrderFilterParams(
            fromDate: '2026-11-05T00:00:00Z',
            toDate: '2026-11-04T00:00:00Z',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', 400)
              .having(
                (error) => error.message,
                'message',
                'fromDate must be earlier than or equal to toDate.',
              ),
        ),
      );
    },
  );

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
}
