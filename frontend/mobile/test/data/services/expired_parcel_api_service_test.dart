import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/expired_parcel.dart';
import 'package:smart_drone_delivery/data/services/expired_parcel_api_service.dart';

import '../../support/fake_http_adapter.dart';

void main() {
  const stationId = '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44';
  const slotId = 'b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10';

  test('filters timeout failures from the expired-parcels endpoint', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody([
          {
            'slotId': slotId,
            'slotNumber': 2,
            'slotStatus': 'OCCUPIED',
            'orderStatus': 'FAILED',
            'trackingNumber': 'DD-202611-0001',
            'cancelReason': ExpiredParcel.pickupTimeoutReason,
            'arrivedAt': '2026-11-14T03:00:00Z',
            'elapsedDuration': '1.00:15:00',
          },
          {
            'id': 'another-slot',
            'slotNumber': 3,
            'status': 'OCCUPIED',
            'orderStatus': 'FAILED',
            'cancelReason': 'Customer cancellation',
          },
          {
            'id': 'empty-slot',
            'slotNumber': 4,
            'status': 'EMPTY',
            'orderStatus': 'FAILED',
            'cancelReason': ExpiredParcel.pickupTimeoutReason,
          },
          {'id': 'legacy-slot', 'slotNumber': 5, 'status': 'OCCUPIED'},
        ]);
      });

    final parcels = await ExpiredParcelApiService(dio)
        .getExpiredParcels(stationId);

    expect(captured?.method, 'GET');
    expect(
      captured?.path,
      '/landing-stations/$stationId/operations/expired-parcels',
    );
    expect(parcels, hasLength(1));
    expect(parcels.single.slotId, slotId);
    expect(parcels.single.slotStatus, 'OCCUPIED');
    expect(
      parcels.single.elapsedDuration,
      const Duration(days: 1, minutes: 15),
    );

    final alert = PickupTimeoutAlert.fromExpiredParcel(parcels.single);
    expect(alert.slotId, slotId);
    expect(alert.trackingNumber, 'DD-202611-0001');
    expect(alert.title, 'Pickup window expired');
  });

  test('posts validated clear request to station operations', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'orderId': 'c3b9a792-6e8d-4c12-8d37-2848f8464be2',
          'trackingNumber': 'DD-202611-0001',
          'slotId': slotId,
          'slotStatus': 'EMPTY',
          'clearedAt': '2026-11-15T04:30:00Z',
        });
      });

    final result = await ExpiredParcelApiService(dio).clearExpiredParcel(
      stationId: stationId,
      request: ClearExpiredParcelRequest(
        slotId: slotId,
        reason: 'Parcel removed by station operator.',
      ),
    );

    expect(captured?.method, 'POST');
    expect(
      captured?.path,
      '/landing-stations/$stationId/operations/clear-expired-parcel',
    );
    expect(captured?.data, {
      'slotId': slotId,
      'reason': 'Parcel removed by station operator.',
    });
    expect(result.orderId, 'c3b9a792-6e8d-4c12-8d37-2848f8464be2');
    expect(result.slotStatus, 'EMPTY');
    expect(result.clearedAt, DateTime.utc(2026, 11, 15, 4, 30));
  });

  test('rejects invalid clear request values', () {
    expect(
      () => ClearExpiredParcelRequest(
        slotId: 'not-a-uuid',
        reason: 'valid reason',
      ),
      throwsArgumentError,
    );
    expect(
      () => ClearExpiredParcelRequest(slotId: slotId, reason: 'no'),
      throwsArgumentError,
    );
  });

  for (final (statusCode, title, expectedMessage) in [
    (
      403,
      'Forbidden',
      'You are not authorized to clear parcels at this station.',
    ),
    (
      409,
      'Conflict',
      'The parcel is no longer eligible to be cleared because its order or slot state changed.',
    ),
  ]) {
    test('maps RFC 7807 HTTP $statusCode to a clear-specific error', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({
            'type': 'about:blank',
            'title': title,
            'status': statusCode,
          }, statusCode),
        );

      await expectLater(
        ExpiredParcelApiService(dio).clearExpiredParcel(
          stationId: stationId,
          request: ClearExpiredParcelRequest(
            slotId: slotId,
            reason: 'Parcel removed by station operator.',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', statusCode)
              .having((error) => error.message, 'message', expectedMessage),
        ),
      );
    });
  }

  test(
    'includes RFC 7807 detail with clear-specific conflict message',
    () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({
            'type': 'about:blank',
            'title': 'Conflict',
            'detail': 'The slot is already EMPTY.',
          }, 409),
        );

      await expectLater(
        ExpiredParcelApiService(dio).clearExpiredParcel(
          stationId: stationId,
          request: ClearExpiredParcelRequest(
            slotId: slotId,
            reason: 'Parcel removed by station operator.',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', 409)
              .having(
                (error) => error.message,
                'message',
                contains('The slot is already EMPTY.'),
              ),
        ),
      );
    },
  );
}
