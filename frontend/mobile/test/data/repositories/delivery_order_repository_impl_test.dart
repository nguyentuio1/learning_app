import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/repositories/delivery_order_repository_impl.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/domain/entities/resolution_action.dart';

import '../../support/fake_http_adapter.dart';

void main() {
  test(
    'sends mark-failed and resolve-failure requests with their DTO bodies',
    () async {
      final requests = <RequestOptions>[];
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter((options) {
          requests.add(options);
          return ResponseBody.fromString('', 204);
        });
      final repository = DeliveryOrderRepositoryImpl(
        DeliveryOrderApiService(dio),
      );

      await repository.markFailed(
        id: 'order-1',
        failureReason: 'Weather conditions',
        failureType: 'WEATHER',
      );
      await repository.resolveFailedDelivery(
        id: 'order-1',
        resolutionAction: ResolutionAction.returnToOrigin,
        resolutionNote: 'Return parcel to origin.',
      );

      expect(requests.map((request) => request.path), [
        '/delivery-orders/order-1/fail',
        '/delivery-orders/order-1/resolve-failure',
      ]);
      expect(requests.map((request) => request.method), ['POST', 'POST']);
      expect(requests.map((request) => request.data), [
        {'failureReason': 'Weather conditions', 'failureType': 'WEATHER'},
        {
          'resolutionAction': 'RETURN_TO_ORIGIN',
          'resolutionNote': 'Return parcel to origin.',
        },
      ]);
    },
  );

  for (final (statusCode, code) in [
    (400, 'MSG-P-DW-19'),
    (409, 'MSG-P-DW-20'),
  ]) {
    test('preserves RFC 7807 $statusCode resolution errors', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({
            'type': code,
            'title': statusCode == 409 ? 'Conflict' : 'Bad Request',
            'status': statusCode,
            'detail': 'Resolution request was rejected.',
          }, statusCode),
        );
      final repository = DeliveryOrderRepositoryImpl(
        DeliveryOrderApiService(dio),
      );

      await expectLater(
        repository.resolveFailedDelivery(
          id: 'order-1',
          resolutionAction: ResolutionAction.cancel,
          resolutionNote: 'Cancel the failed delivery.',
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', statusCode)
              .having((error) => error.code, 'code', code)
              .having(
                (error) => error.message,
                'message',
                'Resolution request was rejected.',
              ),
        ),
      );
    });
  }
}
