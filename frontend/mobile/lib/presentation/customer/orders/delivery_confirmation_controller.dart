import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/station_operation_models.dart';
import '../../../data/providers/delivery_order_providers.dart';
import 'my_orders_provider.dart';

class DeliveryConfirmationState {
  const DeliveryConfirmationState({
    this.isSubmitting = false,
    this.errorMessage,
    this.remainingAttempts,
    this.isLocked = false,
    this.lockoutSecondsRemaining,
    this.lockoutExpiresAt,
    this.completedConfirmation,
    this.requiresOrderRefresh = false,
    this.accessLost = false,
  });

  final bool isSubmitting;
  final String? errorMessage;
  final int? remainingAttempts;
  final bool isLocked;
  final int? lockoutSecondsRemaining;
  final DateTime? lockoutExpiresAt;
  final ConfirmPickupResponse? completedConfirmation;
  final bool requiresOrderRefresh;
  final bool accessLost;

  DeliveryConfirmationState copyWith({
    bool? isSubmitting,
    String? errorMessage,
    bool clearError = false,
    int? remainingAttempts,
    bool clearLockout = false,
    bool? isLocked,
    int? lockoutSecondsRemaining,
    DateTime? lockoutExpiresAt,
    ConfirmPickupResponse? completedConfirmation,
    bool clearCompletedConfirmation = false,
    bool? requiresOrderRefresh,
    bool? accessLost,
  }) =>
      DeliveryConfirmationState(
        isSubmitting: isSubmitting ?? this.isSubmitting,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        remainingAttempts: clearLockout
            ? null
            : (remainingAttempts ?? this.remainingAttempts),
        isLocked: clearLockout ? false : (isLocked ?? this.isLocked),
        lockoutSecondsRemaining: clearLockout
            ? null
            : (lockoutSecondsRemaining ?? this.lockoutSecondsRemaining),
        lockoutExpiresAt: clearLockout
            ? null
            : (lockoutExpiresAt ?? this.lockoutExpiresAt),
        completedConfirmation: clearCompletedConfirmation
            ? null
            : (completedConfirmation ?? this.completedConfirmation),
        requiresOrderRefresh:
            requiresOrderRefresh ?? this.requiresOrderRefresh,
        accessLost: accessLost ?? this.accessLost,
      );
}

class DeliveryConfirmationController
    extends Notifier<DeliveryConfirmationState> {
  DeliveryConfirmationController(this.orderId);

  final String orderId;

  @override
  DeliveryConfirmationState build() => const DeliveryConfirmationState();

  Future<bool> confirmDelivery({
    required String secureCode,
    String? currentOrderStatus,
  }) async {
    if (state.isSubmitting) {
      return false;
    }

    if (state.isLocked) {
      final expiresAt = state.lockoutExpiresAt;
      if (expiresAt == null || expiresAt.isAfter(DateTime.now().toUtc())) {
        return false;
      }
      state = state.copyWith(clearLockout: true, clearError: true);
    }

    if (currentOrderStatus != null &&
        currentOrderStatus.trim().toUpperCase() != 'ARRIVED') {
      state = state.copyWith(
        errorMessage: 'Only arrived orders can be confirmed for delivery.',
      );
      return false;
    }

    final code = secureCode.trim();
    if (!RegExp(r'^\d{6}$').hasMatch(code)) {
      state = state.copyWith(
        errorMessage: 'Pickup code must be exactly 6 digits.',
      );
      return false;
    }

    state = state.copyWith(
      isSubmitting: true,
      clearError: true,
      requiresOrderRefresh: false,
    );

    try {
      final response = await ref
          .read(deliveryOrderApiServiceProvider)
          .confirmDelivery(
            orderId,
            ConfirmDeliveryRequest(pickupSecureCode: code),
          );
      if (!ref.mounted) return false;
      state = state.copyWith(
        isSubmitting: false,
        completedConfirmation: response,
        clearLockout: true,
        clearError: true,
        requiresOrderRefresh: false,
        accessLost: false,
      );
      ref.invalidate(orderDetailProvider(orderId));
      ref.invalidate(myOrdersProvider);
      return true;
    } on ApiException catch (e) {
      if (!ref.mounted) return false;

      if (e.isNetwork) {
        ref.invalidate(orderDetailProvider(orderId));
        ref.invalidate(myOrdersProvider);
        state = state.copyWith(
          isSubmitting: false,
          requiresOrderRefresh: true,
          errorMessage:
              'Connection lost before confirmation completed. Order status is being refreshed before you retry.',
        );
        return false;
      }

      if (e.statusCode == 401 ||
          e.statusCode == 403 ||
          e.statusCode == 404) {
        ref.invalidate(orderDetailProvider(orderId));
        ref.invalidate(myOrdersProvider);
        state = state.copyWith(
          isSubmitting: false,
          errorMessage: e.message,
          accessLost: true,
          clearCompletedConfirmation: true,
          clearLockout: true,
        );
        return false;
      }

      if (e.statusCode == 409) {
        ref.invalidate(orderDetailProvider(orderId));
        ref.invalidate(myOrdersProvider);
        state = state.copyWith(
          isSubmitting: false,
          requiresOrderRefresh: true,
          errorMessage: e.message,
        );
        return false;
      }

      final locked = e.statusCode == 423 ||
          e.lockoutExpiresAt != null ||
          e.retryAfterSeconds != null;
      final expiresAt = e.lockoutExpiresAt;
      int? lockoutSeconds = e.retryAfterSeconds;
      if (lockoutSeconds == null && expiresAt != null) {
        final diff = expiresAt.difference(DateTime.now().toUtc()).inSeconds;
        lockoutSeconds = diff > 0 ? diff : 0;
      }

      if (locked) {
        state = state.copyWith(
          isSubmitting: false,
          errorMessage: e.message,
          remainingAttempts: 0,
          isLocked: true,
          lockoutSecondsRemaining: lockoutSeconds,
          lockoutExpiresAt: expiresAt,
        );
        return false;
      }

      state = state.copyWith(
        isSubmitting: false,
        errorMessage: e.message,
        remainingAttempts: e.attemptsRemaining ?? state.remainingAttempts,
        isLocked: false,
      );
      return false;
    } catch (_) {
      if (!ref.mounted) return false;
      state = state.copyWith(
        isSubmitting: false,
        errorMessage:
            'An unexpected error occurred during delivery confirmation.',
      );
      return false;
    }
  }

  void clearError() {
    if (state.errorMessage != null) {
      state = state.copyWith(clearError: true);
    }
  }

  void reset() {
    state = const DeliveryConfirmationState();
  }
}

final deliveryConfirmationControllerProvider = NotifierProvider.autoDispose
    .family<DeliveryConfirmationController, DeliveryConfirmationState, String>(
      DeliveryConfirmationController.new,
      name: 'deliveryConfirmationController',
    );
