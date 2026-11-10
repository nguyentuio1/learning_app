import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../core/network/network_providers.dart';
import '../../../data/models/station_operation_models.dart';
import '../../../data/services/station_operations_api_service.dart';

final stationOperationsApiServiceProvider =
    Provider<StationOperationsApiService>(
  (ref) => StationOperationsApiService(ref.watch(dioProvider)),
  name: 'stationOperationsApiService',
);

class PickupState {
  const PickupState({
    this.orderInfo,
    this.isLoading = false,
    this.isConfirming = false,
    this.errorMessage,
    this.completedPickup,
    this.remainingAttempts,
    this.isLocked = false,
    this.lockoutSecondsRemaining,
    this.lockoutExpiresAt,
  });

  final StationOrderInfo? orderInfo;
  final bool isLoading;
  final bool isConfirming;
  final String? errorMessage;
  final ConfirmPickupResponse? completedPickup;
  final int? remainingAttempts;
  final bool isLocked;
  final int? lockoutSecondsRemaining;
  final DateTime? lockoutExpiresAt;

  PickupState copyWith({
    StationOrderInfo? orderInfo,
    bool clearOrderInfo = false,
    bool? isLoading,
    bool? isConfirming,
    String? errorMessage,
    bool clearError = false,
    ConfirmPickupResponse? completedPickup,
    bool clearCompletedPickup = false,
    int? remainingAttempts,
    bool clearLockout = false,
    bool? isLocked,
    int? lockoutSecondsRemaining,
    DateTime? lockoutExpiresAt,
  }) =>
      PickupState(
        orderInfo: clearOrderInfo ? null : (orderInfo ?? this.orderInfo),
        isLoading: isLoading ?? this.isLoading,
        isConfirming: isConfirming ?? this.isConfirming,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        completedPickup: clearCompletedPickup
            ? null
            : (completedPickup ?? this.completedPickup),
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
      );
}

class PickupController extends Notifier<PickupState> {
  @override
  PickupState build() => const PickupState();

  StationOperationsApiService get _apiService =>
      ref.read(stationOperationsApiServiceProvider);

  static String normalizeTrackingNumber(String trackingNumber) =>
      trackingNumber.trim().toUpperCase();

  Future<void> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async {
    final normalized = normalizeTrackingNumber(trackingNumber);
    if (normalized.isEmpty) {
      state = const PickupState(
        errorMessage: 'Please enter a tracking number.',
      );
      return;
    }

    // Reset all previous order/pickup/lockout state at the start of a new lookup
    state = const PickupState(isLoading: true);
    try {
      final info = await _apiService.lookupOrder(
        stationId: stationId,
        trackingNumber: normalized,
      );
      state = PickupState(orderInfo: info, isLoading: false);
    } on ApiException catch (e) {
      state = PickupState(
        isLoading: false,
        errorMessage: e.message,
      );
    } catch (_) {
      state = const PickupState(
        isLoading: false,
        errorMessage: 'Failed to look up order. Check tracking number.',
      );
    }
  }

  Future<bool> confirmPickup({
    required String stationId,
    required String trackingNumber,
    required String secureCode,
  }) async {
    if (state.isLocked) {
      return false;
    }

    final currentOrder = state.orderInfo;
    if (currentOrder != null && !currentOrder.isEligibleForPickup) {
      state = state.copyWith(
        errorMessage: eligibilityMessage(currentOrder),
      );
      return false;
    }

    final normalizedTracking =
        currentOrder != null && currentOrder.trackingNumber.isNotEmpty
            ? normalizeTrackingNumber(currentOrder.trackingNumber)
            : normalizeTrackingNumber(trackingNumber);
    final code = secureCode.trim();
    if (!RegExp(r'^\d{6}$').hasMatch(code)) {
      state = state.copyWith(
        errorMessage: 'Secure code must be exactly 6 digits.',
      );
      return false;
    }

    state = state.copyWith(isConfirming: true, clearError: true);
    try {
      final response = await _apiService.confirmPickup(
        stationId: stationId,
        request: ConfirmPickupRequest(
          trackingNumber: normalizedTracking,
          pickupSecureCode: code,
        ),
      );
      state = state.copyWith(
        isConfirming: false,
        completedPickup: response,
        clearLockout: true,
      );
      return true;
    } on ApiException catch (e) {
      final locked = e.statusCode == 423 ||
          e.lockoutExpiresAt != null ||
          e.retryAfterSeconds != null;
      final expiresAt = e.lockoutExpiresAt;
      int? lockoutSeconds = e.retryAfterSeconds;
      if (lockoutSeconds == null && expiresAt != null) {
        final diff = expiresAt.difference(DateTime.now().toUtc()).inSeconds;
        lockoutSeconds = diff > 0 ? diff : 0;
      }
      final attempts = locked ? (e.attemptsRemaining ?? 0) : e.attemptsRemaining;

      state = state.copyWith(
        isConfirming: false,
        errorMessage: e.message,
        remainingAttempts: attempts,
        isLocked: locked,
        lockoutSecondsRemaining: lockoutSeconds,
        lockoutExpiresAt: expiresAt,
      );
      return false;
    } catch (_) {
      state = state.copyWith(
        isConfirming: false,
        errorMessage: 'An unexpected error occurred during pickup confirmation.',
      );
      return false;
    }
  }

  static String eligibilityMessage(StationOrderInfo order) {
    if (!order.isDestination) {
      return 'This parcel is not at its destination station (role: ${order.role}). '
          'Pickup can only be confirmed at the destination station.';
    }
    return 'This parcel is currently ${order.status} and is not ready for pickup. '
        'Only ARRIVED parcels at the destination station can be picked up.';
  }

  void reset() {
    state = const PickupState();
  }
}

final pickupControllerProvider =
    NotifierProvider.autoDispose<PickupController, PickupState>(
  PickupController.new,
  name: 'pickupController',
);
