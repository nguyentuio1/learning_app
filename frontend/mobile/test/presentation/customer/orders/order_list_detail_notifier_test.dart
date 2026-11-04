import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/paged_result.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';

class _FakeOrderService extends DeliveryOrderApiService {
  _FakeOrderService() : super(Dio());

  final requests = <DeliveryOrderFilterParams>[];

  @override
  Future<PagedResult<DeliveryOrder>> getOrders(
    DeliveryOrderFilterParams params,
  ) async {
    requests.add(params);
    return PagedResult(
      items: [
        DeliveryOrder(
          id: 'order-${params.page}',
          recipientName: 'Customer',
          recipientPhone: '123456789',
          originStationId: 'origin',
          destinationStationId: 'destination',
          status: params.status ?? 'PENDING',
          trackingNumber: 'TRACK-${params.page}',
        ),
      ],
      page: params.page,
      pageSize: params.pageSize,
      totalCount: 40,
      totalPages: 2,
    );
  }
}

void main() {
  test(
    'paginates and resets the page when status/search filters change',
    () async {
      final service = _FakeOrderService();
      final container = ProviderContainer(
        overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
        retry: (_, _) => null,
      );
      addTearDown(container.dispose);
      final subscription = container.listen(myOrdersProvider, (_, _) {});
      addTearDown(subscription.close);

      await container.read(myOrdersProvider.future);
      await container.read(myOrdersProvider.notifier).loadNextPage();
      expect(service.requests.map((params) => params.page), [1, 2]);

      container
          .read(deliveryOrderFilterParamsProvider.notifier)
          .setStatus('IN_TRANSIT');
      await container.read(myOrdersProvider.future);
      expect(service.requests.last.page, 1);
      expect(service.requests.last.status, 'IN_TRANSIT');

      container
          .read(deliveryOrderFilterParamsProvider.notifier)
          .setSearchKeyword(' TRACK-2 ');
      await container.read(myOrdersProvider.future);
      expect(service.requests.last.page, 1);
      expect(service.requests.last.searchKeyword, 'TRACK-2');
    },
  );

  test('sends date range as ISO 8601 and resets page to one', () async {
    final service = _FakeOrderService();
    final container = ProviderContainer(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      retry: (_, _) => null,
    );
    addTearDown(container.dispose);
    final subscription = container.listen(myOrdersProvider, (_, _) {});
    addTearDown(subscription.close);

    await container.read(myOrdersProvider.future);
    await container.read(myOrdersProvider.notifier).loadNextPage();
    final fromDate = DateTime(2026, 10, 29);
    final toDate = DateTime(2026, 11, 5);

    container
        .read(deliveryOrderFilterParamsProvider.notifier)
        .setDateRange(fromDate, toDate);
    await container.read(myOrdersProvider.future);

    expect(service.requests.last.page, 1);
    expect(service.requests.last.fromDate, fromDate.toIso8601String());
    expect(service.requests.last.toDate, toDate.toIso8601String());

    container
        .read(deliveryOrderFilterParamsProvider.notifier)
        .setDateRange(null, null);
    await container.read(myOrdersProvider.future);
    expect(service.requests.last.fromDate, isNull);
    expect(service.requests.last.toDate, isNull);
    expect(service.requests.last.page, 1);
  });
}
