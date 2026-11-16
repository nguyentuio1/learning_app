import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
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
  Timer? _lockoutTimer;
  int _refreshGeneration = 0;
  bool _awaitingFreshRead = false;

  @override
  DeliveryConfirmationState build() {
    ref.onDispose(_cancelLockoutTimer);
    ref.listen<AsyncValue<DeliveryOrderDetailResponseDto>>(
      orderDetailProvider(orderId),
      (previous, next) {
        if (next.hasError) {
          final error = next.error;
          if (error != null && isAccessDeniedError(error)) {
            _awaitingFreshRead = false;
            _cancelLockoutTimer();
            state = state.copyWith(
              isSubmitting: false,
              accessLost: true,
              clearCompletedConfirmation: true,
              clearLockout: true,
              requiresOrderRefresh: false,
              errorMessage: accessDeniedMessage(error),
            );
            return;
          }
        }
        if (next.isLoading || next.isRefreshing) return;
        if (next.hasError) {
          _awaitingFreshRead = false;
          return;
        }
        if (next.hasValue) {
          final detail = next.requireValue;
          if (state.accessLost) {
            state = state.copyWith(accessLost: false, clearError: true);
          }
          if (state.requiresOrderRefresh && _awaitingFreshRead) {
            _awaitingFreshRead = false;
            if (detail.status == 'ARRIVED' || detail.status == 'DELIVERED') {
              state = state.copyWith(requiresOrderRefresh: false);
            }
          }
        }
      },
    );
    final initialDetail = ref.read(orderDetailProvider(orderId));
    if (initialDetail.hasError &&
        initialDetail.error != null &&
        isAccessDeniedError(initialDetail.error!)) {
      return DeliveryConfirmationState(
        accessLost: true,
        errorMessage: accessDeniedMessage(initialDetail.error!),
      );
    }
    return const DeliveryConfirmationState();
  }

  static bool isAccessDeniedError(Object error) {
    if (error is ApiException) {
      return error.statusCode == 401 ||
          error.statusCode == 403 ||
          error.statusCode == 404 ||
          error.code == 'MSG-P-CO-25';
    }
    return false;
  }

  static String accessDeniedMessage(Object error) {
    if (error is ApiException) {
      if (error.code == 'MSG-P-CO-25' || error.statusCode == 404) {
        return 'Order not found or unavailable to your account.';
      }
      if (error.message.isNotEmpty) {
        return error.message;
      }
    }
    return 'You no longer have access to this delivery order.';
  }

  void _cancelLockoutTimer() {
    _lockoutTimer?.cancel();
    _lockoutTimer = null;
  }

  void _scheduleLockoutExpiry({
    required DateTime? expiresAt,
    required int? retryAfterSeconds,
  }) {
    _cancelLockoutTimer();
    final now = DateTime.now().toUtc();
    Duration? duration;

    if (expiresAt != null) {
      final diff = expiresAt.difference(now);
      if (diff > Duration.zero) {
        duration = diff;
      }
    }
    if (retryAfterSeconds != null && retryAfterSeconds > 0) {
      final retryDuration = Duration(seconds: retryAfterSeconds);
      if (duration == null || retryDuration < duration) {
        duration = retryDuration;
      }
    }

    duration ??= const Duration(milliseconds: 1);

    _lockoutTimer = Timer(duration, () {
      _lockoutTimer = null;
      if (!ref.mounted) return;
      state = state.copyWith(clearLockout: true, clearError: true);
    });
  }

  Future<bool> refreshOrderEligibility() async {
    final generation = ++_refreshGeneration;
    _awaitingFreshRead = true;
    state = state.copyWith(requiresOrderRefresh: true);
    ref.invalidate(orderDetailProvider(orderId));
    ref.invalidate(myOrdersProvider);

    try {
      final detail = await ref.read(orderDetailProvider(orderId).future);
      if (!ref.mounted || generation != _refreshGeneration) return false;
      _awaitingFreshRead = false;
      if (detail.status == 'ARRIVED' || detail.status == 'DELIVERED') {
        state = state.copyWith(requiresOrderRefresh: false);
        return true;
      }
      state = state.copyWith(
        requiresOrderRefresh: false,
        errorMessage:
            'Order ${detail.trackingNumber ?? detail.id} is currently ${detail.status}. Only ARRIVED orders can be confirmed for delivery.',
      );
      return false;
    } catch (error) {
      if (!ref.mounted || generation != _refreshGeneration) return false;
      _awaitingFreshRead = false;
      if (isAccessDeniedError(error)) {
        _cancelLockoutTimer();
        state = state.copyWith(
          isSubmitting: false,
          accessLost: true,
          clearCompletedConfirmation: true,
          clearLockout: true,
          requiresOrderRefresh: false,
          errorMessage: accessDeniedMessage(error),
        );
        return false;
      }
      state = state.copyWith(requiresOrderRefresh: true);
      return false;
    }
  }

  Future<bool> confirmDelivery({
    required String secureCode,
    String? currentOrderStatus,
  }) async {
    if (state.isSubmitting || state.requiresOrderRefresh) {
      return false;
    }

    if (state.isLocked) {
      final expiresAt = state.lockoutExpiresAt;
      if (expiresAt == null || expiresAt.isAfter(DateTime.now().toUtc())) {
        return false;
      }
      _cancelLockoutTimer();
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
      _cancelLockoutTimer();
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
        state = state.copyWith(
          isSubmitting: false,
          requiresOrderRefresh: true,
          errorMessage:
              'Connection lost before confirmation completed. Order status is being refreshed before you retry.',
        );
        unawaited(refreshOrderEligibility());
        return false;
      }

      if (isAccessDeniedError(e)) {
        _cancelLockoutTimer();
        ref.invalidate(orderDetailProvider(orderId));
        ref.invalidate(myOrdersProvider);
        state = state.copyWith(
          isSubmitting: false,
          errorMessage: accessDeniedMessage(e),
          accessLost: true,
          clearCompletedConfirmation: true,
          clearLockout: true,
          requiresOrderRefresh: false,
        );
        return false;
      }

      if (e.statusCode == 409) {
        state = state.copyWith(
          isSubmitting: false,
          requiresOrderRefresh: true,
          errorMessage: e.message,
        );
        unawaited(refreshOrderEligibility());
        return false;
      }

      final locked = e.statusCode == 423 ||
          e.lockoutExpiresAt != null ||
          e.retryAfterSeconds != null;
      final expiresAt = e.lockoutExpiresAt;
      int? lockoutSeconds = e.retryAfterSeconds;
      if (lockoutSeconds == null && expiresAt != null) {
        final diffMs = expiresAt
            .difference(DateTime.now().toUtc())
            .inMilliseconds;
        lockoutSeconds = diffMs > 0 ? (diffMs / 1000).ceil() : 0;
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
        _scheduleLockoutExpiry(
          expiresAt: expiresAt,
          retryAfterSeconds: lockoutSeconds,
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
    _cancelLockoutTimer();
    _refreshGeneration++;
    state = const DeliveryConfirmationState();
  }
}

final deliveryConfirmationControllerProvider = NotifierProvider.autoDispose
    .family<DeliveryConfirmationController, DeliveryConfirmationState, String>(
      DeliveryConfirmationController.new,
      name: 'deliveryConfirmationController',
    );
