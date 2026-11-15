import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/datasources/remote/delivery_map_remote_data_source.dart';
import 'package:smart_drone_delivery/domain/entities/delivery_map_snapshot.dart';

void main() {
  final payload = {
    'orderId': 'order-1',
    'trackingNumber': 'TRACK-1',
    'status': 'IN_TRANSIT',
    'originStation': {
      'id': 'station-1',
      'name': 'Origin Hub',
      'latitude': 10.77,
      'longitude': 106.7,
    },
    'destinationStation': {
      'id': 'station-2',
      'name': 'Destination Hub',
      'latitude': 10.85,
      'longitude': 106.77,
    },
    'estimatedDeliveryTime': '2026-11-15T08:24:00Z',
    'lastPosition': {
      'latitude': 10.8,
      'longitude': 106.73,
      'altitudeMeters': 80.0,
      'speedMps': 11.5,
      'batteryPercentage': 71,
      'timestamp': '2026-11-15T08:10:05Z',
    },
    'telemetryStale': false,
  };

  test(
    'requests the tracking snapshot and parses nullable UTC telemetry',
    () async {
      Uri? requestedUri;
      final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api'))
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (options, handler) {
              requestedUri = options.uri;
              handler.resolve(
                Response(
                  requestOptions: options,
                  data: payload,
                  statusCode: 200,
                ),
              );
            },
          ),
        );
      final source = DeliveryMapRemoteDataSource(dio);

      final snapshot = await source.getMapSnapshot('order-1');

      expect(snapshot.orderId, 'order-1');
      expect(snapshot.status, DeliveryMapOrderStatus.inTransit);
      expect(snapshot.originStation.name, 'Origin Hub');
      expect(snapshot.lastPosition!.batteryPercentage, 71);
      expect(snapshot.lastPosition!.timestamp.isUtc, isTrue);
      expect(snapshot.estimatedDeliveryTime, DateTime.utc(2026, 11, 15, 8, 24));
      expect(snapshot.lastPosition!.latitude, 10.8);
      expect(snapshot.telemetryStale, isFalse);
      expect(requestedUri!.path, '/api/delivery-orders/order-1/tracking');
      dio.close();
    },
  );

  test('preserves a null position and estimated delivery time', () async {
    final emptyTelemetry = {
      ...payload,
      'lastPosition': null,
      'estimatedDeliveryTime': null,
    };
    final dio = Dio()
      ..interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) => handler.resolve(
            Response(requestOptions: options, data: emptyTelemetry),
          ),
        ),
      );

    final snapshot = await DeliveryMapRemoteDataSource(dio)
        .getMapSnapshot('order-1');

    expect(snapshot.lastPosition, isNull);
    expect(snapshot.estimatedDeliveryTime, isNull);
    dio.close();
  });

  for (final statusCode in [400, 401, 403, 404]) {
    test('maps HTTP $statusCode to ApiException', () async {
      final dio = Dio()
        ..interceptors.add(
          InterceptorsWrapper(
            onRequest: (options, handler) => handler.reject(
              DioException(
                requestOptions: options,
                response: Response(
                  requestOptions: options,
                  statusCode: statusCode,
                  data: {'title': 'Request denied'},
                ),
                type: DioExceptionType.badResponse,
              ),
            ),
          ),
        );

      await expectLater(
        DeliveryMapRemoteDataSource(dio).getMapSnapshot('order-1'),
        throwsA(
          isA<ApiException>().having(
            (error) => error.statusCode,
            'statusCode',
            statusCode,
          ),
        ),
      );
      dio.close();
    });
  }
}
