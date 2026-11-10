import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/cancel_delivery_order_dto.dart';
import '../../../data/providers/delivery_order_providers.dart';

class CancelOrderController extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> cancelOrder(String id, String cancellationReason) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final reason = cancellationReason.trim();
      if (reason.isEmpty) {
        throw const ApiException(
          message: 'Enter a cancellation reason.',
          code: 'MSG-P-CO-20',
        );
      }
      if (reason.length > 500) {
        throw const ApiException(
          message: 'Cancellation reason must be at most 500 characters.',
          statusCode: 400,
        );
      }
      await ref
          .read(deliveryOrderApiServiceProvider)
          .cancelOrder(id, CancelDeliveryOrderDto(cancellationReason: reason));
    });
  }
}

final cancelOrderControllerProvider =
    AsyncNotifierProvider.autoDispose<CancelOrderController, void>(
      CancelOrderController.new,
      name: 'cancelOrderController',
    );
