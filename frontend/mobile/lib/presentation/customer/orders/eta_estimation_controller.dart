import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/delivery_order_detail_response_dto.dart';
import '../../../data/models/eta_estimation_model.dart';
import '../../../data/models/landing_station.dart';
import '../../../data/providers/delivery_order_providers.dart';
import '../../../data/providers/repository_providers.dart';
import '../../../domain/entities/delivery_map_snapshot.dart';
import '../delivery_map/delivery_map_controller.dart';
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
  Timer? _countdownTimer;
  DeliveryOrderDetailResponseDto? _lastDetail;
  DateTime? _lastCanonicalEstimatedDeliveryTime;
  String? _lastCanonicalStatus;
  DateTime? _externalEstimatedDeliveryTime;
  Future<bool>? _inFlightRefresh;
  int _refreshGeneration = 0;

  @override
  EtaEstimationState build() {
    ref.onDispose(_cancelTimers);

    try {
      final trackingSub = ref
          .read(trackingRepositoryProvider)
          .statusUpdates
          .listen((update) {
            if (update.orderId == orderId && !state.accessLost) {
              onExternalStatusObserved(
                update.status.apiValue,
                forceRefresh: true,
              );
            }
          });
      ref.onDispose(() => unawaited(trackingSub.cancel()));
    } catch (_) {
      // Tracking repository is unavailable in isolated widget tests without AppConfig.
    }

    ref.listen<AsyncValue<DeliveryMapViewState>>(
      deliveryMapControllerProvider(orderId),
      (previous, next) {
        if (state.accessLost) return;
        if (!next.hasValue || next.isLoading || next.isRefreshing) return;
        final nextView = next.requireValue;
        final nextSnapshot = nextView.snapshot;
        final mapStatus = _mapOrderStatusToString(nextSnapshot.status);
        final prevView = (previous != null && previous.hasValue)
            ? previous.value
            : null;

        final currentEta =
            state.estimate?.estimatedDeliveryTime?.toUtc() ??
            _lastDetail?.estimatedDeliveryTime?.toUtc();
        final nextEta = nextSnapshot.estimatedDeliveryTime?.toUtc();
        final etaChanged =
            nextEta != null &&
            (currentEta == null || !currentEta.isAtSameMomentAs(nextEta));
        final staleChanged =
            nextSnapshot.telemetryStale != state.telemetryStale;

        if (prevView == null) {
          if (etaChanged || staleChanged) {
            onExternalStatusObserved(
              state.lastObservedStatus ?? mapStatus,
              updatedEstimatedDeliveryTime: nextSnapshot.estimatedDeliveryTime,
              telemetryStale: nextSnapshot.telemetryStale,
              forceRefresh: etaChanged,
            );
          }
          return;
        }

        final prevSnapshot = prevView.snapshot;
        final mapStatusChanged = prevSnapshot.status != nextSnapshot.status;
        final isPositionOnlyUpdate =
            nextSnapshot.lastPosition != null &&
            !identical(prevSnapshot.lastPosition, nextSnapshot.lastPosition) &&
            !mapStatusChanged &&
            !etaChanged;
        final isDisconnectOnlyUpdate =
            nextView.liveUnavailable &&
            !prevView.liveUnavailable &&
            !mapStatusChanged &&
            !etaChanged;

        if (isPositionOnlyUpdate || isDisconnectOnlyUpdate) {
          if (staleChanged) {
            onExternalStatusObserved(
              state.lastObservedStatus ?? mapStatus,
              updatedEstimatedDeliveryTime: nextSnapshot.estimatedDeliveryTime,
              telemetryStale: nextSnapshot.telemetryStale,
            );
          }
          return;
        }

        onExternalStatusObserved(
          mapStatusChanged
              ? mapStatus
              : (state.lastObservedStatus ?? mapStatus),
          updatedEstimatedDeliveryTime: nextSnapshot.estimatedDeliveryTime,
          telemetryStale: nextSnapshot.telemetryStale,
          forceRefresh: true,
        );
      },
    );

    ref.listen<AsyncValue<DeliveryOrderDetailResponseDto>>(
      orderDetailProvider(orderId),
      (previous, next) {
        if (next.hasError) {
          final error = next.error;
          if (error != null && isAccessDeniedError(error)) {
            _cancelTimers();
            _lastDetail = null;
            _externalEstimatedDeliveryTime = null;
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

        if (next.isLoading || next.isRefreshing) return;

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
          _applyCanonicalDetail(detail, telemetryStale: state.telemetryStale);
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
      _lastCanonicalEstimatedDeliveryTime = detail.estimatedDeliveryTime
          ?.toUtc();
      _lastCanonicalStatus = detail.status.trim().toUpperCase();
      final estimate = OrderEtaEstimate.fromOrderDetail(detail, now: _now());
      _scheduleFreshnessDeadline(estimate);
      return EtaEstimationState(
        estimate: estimate,
        lastObservedStatus: estimate.orderStatus,
      );
    }

    return const EtaEstimationState(isLoading: true);
  }

  static String _mapOrderStatusToString(DeliveryMapOrderStatus status) =>
      switch (status) {
        DeliveryMapOrderStatus.pending => 'PENDING',
        DeliveryMapOrderStatus.approved => 'APPROVED',
        DeliveryMapOrderStatus.readyForTakeoff => 'READY_FOR_TAKEOFF',
        DeliveryMapOrderStatus.inTransit => 'IN_TRANSIT',
        DeliveryMapOrderStatus.arrived => 'ARRIVED',
        DeliveryMapOrderStatus.delivered => 'DELIVERED',
        DeliveryMapOrderStatus.cancelled => 'CANCELLED',
        DeliveryMapOrderStatus.failed => 'FAILED',
        DeliveryMapOrderStatus.unknown => 'IN_TRANSIT',
      };

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

  void _applyCanonicalDetail(
    DeliveryOrderDetailResponseDto detail, {
    required bool telemetryStale,
  }) {
    final detailEta = detail.estimatedDeliveryTime?.toUtc();
    final detailStatus = detail.status.trim().toUpperCase();
    final canonicalChanged =
        detailEta != _lastCanonicalEstimatedDeliveryTime ||
        detailStatus != _lastCanonicalStatus;

    _lastCanonicalEstimatedDeliveryTime = detailEta;
    _lastCanonicalStatus = detailStatus;

    if (canonicalChanged) {
      _externalEstimatedDeliveryTime = null;
    }

    final effectiveEta =
        (!canonicalChanged && _externalEstimatedDeliveryTime != null)
        ? _externalEstimatedDeliveryTime
        : detail.estimatedDeliveryTime;

    final effectiveDetail = effectiveEta != detail.estimatedDeliveryTime
        ? _withDetailOverrides(
            detail,
            status: detail.status,
            estimatedDeliveryTime: effectiveEta,
          )
        : detail;

    _applyDetail(effectiveDetail, telemetryStale: telemetryStale);
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

  DeliveryOrderDetailResponseDto _withDetailOverrides(
    DeliveryOrderDetailResponseDto base, {
    required String status,
    required DateTime? estimatedDeliveryTime,
  }) => DeliveryOrderDetailResponseDto(
    id: base.id,
    trackingNumber: base.trackingNumber,
    recipientName: base.recipientName,
    recipientPhone: base.recipientPhone,
    originStationId: base.originStationId,
    destinationStationId: base.destinationStationId,
    status: status,
    statusLogs: base.statusLogs,
    packageId: base.packageId,
    deliveryAddressId: base.deliveryAddressId,
    cancelReason: base.cancelReason,
    createdAt: base.createdAt,
    updatedAt: base.updatedAt,
    scheduledDepartureTime: base.scheduledDepartureTime,
    actualDepartureTime: base.actualDepartureTime,
    estimatedDeliveryTime: estimatedDeliveryTime,
    actualDeliveryTime: base.actualDeliveryTime,
    package: base.package,
    originStation: base.originStation,
    destinationStation: base.destinationStation,
    deliveryAddress: base.deliveryAddress,
    aiEtaPrediction: base.aiEtaPrediction,
  );

  void _cancelTimers() {
    _freshnessTimer?.cancel();
    _freshnessTimer = null;
    _countdownTimer?.cancel();
    _countdownTimer = null;
  }

  void _scheduleFreshnessDeadline(OrderEtaEstimate estimate) {
    _cancelTimers();
    final estimatedArrival = estimate.estimatedDeliveryTime;
    if (!estimate.isLiveEstimateActive || estimatedArrival == null) {
      return;
    }

    final scheduledAtUtc = _now().toUtc();
    final deadline = estimatedArrival.toUtc().add(
      const Duration(milliseconds: 50),
    );
    final delay = deadline.difference(scheduledAtUtc);

    _countdownTimer = Timer.periodic(const Duration(minutes: 1), (timer) {
      if (!ref.mounted || state.accessLost) {
        timer.cancel();
        return;
      }
      final detail = _lastDetail;
      if (detail == null) return;
      final wallNow = _now().toUtc();
      final simulatedNow = scheduledAtUtc.add(Duration(minutes: timer.tick));
      final effectiveNow = wallNow.isAfter(simulatedNow)
          ? wallNow
          : simulatedNow;
      final recomputed = OrderEtaEstimate.fromOrderDetail(
        detail,
        now: effectiveNow,
        telemetryStale: state.telemetryStale,
      );
      state = state.copyWith(estimate: recomputed);
      if (!recomputed.isLiveEstimateActive) {
        timer.cancel();
      }
    });

    _freshnessTimer = Timer(
      delay.isNegative ? const Duration(milliseconds: 1) : delay,
      () {
        _freshnessTimer = null;
        _countdownTimer?.cancel();
        _countdownTimer = null;
        if (!ref.mounted || state.accessLost) return;
        final detail = _lastDetail;
        if (detail != null) {
          final currentNow = _now().toUtc();
          final effectiveNow = currentNow.isBefore(deadline)
              ? deadline
              : currentNow;
          final recomputed = OrderEtaEstimate.fromOrderDetail(
            detail,
            now: effectiveNow,
            telemetryStale: state.telemetryStale,
          );
          state = state.copyWith(estimate: recomputed);
        }
      },
    );
  }

  /// Called when an external source (such as SignalR status updates or map/tracking state)
  /// observes an order status change, ETA correction, or telemetry freshness change.
  void onExternalStatusObserved(
    String newStatus, {
    DateTime? updatedEstimatedDeliveryTime,
    bool? telemetryStale,
    bool forceRefresh = false,
  }) {
    if (state.accessLost) return;
    final normalizedStatus = newStatus.trim().toUpperCase();
    final statusChanged =
        state.lastObservedStatus != null &&
        state.lastObservedStatus != normalizedStatus;
    final nextTelemetryStale = telemetryStale ?? state.telemetryStale;
    final staleChanged = nextTelemetryStale != state.telemetryStale;

    final normalizedEta = updatedEstimatedDeliveryTime?.toUtc();
    final currentEta =
        state.estimate?.estimatedDeliveryTime?.toUtc() ??
        _lastDetail?.estimatedDeliveryTime?.toUtc();
    final etaChanged =
        normalizedEta != null &&
        (currentEta == null || !currentEta.isAtSameMomentAs(normalizedEta));

    if (etaChanged) {
      _externalEstimatedDeliveryTime = normalizedEta;
    }

    final currentDetail = _lastDetail;
    if (currentDetail != null &&
        (statusChanged || staleChanged || etaChanged)) {
      final syntheticDetail = _withDetailOverrides(
        currentDetail,
        status: normalizedStatus,
        estimatedDeliveryTime:
            normalizedEta ?? currentDetail.estimatedDeliveryTime,
      );
      _applyDetail(syntheticDetail, telemetryStale: nextTelemetryStale);
    } else if (staleChanged) {
      state = state.copyWith(telemetryStale: nextTelemetryStale);
    }

    if (statusChanged || etaChanged || forceRefresh) {
      unawaited(refreshEta());
    }
  }

  Future<bool> refreshEta() {
    final existing = _inFlightRefresh;
    if (existing != null) return existing;
    final future = _performRefresh();
    _inFlightRefresh = future;
    future.whenComplete(() {
      Future<void>.delayed(Duration.zero, () {
        if (identical(_inFlightRefresh, future)) {
          _inFlightRefresh = null;
        }
      });
    });
    return future;
  }

  Future<bool> _performRefresh() async {
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
      _applyCanonicalDetail(detail, telemetryStale: state.telemetryStale);
      return true;
    } catch (error) {
      if (!ref.mounted || generation != _refreshGeneration) return false;
      if (isAccessDeniedError(error)) {
        _cancelTimers();
        _lastDetail = null;
        _externalEstimatedDeliveryTime = null;
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
