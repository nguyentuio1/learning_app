import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/cancel_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_page_response.dart';
import 'package:smart_drone_delivery/data/models/update_delivery_order_dto.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_provider.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_notifier.dart';

class _FakeDeliveryOrderApiService extends DeliveryOrderApiService {
  _FakeDeliveryOrderApiService() : super(Dio());

  final order = const DeliveryOrder(
    id: 'order-1',
    recipientName: 'Sam Recipient',
    recipientPhone: '123456789',
    originStationId: '11111111-1111-1111-1111-111111111111',
    destinationStationId: '22222222-2222-2222-2222-222222222222',
    status: 'PENDING',
  );
  final calls = <String>[];
  Object? updateError;
  Object? cancelError;
  int listCalls = 0;
  UpdateDeliveryOrderDto? updateRequest;
  CancelDeliveryOrderDto? cancelRequest;

  @override
  Future<DeliveryOrderPageResponse> getMyOrders([
    DeliveryOrderFilterParams params = const DeliveryOrderFilterParams(),
  ]) async {
    listCalls++;
    return DeliveryOrderPageResponse(
      items: [order],
      page: params.page,
      pageSize: params.pageSize,
      totalCount: 1,
    );
  }

  @override
  Future<void> updateOrder(String id, UpdateDeliveryOrderDto update) async {
    calls.add('update:$id');
    updateRequest = update;
    if (updateError case final error?) throw error;
  }

  @override
  Future<void> cancelOrder(
    String id,
    CancelDeliveryOrderDto cancellation,
  ) async {
    calls.add('cancel:$id');
    cancelRequest = cancellation;
    if (cancelError case final error?) throw error;
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
  });

  tearDown(() => container.dispose());

  test('update DTO only serializes permitted mutable fields', () {
    const dto = UpdateDeliveryOrderDto(
      recipientName: 'Sam Recipient',
      recipientPhone: '123456789',
      destinationStationId: '22222222-2222-2222-2222-222222222222',
      deliveryAddressId: null,
    );

    expect(dto.toJson(), {
      'recipientName': 'Sam Recipient',
      'recipientPhone': '123456789',
      'destinationStationId': '22222222-2222-2222-2222-222222222222',
      'deliveryAddressId': null,
    });
    expect(dto.toJson().keys, [
      'recipientName',
      'recipientPhone',
      'destinationStationId',
      'deliveryAddressId',
    ]);
  });

  test('updates an order and invalidates the customer order list', () async {
    final subscription = container.listen(myOrdersProvider, (_, _) {});
    await container.read(myOrdersProvider.future);
    expect(service.listCalls, 1);

    await container
        .read(orderNotifierProvider.notifier)
        .updateOrder(
          service.order.id,
          const UpdateDeliveryOrderDto(
            recipientName: 'New Recipient',
            recipientPhone: '987654321',
            destinationStationId: '33333333-3333-3333-3333-333333333333',
          ),
          originStationId: service.order.originStationId,
          currentStatus: service.order.status,
        );

    expect(service.calls, ['update:order-1']);
    expect(container.read(orderNotifierProvider).hasValue, isTrue);
    await container.read(myOrdersProvider.future);
    expect(service.listCalls, 2);
    subscription.close();
  });

  test('captures update conflict in AsyncError', () async {
    service.updateError = const ApiException(
      message: 'Conflict',
      statusCode: 409,
    );

    await container
        .read(orderNotifierProvider.notifier)
        .updateOrder(
          service.order.id,
          const UpdateDeliveryOrderDto(
            recipientName: 'New Recipient',
            recipientPhone: '987654321',
            destinationStationId: '33333333-3333-3333-3333-333333333333',
          ),
          originStationId: service.order.originStationId,
          currentStatus: service.order.status,
        );

    final state = container.read(orderNotifierProvider);
    expect(state.hasError, isTrue);
    expect(state.error, isA<ApiException>());
    expect((state.error! as ApiException).statusCode, 409);
  });

  test('rejects an invalid recipient phone before updating', () async {
    await container
        .read(orderNotifierProvider.notifier)
        .updateOrder(
          service.order.id,
          const UpdateDeliveryOrderDto(
            recipientName: 'New Recipient',
            recipientPhone: '12345',
            destinationStationId: '33333333-3333-3333-3333-333333333333',
          ),
          originStationId: service.order.originStationId,
          currentStatus: service.order.status,
        );

    final state = container.read(orderNotifierProvider);
    expect(state.hasError, isTrue);
    expect((state.error! as ApiException).code, 'MSG-API-010');
    expect(
      (state.error! as ApiException).message,
      'Phone number must be 9 to 11 digits.',
    );
    expect(service.calls, isEmpty);
  });

  test('cancels an order and forwards the cancellation reason', () async {
    await container
        .read(orderNotifierProvider.notifier)
        .cancelOrder(
          service.order.id,
          const CancelDeliveryOrderDto(
            cancellationReason: '  Customer changed plans  ',
          ),
          currentStatus: service.order.status,
        );

    expect(service.calls, ['cancel:order-1']);
    expect(service.cancelRequest?.toJson(), {
      'cancellationReason': '  Customer changed plans  ',
    });
    expect(container.read(orderNotifierProvider).hasValue, isTrue);
  });

  test(
    'rejects an empty cancellation reason without making a request',
    () async {
      await container
          .read(orderNotifierProvider.notifier)
          .cancelOrder(
            service.order.id,
            const CancelDeliveryOrderDto(cancellationReason: ' \n '),
            currentStatus: service.order.status,
          );

      final state = container.read(orderNotifierProvider);
      expect(state.hasError, isTrue);
      expect((state.error! as ApiException).code, 'MSG-P-CO-20');
      expect(service.calls, isEmpty);
    },
  );

  test('captures API cancellation conflict in AsyncError', () async {
    service.cancelError = const ApiException(
      message: 'Conflict',
      statusCode: 409,
    );

    await container
        .read(orderNotifierProvider.notifier)
        .cancelOrder(
          service.order.id,
          const CancelDeliveryOrderDto(cancellationReason: 'Changed plans'),
          currentStatus: service.order.status,
        );

    final state = container.read(orderNotifierProvider);
    expect(state.hasError, isTrue);
    expect((state.error! as ApiException).statusCode, 409);
    expect(service.calls, ['cancel:order-1']);
  });

  test(
    'rejects an update for a non-pending order before making a request',
    () async {
      await container
          .read(orderNotifierProvider.notifier)
          .updateOrder(
            service.order.id,
            const UpdateDeliveryOrderDto(
              recipientName: 'New Recipient',
              recipientPhone: '987654321',
              destinationStationId: '33333333-3333-3333-3333-333333333333',
            ),
            originStationId: service.order.originStationId,
            currentStatus: 'IN_TRANSIT',
          );

      final state = container.read(orderNotifierProvider);
      expect((state.error! as ApiException).code, 'MSG-P-CO-17');
      expect(service.calls, isEmpty);
    },
  );

  test(
    'rejects cancellation of a non-cancellable order before API call',
    () async {
      await container
          .read(orderNotifierProvider.notifier)
          .cancelOrder(
            service.order.id,
            const CancelDeliveryOrderDto(cancellationReason: 'Changed plans'),
            currentStatus: 'DELIVERED',
          );

      final state = container.read(orderNotifierProvider);
      expect((state.error! as ApiException).code, 'MSG-P-CO-22');
      expect(service.calls, isEmpty);
    },
  );
}
