import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/failure_models.dart';
import '../../../data/providers/failure_providers.dart';

class ResolveFailureController extends AsyncNotifier<OrderDecision?> {
  @override
  FutureOr<OrderDecision?> build() => null;

  Future<OrderDecision?> resolveFailure({
    required String orderId,
    required String action,
    required String note,
    required bool packageVerified,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final normalizedNote = note.trim();
      if (normalizedNote.isEmpty) {
        throw const ApiException(
          message: 'Enter a resolution note.',
          code: 'MSG-P-DW-19',
        );
      }
      if (normalizedNote.length > 500) {
        throw const ApiException(
          message: 'Resolution note must be at most 500 characters.',
          statusCode: 400,
        );
      }
      if (action == 'RESCHEDULE' && !packageVerified) {
        throw const ApiException(
          message: 'Verify the package before rescheduling.',
          code: 'MSG-P-DW-20',
        );
      }
      final decision = await ref
          .read(failureApiServiceProvider)
          .resolveFailure(
            orderId,
            FailureResolutionRequest(
              action: action,
              note: normalizedNote,
              packageVerified: packageVerified,
            ),
          );
      ref.invalidate(failedOrdersProvider);
      ref.invalidate(failedOrderDetailProvider(orderId));
      return decision;
    });
    return state.hasValue ? state.requireValue : null;
  }
}

final resolveFailureControllerProvider =
    AsyncNotifierProvider.autoDispose<ResolveFailureController, OrderDecision?>(
      ResolveFailureController.new,
      name: 'resolveFailureController',
    );
