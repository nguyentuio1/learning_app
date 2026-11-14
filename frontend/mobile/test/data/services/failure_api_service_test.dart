import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/failure_models.dart';
import 'package:smart_drone_delivery/data/services/failure_api_service.dart';

import '../../support/fake_http_adapter.dart';

void main() {
  test('loads failed orders with contract query parameter names', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'items': [],
          'page': 2,
          'pageSize': 10,
          'totalCount': 11,
          'totalPages': 2,
        });
      });

    final result = await FailureApiService(dio).getFailedOrders(
      const FailureOrderQuery(
        page: 2,
        pageSize: 10,
        failureType: 'HARDWARE_FAULT',
        resolutionStatus: 'UNRESOLVED',
        searchKeyword: 'TRACK-1',
        fromDate: '2026-11-01T00:00:00.000',
        toDate: '2026-11-10T23:59:59.000',
      ),
    );

    expect(captured?.method, 'GET');
    expect(captured?.path, '/dispatch/failed-orders');
    expect(captured?.queryParameters, {
      'page': 2,
      'pageSize': 10,
      'failureType': 'HARDWARE_FAULT',
      'resolutionStatus': 'UNRESOLVED',
      'searchKeyword': 'TRACK-1',
      'fromDate': '2026-11-01T00:00:00.000',
      'toDate': '2026-11-10T23:59:59.000',
    });
    expect(result.page, 2);
    expect(result.totalCount, 11);
  });

  test('loads failure detail and parses both histories', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'orderId': 'order-1',
          'trackingNumber': 'TRACK-1',
          'status': 'FAILED',
          'latestFailure': {
            'failureLogId': 4,
            'failureType': 'HARDWARE_FAULT',
            'failureReason': 'Motor warning',
            'failedAt': '2026-11-10T12:00:00Z',
            'resolutionStatus': 'UNRESOLVED',
          },
          'returnToOriginStatus': 'NOT_REQUESTED',
          'parcelLocation': {'stationId': 'station-1', 'slotId': 'slot-1'},
          'failureHistory': [
            {
              'failureLogId': 4,
              'failureType': 'HARDWARE_FAULT',
              'failureReason': 'Motor warning',
              'failedAt': '2026-11-10T12:00:00Z',
              'resolutionStatus': 'UNRESOLVED',
            },
          ],
          'resolutionHistory': [
            {
              'failureLogId': 4,
              'action': 'RETURN_TO_ORIGIN',
              'note': 'Bring to origin hub',
              'performedBy': 'dispatcher-1',
              'createdAt': '2026-11-10T12:10:00Z',
            },
          ],
          'resolutionHistoryComplete': true,
          'statusLogs': [],
        });
      });

    final detail = await FailureApiService(dio).getFailureDetail('order-1');

    expect(captured?.method, 'GET');
    expect(captured?.path, '/dispatch/failed-orders/order-1');
    expect(detail.latestFailure.failureReason, 'Motor warning');
    expect(detail.parcelLocation?.stationId, 'station-1');
    expect(detail.failureHistory, hasLength(1));
    expect(detail.resolutionHistory.single.note, 'Bring to origin hub');
  });

  test('posts exact report-failure contract', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'orderId': 'order-1',
          'trackingNumber': 'TRACK-1',
          'status': 'FAILED',
          'updatedAt': '2026-11-10T12:00:00Z',
        });
      });

    await FailureApiService(dio).reportFailure(
      'order-1',
      'EMERGENCY_LANDING',
      'Emergency landing at station',
    );

    expect(captured?.method, 'POST');
    expect(captured?.path, '/delivery-orders/order-1/fail');
    expect(captured?.data, {
      'failureType': 'EMERGENCY_LANDING',
      'reason': 'Emergency landing at station',
    });
  });

  test('posts exact resolve-failure body keys and parses decision', () async {
    RequestOptions? captured;
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter((options) {
        captured = options;
        return jsonBody({
          'orderId': 'order-1',
          'trackingNumber': 'TRACK-1',
          'status': 'APPROVED',
          'updatedAt': '2026-11-10T12:00:00Z',
        });
      });

    final decision = await FailureApiService(dio).resolveFailure(
      'order-1',
      const FailureResolutionRequest(
        action: 'RESCHEDULE',
        note: 'Package inspected',
        packageVerified: true,
      ),
    );

    expect(captured?.method, 'POST');
    expect(captured?.path, '/delivery-orders/order-1/resolve-failure');
    expect(captured?.data, {
      'action': 'RESCHEDULE',
      'note': 'Package inspected',
      'packageVerified': true,
    });
    expect(captured?.data.keys, isNot(contains('resolutionAction')));
    expect(captured?.data.keys, isNot(contains('resolutionNote')));
    expect(decision.status, 'APPROVED');
  });

  for (final statusCode in [400, 403, 404, 409]) {
    test('preserves RFC 7807 detail for HTTP $statusCode', () async {
      final dio = Dio()
        ..httpClientAdapter = FakeHttpAdapter(
          (_) => jsonBody({
            'type': 'about:blank',
            'title': 'Problem',
            'detail': 'Failure request rejected.',
          }, statusCode),
        );

      await expectLater(
        FailureApiService(dio).getFailedOrders(const FailureOrderQuery()),
        throwsA(
          isA<ApiException>()
              .having((error) => error.statusCode, 'statusCode', statusCode)
              .having(
                (error) => error.message,
                'message',
                'Failure request rejected.',
              ),
        ),
      );
    });
  }
}
