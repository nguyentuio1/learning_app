import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_page_response.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';

import '../../../support/fake_http_adapter.dart';

class _FakeOrdersService extends DeliveryOrderApiService {
  _FakeOrdersService() : super(Dio());

  final requests = <DeliveryOrderFilterParams>[];

  @override
  Future<DeliveryOrderPageResponse> getMyOrders([
    DeliveryOrderFilterParams params = const DeliveryOrderFilterParams(),
  ]) async {
    requests.add(params);
    return DeliveryOrderPageResponse(
      items: [
        DeliveryOrder(
          id: 'order-${params.page}',
          recipientName: 'Recipient',
          recipientPhone: '123456789',
          originStationId: 'origin',
          destinationStationId: 'destination',
          status: params.status ?? 'PENDING',
          trackingNumber: 'TRACK-${params.page}',
        ),
      ],
      page: params.page,
      pageSize: params.pageSize,
      totalCount: params.pageSize * 2,
    );
  }
}

void main() {
  test(
    'paginates and resets to page one when status or search changes',
    () async {
      final service = _FakeOrdersService();
      final container = ProviderContainer(
        overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
        retry: (_, _) => null,
      );
      addTearDown(container.dispose);
      final subscription = container.listen(myOrdersProvider, (_, _) {});
      addTearDown(subscription.close);

      await container.read(myOrdersProvider.future);
      await container.read(myOrdersProvider.notifier).loadNextPage();
      expect(service.requests.map((request) => request.page), [1, 2]);

      container
          .read(deliveryOrderFilterParamsProvider.notifier)
          .setStatus('APPROVED');
      await container.read(myOrdersProvider.future);
      expect(service.requests.last.page, 1);
      expect(service.requests.last.status, 'APPROVED');

      container
          .read(deliveryOrderFilterParamsProvider.notifier)
          .setSearchKeyword('TRACK-2');
      await container.read(myOrdersProvider.future);
      expect(service.requests.last.page, 1);
      expect(service.requests.last.searchKeyword, 'TRACK-2');
    },
  );

  test('resets to page one when the date range changes', () {
    final container = ProviderContainer(retry: (_, _) => null);
    addTearDown(container.dispose);
    final filters = container.read(deliveryOrderFilterParamsProvider.notifier);
    filters.setDateRange(
      fromDate: DateTime.utc(2026, 10, 1),
      toDate: DateTime.utc(2026, 10, 23),
    );

    final state = container.read(deliveryOrderFilterParamsProvider);
    expect(state.page, 1);
    expect(state.fromDate, '2026-10-01T00:00:00.000Z');
    expect(state.toDate, '2026-10-23T00:00:00.000Z');
  });

  test('detail DTO sorts status history in ascending timestamp order', () {
    final detail = DeliveryOrderDetailResponseDto.fromJson({
      'id': 'order-1',
      'trackingNumber': 'TRACK-1',
      'recipientName': 'Recipient',
      'recipientPhone': '123456789',
      'originStationId': 'origin',
      'destinationStationId': 'destination',
      'status': 'APPROVED',
      'statusLogs': [
        {
          'fromStatus': 'APPROVED',
          'toStatus': 'IN_TRANSIT',
          'remarks': null,
          'createdAt': '2026-10-22T10:02:00Z',
        },
        {
          'fromStatus': null,
          'toStatus': 'PENDING',
          'remarks': 'Order created',
          'createdAt': '2026-10-22T10:00:00Z',
        },
      ],
    });

    expect(detail.statusLogs.map((log) => log.toStatus), [
      'PENDING',
      'IN_TRANSIT',
    ]);
  });

  test('maps a detail 404 response to MSG-P-CO-25', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => jsonBody({'detail': 'Not found'}, 404),
      );
    final service = DeliveryOrderApiService(dio);

    await expectLater(
      service.getOrderDetail('foreign-order'),
      throwsA(
        isA<ApiException>()
            .having((error) => error.code, 'code', 'MSG-P-CO-25')
            .having(
              (error) => error.message,
              'message',
              'Order not found or unavailable to your account.',
            ),
      ),
    );
  });

  test('maps invalid status query parameters to MSG-P-CO-23', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => throw StateError('Invalid filter must not reach the API.'),
      );
    final service = DeliveryOrderApiService(dio);

    await expectLater(
      service.getMyOrders(const DeliveryOrderFilterParams(status: 'INVALID')),
      throwsA(
        isA<ApiException>()
            .having((error) => error.code, 'code', 'MSG-P-CO-23')
            .having(
              (error) => error.message,
              'message',
              'Select a valid order status.',
            ),
      ),
    );
  });

  test('maps invalid page and page size to MSG-P-CO-24', () async {
    final dio = Dio()
      ..httpClientAdapter = FakeHttpAdapter(
        (_) => throw StateError('Invalid pagination must not reach the API.'),
      );
    final service = DeliveryOrderApiService(dio);

    for (final params in [
      const DeliveryOrderFilterParams(page: 0),
      const DeliveryOrderFilterParams(pageSize: 101),
    ]) {
      await expectLater(
        service.getMyOrders(params),
        throwsA(
          isA<ApiException>()
              .having((error) => error.code, 'code', 'MSG-P-CO-24')
              .having(
                (error) => error.message,
                'message',
                'Page must be at least 1 and page size must be within the allowed range.',
              ),
        ),
      );
    }
  });

  test('order detail DTO ignores any pickup secure code field', () {
    final detail = DeliveryOrderDetailResponseDto.fromJson({
      'id': 'order-1',
      'trackingNumber': 'TRACK-1',
      'recipientName': 'Recipient',
      'recipientPhone': '123456789',
      'originStationId': 'origin',
      'destinationStationId': 'destination',
      'status': 'PENDING',
      'statusLogs': const [],
      'pickupSecureCode': '123456',
    });

    expect(detail.id, 'order-1');
    expect(detail.statusLogs, isEmpty);
  });
}
