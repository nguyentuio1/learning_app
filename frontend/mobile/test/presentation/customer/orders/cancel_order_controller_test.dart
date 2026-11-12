import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/cancel_order_controller.dart';

class _FakeDeliveryOrderApiService extends DeliveryOrderApiService {
  _FakeDeliveryOrderApiService() : super(Dio());

  CancelDeliveryOrderDto? request;

  @override
  Future<void> cancelOrder(
    String id,
    CancelDeliveryOrderDto cancellation,
  ) async {
    request = cancellation;
  }
}

void main() {
  late _FakeDeliveryOrderApiService service;
  late ProviderContainer container;

  setUp(() {
    service = _FakeDeliveryOrderApiService();
    container = ProviderContainer(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      retry: (_, _) => null,
    );
    container.listen(cancelOrderControllerProvider, (_, _) {});
  });

  tearDown(() => container.dispose());

  for (final status in ['PENDING', 'APPROVED']) {
    test('$status orders can be cancelled', () async {
      await container
          .read(cancelOrderControllerProvider.notifier)
          .cancelOrder('order-1', status, 'Reason');

      expect(service.request?.cancellationReason, 'Reason');
      expect(container.read(cancelOrderControllerProvider).hasValue, isTrue);
    });
  }

  for (final status in [
    'READY_FOR_TAKEOFF',
    'IN_TRANSIT',
    'ARRIVED',
    'DELIVERED',
    'CANCELLED',
    'FAILED',
  ]) {
    test('$status orders are rejected without an API call', () async {
      await container
          .read(cancelOrderControllerProvider.notifier)
          .cancelOrder('order-1', status, 'Reason');

      final state = container.read(cancelOrderControllerProvider);
      expect(state.hasError, isTrue);
      expect(state.error.toString(), contains('This order can no longer'));
      expect(service.request, isNull);
    });
  }

  test('trims cancellation reason before submitting', () async {
    await container
        .read(cancelOrderControllerProvider.notifier)
        .cancelOrder('order-1', 'PENDING', '  Changed plans \n');

    expect(service.request?.cancellationReason, 'Changed plans');
  });

  test('allows exactly 500 characters', () async {
    final reason = 'r' * 500;
    await container
        .read(cancelOrderControllerProvider.notifier)
        .cancelOrder('order-1', 'APPROVED', reason);

    expect(service.request?.cancellationReason, reason);
    expect(container.read(cancelOrderControllerProvider).hasValue, isTrue);
  });

  test('rejects reasons longer than 500 characters', () async {
    await container
        .read(cancelOrderControllerProvider.notifier)
        .cancelOrder('order-1', 'PENDING', 'r' * 501);

    expect(container.read(cancelOrderControllerProvider).hasError, isTrue);
    expect(service.request, isNull);
  });
}
