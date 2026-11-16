import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/datasources/remote/tracking_remote_data_source.dart';

import '../../support/fake_http_adapter.dart';

Map<String, dynamic> _snapshotJson() => {
  'orderId': 'order-1',
  'trackingNumber': 'DD-202611-0012',
  'status': 'IN_TRANSIT',
  'originStation': {
    'id': 'origin-1',
    'name': 'District 1 Hub',
    'latitude': 10.7769,
    'longitude': 106.7009,
  },
  'destinationStation': {
    'id': 'destination-1',
    'name': 'Thu Duc Hub',
    'latitude': 10.8506,
    'longitude': 106.7719,
  },
  'estimatedDeliveryTime': null,
  'lastPosition': null,
  'telemetryStale': false,
};

void main() {
  test(
    'requests the tracking endpoint and parses nullable telemetry fields',
    () async {
      final dio = Dio(BaseOptions(baseUrl: 'https://api.example.test/api'))
        ..httpClientAdapter = FakeHttpAdapter((request) {
          expect(request.path, '/delivery-orders/order-1/tracking');
          return jsonBody(_snapshotJson());
        });

      final result = await TrackingRemoteDataSource(dio).getSnapshot('order-1');

      expect(result.orderId, 'order-1');
      expect(result.lastPosition, isNull);
      expect(result.estimatedDeliveryTime, isNull);
      expect(result.status.apiValue, 'IN_TRANSIT');
    },
  );

  for (final statusCode in [401, 403, 404]) {
    test('maps HTTP $statusCode to ApiException', () async {
      final dio = Dio(BaseOptions(baseUrl: 'https://api.example.test/api'))
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({'title': 'Request failed'}, statusCode),
        );

      await expectLater(
        TrackingRemoteDataSource(dio).getSnapshot('order-1'),
        throwsA(
          isA<ApiException>().having(
            (error) => error.statusCode,
            'statusCode',
            statusCode,
          ),
        ),
      );
    });
  }

  test('a failed snapshot request can be retried successfully', () async {
    var requestCount = 0;
    final dio = Dio(BaseOptions(baseUrl: 'https://api.example.test/api'))
      ..httpClientAdapter = FakeHttpAdapter((_) {
        requestCount++;
        return requestCount == 1
            ? jsonBody({'title': 'Not found'}, 404)
            : jsonBody(_snapshotJson());
      });
    final source = TrackingRemoteDataSource(dio);

    await expectLater(
      source.getSnapshot('order-1'),
      throwsA(isA<ApiException>()),
    );
    expect(
      (await source.getSnapshot('order-1')).trackingNumber,
      'DD-202611-0012',
    );
    expect(requestCount, 2);
  });
}
