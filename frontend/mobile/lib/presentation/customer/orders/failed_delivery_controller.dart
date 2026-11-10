import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/mark_failed_delivery_request_dto.dart';
import '../../../data/models/resolve_failed_delivery_request_dto.dart';
import '../../../data/providers/repository_providers.dart';
import 'my_orders_provider.dart';

class FailedDeliveryController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> markFailed(
    String id,
    MarkFailedDeliveryRequestDto request,
  ) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final reason = request.failureReason.trim();
      if (reason.isEmpty) {
        throw const ApiException(
          message: 'Enter a failure reason.',
          code: 'MSG-P-DW-15',
        );
      }
      if (reason.length > 500 || request.failureType.trim().isEmpty) {
        throw const ApiException(
          message: 'Enter a valid failure type and reason.',
          statusCode: 400,
        );
      }
      await ref
          .read(deliveryOrderRepositoryProvider)
          .markFailed(
            id: id,
            failureReason: reason,
            failureType: request.failureType,
          );
      _invalidateOrderProviders(id);
    });
  }

  Future<void> resolveFailedDelivery(
    String id,
    ResolveFailedDeliveryRequestDto request,
  ) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final note = request.resolutionNote.trim();
      if (note.isEmpty || note.length > 500) {
        throw const ApiException(
          message: 'Select a resolution and enter a note.',
          code: 'MSG-P-DW-19',
        );
      }
      await ref
          .read(deliveryOrderRepositoryProvider)
          .resolveFailedDelivery(
            id: id,
            resolutionAction: request.resolutionAction,
            resolutionNote: note,
          );
      _invalidateOrderProviders(id);
    });
  }

  void _invalidateOrderProviders(String id) {
    ref.invalidate(myOrdersProvider);
    ref.invalidate(failedOrdersProvider);
    ref.invalidate(orderDetailProvider(id));
  }
}

final failedDeliveryControllerProvider =
    AsyncNotifierProvider.autoDispose<FailedDeliveryController, void>(
      FailedDeliveryController.new,
      name: 'failedDeliveryController',
    );
