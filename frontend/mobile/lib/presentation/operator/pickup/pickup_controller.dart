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
  });

  final StationOrderInfo? orderInfo;
  final bool isLoading;
  final bool isConfirming;
  final String? errorMessage;
  final ConfirmPickupResponse? completedPickup;
  final int? remainingAttempts;
  final bool isLocked;
  final int? lockoutSecondsRemaining;

  PickupState copyWith({
    StationOrderInfo? orderInfo,
    bool? isLoading,
    bool? isConfirming,
    String? errorMessage,
    bool clearError = false,
    ConfirmPickupResponse? completedPickup,
    int? remainingAttempts,
    bool? isLocked,
    int? lockoutSecondsRemaining,
  }) =>
      PickupState(
        orderInfo: orderInfo ?? this.orderInfo,
        isLoading: isLoading ?? this.isLoading,
        isConfirming: isConfirming ?? this.isConfirming,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        completedPickup: completedPickup ?? this.completedPickup,
        remainingAttempts: remainingAttempts ?? this.remainingAttempts,
        isLocked: isLocked ?? this.isLocked,
        lockoutSecondsRemaining:
            lockoutSecondsRemaining ?? this.lockoutSecondsRemaining,
      );
}

class PickupController extends StateNotifier<PickupState> {
  PickupController(this._apiService) : super(const PickupState());

  final StationOperationsApiService _apiService;

  Future<void> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async {
    final trimmed = trackingNumber.trim().toUpperCase();
    if (trimmed.isEmpty) {
      state = state.copyWith(errorMessage: 'Please enter a tracking number.');
      return;
    }

    state = state.copyWith(isLoading: true, clearError: true);
    try {
      final info = await _apiService.lookupOrder(
        stationId: stationId,
        trackingNumber: trimmed,
      );
      state = state.copyWith(orderInfo: info, isLoading: false);
    } on ApiException catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: e.message,
      );
    } catch (_) {
      state = state.copyWith(
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
          trackingNumber: trackingNumber,
          pickupSecureCode: code,
        ),
      );
      state = state.copyWith(
        isConfirming: false,
        completedPickup: response,
      );
      return true;
    } on ApiException catch (e) {
      int? attempts;
      bool locked = false;
      if (e.statusCode == 423) {
        locked = true;
      }
      state = state.copyWith(
        isConfirming: false,
        errorMessage: e.message,
        remainingAttempts: attempts,
        isLocked: locked,
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

  void reset() {
    state = const PickupState();
  }
}

final pickupControllerProvider =
    StateNotifierProvider.autoDispose<PickupController, PickupState>(
  (ref) => PickupController(ref.watch(stationOperationsApiServiceProvider)),
  name: 'pickupController',
);
