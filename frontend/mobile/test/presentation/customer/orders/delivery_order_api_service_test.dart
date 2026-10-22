import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/update_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';

import '../../../support/fake_http_adapter.dart';

void main() {
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
