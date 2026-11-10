import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/resolve_failed_delivery_request_dto.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/resolution_action.dart';
import 'package:smart_drone_delivery/domain/repositories/delivery_order_repository.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/failed_delivery_controller.dart';

class _FakeDeliveryOrderRepository implements DeliveryOrderRepository {
  _FakeDeliveryOrderRepository({this.resolveError});

  final Object? resolveError;

  @override
  Future<void> markFailed({
    required String id,
    required String failureReason,
    required String failureType,
  }) async {}

  @override
  Future<void> resolveFailedDelivery({
    required String id,
    required ResolutionAction resolutionAction,
    required String resolutionNote,
  }) async {
    if (resolveError != null) throw resolveError!;
  }
}

void main() {
  test(
    'transitions through AsyncLoading to AsyncData after resolve succeeds',
    () async {
      final container = ProviderContainer(
        overrides: [
          deliveryOrderRepositoryProvider.overrideWithValue(
            _FakeDeliveryOrderRepository(),
          ),
        ],
        retry: (_, _) => null,
      );
      addTearDown(container.dispose);
      final states = <AsyncValue<void>>[];
      final subscription = container.listen(
        failedDeliveryControllerProvider,
        (_, state) => states.add(state),
      );
      addTearDown(subscription.close);
      await container.read(failedDeliveryControllerProvider.future);

      await container
          .read(failedDeliveryControllerProvider.notifier)
          .resolveFailedDelivery(
            'order-1',
            const ResolveFailedDeliveryRequestDto(
              resolutionAction: ResolutionAction.reschedule,
              resolutionNote: 'Schedule another delivery.',
            ),
          );

      expect(states.any((state) => state.isLoading), isTrue);
      expect(states.last.hasValue, isTrue);
    },
  );

  for (final (statusCode, code) in [
    (400, 'MSG-P-DW-19'),
    (409, 'MSG-P-DW-20'),
  ]) {
    test('preserves API $statusCode errors as AsyncError', () async {
      final exception = ApiException(
        message: 'Problem details response.',
        statusCode: statusCode,
        code: code,
      );
      final container = ProviderContainer(
        overrides: [
          deliveryOrderRepositoryProvider.overrideWithValue(
            _FakeDeliveryOrderRepository(resolveError: exception),
          ),
        ],
        retry: (_, _) => null,
      );
      addTearDown(container.dispose);
      await container.read(failedDeliveryControllerProvider.future);

      await container
          .read(failedDeliveryControllerProvider.notifier)
          .resolveFailedDelivery(
            'order-1',
            const ResolveFailedDeliveryRequestDto(
              resolutionAction: ResolutionAction.cancel,
              resolutionNote: 'Cancel the failed delivery.',
            ),
          );

      final state = container.read(failedDeliveryControllerProvider);
      expect(state.hasError, isTrue);
      expect(state.error, same(exception));
    });
  }
}
