import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/update_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';

import '../../../support/fake_http_adapter.dart';

void main() {
  test('sends order filters and pagination as query parameters', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'items': [],
          'page': 2,
          'pageSize': 10,
          'totalCount': 0,
        });
      });
    final service = DeliveryOrderApiService(dio);

    final result = await service.getMyOrders(
      const DeliveryOrderFilterParams(
        page: 2,
        pageSize: 10,
        status: 'IN_TRANSIT',
        fromDate: '2026-10-01T00:00:00Z',
        toDate: '2026-10-23T00:00:00Z',
        searchKeyword: 'TRACK-42',
      ),
    );

    expect(captured?.method, 'GET');
    expect(captured?.path, '/api/delivery-orders');
    expect(captured?.queryParameters, {
      'page': 2,
      'pageSize': 10,
      'status': 'IN_TRANSIT',
      'fromDate': '2026-10-01T00:00:00Z',
      'toDate': '2026-10-23T00:00:00Z',
      'searchKeyword': 'TRACK-42',
    });
    expect(result.page, 2);
    expect(result.pageSize, 10);
  });

  test('maps invalid order status to MSG-P-CO-23', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => jsonBody({'detail': 'Invalid status'}, 400),
      );
    final service = DeliveryOrderApiService(dio);

    await expectLater(
      service.getMyOrders(
        const DeliveryOrderFilterParams(status: 'NOT_A_STATUS'),
      ),
      throwsA(
        isA<ApiException>().having(
          (error) => error.code,
          'code',
          'MSG-P-CO-23',
        ),
      ),
    );
  });

  test('maps invalid pagination to MSG-P-CO-24', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => jsonBody({'detail': 'Invalid page'}, 400),
      );
    final service = DeliveryOrderApiService(dio);

    await expectLater(
      service.getMyOrders(const DeliveryOrderFilterParams(page: 0)),
      throwsA(
        isA<ApiException>().having(
          (error) => error.code,
          'code',
          'MSG-P-CO-24',
        ),
      ),
    );
  });

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
      expect(captured?.path, '/api/delivery-orders/order-1');
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
      expect(captured?.path, '/api/delivery-orders/order-1/cancel');
      expect(captured?.data, {'cancellationReason': 'Changed plans'});
    },
  );
}
