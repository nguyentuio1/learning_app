import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/station_operation_models.dart';
import '../../../data/services/station_operations_api_service.dart';
import '../pickup/pickup_controller.dart';

class IntakeState {
  const IntakeState({
    this.orderInfo,
    this.isLoading = false,
    this.isSubmitting = false,
    this.errorMessage,
    this.completedIntake,
    this.completedRejection,
  });

  final StationOrderInfo? orderInfo;
  final bool isLoading;
  final bool isSubmitting;
  final String? errorMessage;
  final PackageIntakeResponse? completedIntake;
  final RejectIntakeResponse? completedRejection;

  IntakeState copyWith({
    StationOrderInfo? orderInfo,
    bool clearOrderInfo = false,
    bool? isLoading,
    bool? isSubmitting,
    String? errorMessage,
    bool clearError = false,
    PackageIntakeResponse? completedIntake,
    bool clearCompletedIntake = false,
    RejectIntakeResponse? completedRejection,
    bool clearCompletedRejection = false,
  }) =>
      IntakeState(
        orderInfo: clearOrderInfo ? null : (orderInfo ?? this.orderInfo),
        isLoading: isLoading ?? this.isLoading,
        isSubmitting: isSubmitting ?? this.isSubmitting,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        completedIntake: clearCompletedIntake
            ? null
            : (completedIntake ?? this.completedIntake),
        completedRejection: clearCompletedRejection
            ? null
            : (completedRejection ?? this.completedRejection),
      );
}

class IntakeController extends Notifier<IntakeState> {
  @override
  IntakeState build() => const IntakeState();

  StationOperationsApiService get _apiService =>
      ref.read(stationOperationsApiServiceProvider);

  Future<void> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async {
    final normalized = PickupController.normalizeTrackingNumber(trackingNumber);
    if (normalized.isEmpty) {
      state = const IntakeState(
        errorMessage: 'Please enter a tracking number.',
      );
      return;
    }

    state = const IntakeState(isLoading: true);
    try {
      final info = await _apiService.lookupOrder(
        stationId: stationId,
        trackingNumber: normalized,
      );
      state = IntakeState(orderInfo: info);
    } on ApiException catch (error) {
      state = IntakeState(errorMessage: error.message);
    } catch (error) {
      state = IntakeState(errorMessage: error.toString());
    }
  }

  Future<bool> confirmIntake({
    required String stationId,
    required String trackingNumber,
    required String slotId,
    required double measuredWeightGram,
    required double measuredLengthCm,
    required double measuredWidthCm,
    required double measuredHeightCm,
    required bool visualInspectionPassed,
  }) async {
    final normalized = PickupController.normalizeTrackingNumber(trackingNumber);
    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final response = await _apiService.confirmIntake(
        stationId: stationId,
        request: PackageIntakeRequest(
          trackingNumber: normalized,
          slotId: slotId,
          measuredWeightGram: measuredWeightGram,
          measuredLengthCm: measuredLengthCm,
          measuredWidthCm: measuredWidthCm,
          measuredHeightCm: measuredHeightCm,
          visualInspectionPassed: visualInspectionPassed,
        ),
      );
      state = state.copyWith(
        isSubmitting: false,
        completedIntake: response,
      );
      return true;
    } on ApiException catch (error) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: error.message,
      );
      return false;
    } catch (error) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: error.toString(),
      );
      return false;
    }
  }

  Future<bool> rejectIntake({
    required String stationId,
    required String trackingNumber,
    required String reason,
  }) async {
    final trimmedReason = reason.trim();
    if (trimmedReason.isEmpty) {
      state = state.copyWith(
        errorMessage: 'Please provide a rejection reason.',
      );
      return false;
    }

    final normalized = PickupController.normalizeTrackingNumber(trackingNumber);
    state = state.copyWith(isSubmitting: true, clearError: true);
    try {
      final response = await _apiService.rejectIntake(
        stationId: stationId,
        request: RejectIntakeRequest(
          trackingNumber: normalized,
          reason: trimmedReason,
        ),
      );
      state = state.copyWith(
        isSubmitting: false,
        completedRejection: response,
      );
      return true;
    } on ApiException catch (error) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: error.message,
      );
      return false;
    } catch (error) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: error.toString(),
      );
      return false;
    }
  }

  void reset() {
    state = const IntakeState();
  }
}

final intakeControllerProvider =
    NotifierProvider<IntakeController, IntakeState>(
  IntakeController.new,
  name: 'intakeController',
);
