import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/cancel_delivery_order_dto.dart';
import '../../../data/models/update_delivery_order_dto.dart';
import '../../../data/providers/delivery_order_providers.dart';
import 'my_orders_provider.dart';

final _guidPattern = RegExp(
  r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
);

class OrderNotifier extends AsyncNotifier<void> {
  @override
  FutureOr<void> build() {}

  Future<void> updateOrder(
    String id,
    UpdateDeliveryOrderDto update, {
    required String originStationId,
    required String currentStatus,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      if (currentStatus != 'PENDING') {
        throw const ApiException(
          message: 'Only pending orders can be edited.',
          statusCode: 409,
          code: 'MSG-P-CO-17',
        );
      }
      _validateUpdate(update, originStationId);
      await ref.read(deliveryOrderApiServiceProvider).updateOrder(id, update);
      ref.invalidate(myOrdersProvider);
    });
  }

  Future<void> cancelOrder(
    String id,
    CancelDeliveryOrderDto cancellation, {
    required String currentStatus,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      if (currentStatus != 'PENDING' && currentStatus != 'APPROVED') {
        throw const ApiException(
          message: 'This order can no longer be cancelled.',
          statusCode: 409,
          code: 'MSG-P-CO-22',
        );
      }
      if (cancellation.cancellationReason.trim().isEmpty) {
        throw const ApiException(
          message: 'Enter a cancellation reason.',
          code: 'MSG-P-CO-20',
        );
      }
      if (cancellation.cancellationReason.length > 500) {
        throw const ApiException(
          message: 'Cancellation reason must be at most 500 characters.',
        );
      }
      await ref
          .read(deliveryOrderApiServiceProvider)
          .cancelOrder(id, cancellation);
      ref.invalidate(myOrdersProvider);
    });
  }

  void _validateUpdate(UpdateDeliveryOrderDto update, String originStationId) {
    if (!RegExp(r'^\d{9,11}$').hasMatch(update.recipientPhone)) {
      throw const ApiException(
        message: 'Phone number must be 9 to 11 digits.',
        code: 'MSG-API-010',
      );
    }
    if (update.recipientName.trim().isEmpty ||
        update.recipientName.trim().length > 150 ||
        !_guidPattern.hasMatch(update.destinationStationId) ||
        update.destinationStationId.toLowerCase() ==
            originStationId.toLowerCase() ||
        (update.deliveryAddressId != null &&
            !_guidPattern.hasMatch(update.deliveryAddressId!))) {
      throw const ApiException(
        message: 'These order fields cannot be changed.',
        code: 'MSG-P-CO-18',
      );
    }
  }
}

final orderNotifierProvider =
    AsyncNotifierProvider.autoDispose<OrderNotifier, void>(
      OrderNotifier.new,
      name: 'orderNotifier',
    );
