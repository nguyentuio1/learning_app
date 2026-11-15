import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/failure_models.dart';
import '../../../data/providers/failure_providers.dart';

const reportableFailureTypes = [
  'MISSION_ABORTED',
  'EMERGENCY_LANDING',
  'HARDWARE_FAULT',
];

class MarkFailureController extends AsyncNotifier<OrderDecision?> {
  @override
  FutureOr<OrderDecision?> build() => null;

  Future<OrderDecision?> reportFailure({
    required String orderId,
    required String failureType,
    required String reason,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final normalizedOrderId = orderId.trim();
      final normalizedReason = reason.trim();
      if (normalizedOrderId.isEmpty) {
        throw const ApiException(message: 'Enter an order ID.');
      }
      if (!reportableFailureTypes.contains(failureType)) {
        throw const ApiException(message: 'Select a failure type.');
      }
      if (normalizedReason.isEmpty) {
        throw const ApiException(message: 'Enter a failure reason.');
      }
      if (normalizedReason.length > 500) {
        throw const ApiException(
          message: 'Failure reason must be at most 500 characters.',
          statusCode: 400,
        );
      }

      final decision = await ref
          .read(failureApiServiceProvider)
          .reportFailure(normalizedOrderId, failureType, normalizedReason);
      ref.invalidate(failedOrdersProvider);
      ref.invalidate(failedOrderDetailProvider(normalizedOrderId));
      return decision;
    });
    return state.hasValue ? state.requireValue : null;
  }
}

final markFailureControllerProvider =
    AsyncNotifierProvider.autoDispose<MarkFailureController, OrderDecision?>(
      MarkFailureController.new,
      name: 'markFailureController',
    );
