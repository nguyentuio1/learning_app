import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/eta_estimation_model.dart';
import '../../../data/models/landing_station.dart';
import 'create_order_notifier.dart';
import 'my_orders_provider.dart';

class EtaEstimationState {
  const EtaEstimationState({
    this.estimate,
    this.isLoading = false,
    this.isRefreshing = false,
    this.errorMessage,
    this.accessLost = false,
    this.lastObservedStatus,
    this.telemetryStale = false,
  });

  final OrderEtaEstimate? estimate;
  final bool isLoading;
  final bool isRefreshing;
  final String? errorMessage;
  final bool accessLost;
  final String? lastObservedStatus;
  final bool telemetryStale;

  EtaEstimationState copyWith({
    OrderEtaEstimate? estimate,
    bool clearEstimate = false,
    bool? isLoading,
    bool? isRefreshing,
    String? errorMessage,
    bool clearError = false,
    bool? accessLost,
    String? lastObservedStatus,
    bool? telemetryStale,
  }) {
    return EtaEstimationState(
      estimate: clearEstimate ? null : (estimate ?? this.estimate),
      isLoading: isLoading ?? this.isLoading,
      isRefreshing: isRefreshing ?? this.isRefreshing,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      accessLost: accessLost ?? this.accessLost,
      lastObservedStatus: lastObservedStatus ?? this.lastObservedStatus,
      telemetryStale: telemetryStale ?? this.telemetryStale,
    );
  }
}

class EtaEstimationController extends Notifier<EtaEstimationState> {
  EtaEstimationController(this.orderId, {DateTime Function()? now})
    : _now = now ?? DateTime.now;

  final String orderId;
  final DateTime Function() _now;
  Timer? _freshnessTimer;
  DeliveryOrderDetailResponseDto? _lastDetail;
  int _refreshGeneration = 0;

  @override
  EtaEstimationState build() {
    ref.onDispose(_cancelFreshnessTimer);

    ref.listen<AsyncValue<DeliveryOrderDetailResponseDto>>(
      orderDetailProvider(orderId),
      (previous, next) {
        if (next.hasError) {
          final error = next.error;
          if (error != null && isAccessDeniedError(error)) {
            _cancelFreshnessTimer();
            _lastDetail = null;
            state = state.copyWith(
              clearEstimate: true,
              isLoading: false,
              isRefreshing: false,
              accessLost: true,
              errorMessage: accessDeniedMessage(error),
            );
            return;
          }
        }

        if (next.isLoading || next.isRefreshing) {
          if (!state.accessLost) {
            state = state.copyWith(
              isLoading: state.estimate == null,
              isRefreshing: state.estimate != null,
            );
          }
          return;
        }

        if (next.hasError) {
          final error = next.error;
          state = state.copyWith(
            isLoading: false,
            isRefreshing: false,
            errorMessage: error == null
                ? 'Unable to load ETA estimate. Please try again.'
                : etaErrorMessage(error),
          );
          return;
        }

        if (next.hasValue) {
          final detail = next.requireValue;
          _applyDetail(detail, telemetryStale: state.telemetryStale);
        }
      },
    );

    final initialAsync = ref.read(orderDetailProvider(orderId));
    if (initialAsync.hasError) {
      final error = initialAsync.error;
      if (error != null && isAccessDeniedError(error)) {
        return EtaEstimationState(
          accessLost: true,
          errorMessage: accessDeniedMessage(error),
        );
      }
      if (!initialAsync.isLoading && !initialAsync.isRefreshing) {
        return EtaEstimationState(
          errorMessage: error == null
              ? 'Unable to load ETA estimate. Please try again.'
              : etaErrorMessage(error),
        );
      }
    }

    if (initialAsync.hasValue &&
        !initialAsync.isLoading &&
        !initialAsync.isRefreshing &&
        !initialAsync.hasError) {
      final detail = initialAsync.requireValue;
      _lastDetail = detail;
      final estimate = OrderEtaEstimate.fromOrderDetail(
        detail,
        now: _now(),
      );
      _scheduleFreshnessDeadline(estimate);
      return EtaEstimationState(
        estimate: estimate,
        lastObservedStatus: estimate.orderStatus,
      );
    }

    return const EtaEstimationState(isLoading: true);
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

  static String etaErrorMessage(Object error) {
    if (isAccessDeniedError(error)) {
      return accessDeniedMessage(error);
    }
    if (error is ApiException && error.message.isNotEmpty) {
      return error.message;
    }
    return 'Unable to load ETA estimate. Please try again.';
  }

  void _applyDetail(
    DeliveryOrderDetailResponseDto detail, {
    required bool telemetryStale,
  }) {
    _lastDetail = detail;
    final estimate = OrderEtaEstimate.fromOrderDetail(
      detail,
      now: _now(),
      telemetryStale: telemetryStale,
    );
    _scheduleFreshnessDeadline(estimate);
    state = EtaEstimationState(
      estimate: estimate,
      isLoading: false,
      isRefreshing: false,
      accessLost: false,
      lastObservedStatus: estimate.orderStatus,
      telemetryStale: telemetryStale,
    );
  }

  void _cancelFreshnessTimer() {
    _freshnessTimer?.cancel();
    _freshnessTimer = null;
  }

  void _scheduleFreshnessDeadline(OrderEtaEstimate estimate) {
    _cancelFreshnessTimer();
    final estimatedArrival = estimate.estimatedDeliveryTime;
    if (!estimate.isLiveEstimateActive || estimatedArrival == null) {
      return;
    }

    final nowUtc = _now().toUtc();
    final deadline = estimatedArrival.toUtc().add(
      const Duration(milliseconds: 50),
    );
    final delay = deadline.difference(nowUtc);
    _freshnessTimer = Timer(
      delay.isNegative ? const Duration(milliseconds: 1) : delay,
      () {
        _freshnessTimer = null;
        if (!ref.mounted || state.accessLost) return;
        final detail = _lastDetail;
        if (detail != null) {
          final recomputed = OrderEtaEstimate.fromOrderDetail(
            detail,
            now: _now(),
            telemetryStale: state.telemetryStale,
          );
          state = state.copyWith(estimate: recomputed);
        }
      },
    );
  }

  /// Called when an external source (such as SignalR status updates or map/tracking state)
  /// observes an order status change or telemetry freshness change.
  void onExternalStatusObserved(
    String newStatus, {
    DateTime? updatedEstimatedDeliveryTime,
    bool? telemetryStale,
  }) {
    if (state.accessLost) return;
    final normalizedStatus = newStatus.trim().toUpperCase();
    final statusChanged =
        state.lastObservedStatus != null &&
        state.lastObservedStatus != normalizedStatus;
    final nextTelemetryStale = telemetryStale ?? state.telemetryStale;
    final staleChanged = nextTelemetryStale != state.telemetryStale;

    final currentDetail = _lastDetail;
    if (currentDetail != null && (statusChanged || staleChanged)) {
      final syntheticDetail = DeliveryOrderDetailResponseDto(
        id: currentDetail.id,
        trackingNumber: currentDetail.trackingNumber,
        recipientName: currentDetail.recipientName,
        recipientPhone: currentDetail.recipientPhone,
        originStationId: currentDetail.originStationId,
        destinationStationId: currentDetail.destinationStationId,
        status: normalizedStatus,
        statusLogs: currentDetail.statusLogs,
        packageId: currentDetail.packageId,
        deliveryAddressId: currentDetail.deliveryAddressId,
        cancelReason: currentDetail.cancelReason,
        createdAt: currentDetail.createdAt,
        updatedAt: currentDetail.updatedAt,
        scheduledDepartureTime: currentDetail.scheduledDepartureTime,
        actualDepartureTime: currentDetail.actualDepartureTime,
        estimatedDeliveryTime:
            updatedEstimatedDeliveryTime ?? currentDetail.estimatedDeliveryTime,
        actualDeliveryTime: currentDetail.actualDeliveryTime,
        package: currentDetail.package,
        originStation: currentDetail.originStation,
        destinationStation: currentDetail.destinationStation,
        deliveryAddress: currentDetail.deliveryAddress,
        aiEtaPrediction: currentDetail.aiEtaPrediction,
      );
      _applyDetail(syntheticDetail, telemetryStale: nextTelemetryStale);
    } else if (staleChanged) {
      state = state.copyWith(telemetryStale: nextTelemetryStale);
    }

    if (statusChanged) {
      unawaited(refreshEta());
    }
  }

  Future<bool> refreshEta() async {
    final generation = ++_refreshGeneration;
    if (!state.accessLost) {
      state = state.copyWith(
        isLoading: state.estimate == null,
        isRefreshing: state.estimate != null,
        clearError: true,
      );
    }
    ref.invalidate(orderDetailProvider(orderId));

    try {
      final detail = await ref.read(orderDetailProvider(orderId).future);
      if (!ref.mounted || generation != _refreshGeneration) return false;
      _applyDetail(detail, telemetryStale: state.telemetryStale);
      return true;
    } catch (error) {
      if (!ref.mounted || generation != _refreshGeneration) return false;
      if (isAccessDeniedError(error)) {
        _cancelFreshnessTimer();
        _lastDetail = null;
        state = state.copyWith(
          clearEstimate: true,
          isLoading: false,
          isRefreshing: false,
          accessLost: true,
          errorMessage: accessDeniedMessage(error),
        );
        return false;
      }
      state = state.copyWith(
        isLoading: false,
        isRefreshing: false,
        errorMessage: etaErrorMessage(error),
      );
      return false;
    }
  }
}

final etaEstimationControllerProvider = NotifierProvider.autoDispose
    .family<EtaEstimationController, EtaEstimationState, String>(
      EtaEstimationController.new,
      name: 'etaEstimationController',
    );

final routeEtaPreviewProvider = Provider.autoDispose<RouteEtaPreview?>((ref) {
  final orderState = ref.watch(createOrderNotifierProvider);
  final stationsAsync = ref.watch(orderStationsProvider);
  final package = orderState.package;
  if (package == null ||
      orderState.originStationId.isEmpty ||
      orderState.destinationStationId.isEmpty ||
      orderState.originStationId == orderState.destinationStationId) {
    return null;
  }

  return stationsAsync.maybeWhen(
    data: (stations) {
      LandingStation? origin;
      LandingStation? destination;
      for (final station in stations) {
        if (station.id == orderState.originStationId) {
          origin = station;
        } else if (station.id == orderState.destinationStationId) {
          destination = station;
        }
      }
      if (origin == null || destination == null) return null;
      return RouteEtaPreview.compute(
        originLatitude: origin.latitude,
        originLongitude: origin.longitude,
        destinationLatitude: destination.latitude,
        destinationLongitude: destination.longitude,
        weightGram: package.weightGram,
        isFragile: package.isFragile,
      );
    },
    orElse: () => null,
  );
}, name: 'routeEtaPreview');
