import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/providers/repository_providers.dart';
import '../../../domain/entities/delivery_map_snapshot.dart';
import '../../../domain/usecases/get_delivery_map_snapshot_usecase.dart';
import '../../../domain/usecases/watch_delivery_map_usecase.dart';

final deliveryMapControllerProvider = AsyncNotifierProvider.autoDispose
    .family<DeliveryMapController, DeliveryMapViewState, String>(
      DeliveryMapController.new,
    );

class DeliveryMapViewState {
  const DeliveryMapViewState({
    required this.snapshot,
    this.liveUnavailable = false,
  });

  final DeliveryMapSnapshot snapshot;
  final bool liveUnavailable;
}

class DeliveryMapController extends AsyncNotifier<DeliveryMapViewState> {
  DeliveryMapController(this._currentOrderId);

  final String _currentOrderId;
  StreamSubscription<DeliveryMapUpdate>? _updates;
  Timer? _pollTimer;
  Timer? _idleRefreshTimer;
  bool _pollInFlight = false;
  bool _disposed = false;
  bool _snapshotRequestInFlight = false;
  bool _queuedRefresh = false;
  bool _signalrConnected = false;
  DroneMapTelemetry? _latestStreamPosition;
  int _missionGeneration = 0;
  int _latestStreamGeneration = -1;
  int _refreshRequestSeq = 0;
  int _latestStreamSeq = 0;

  @override
  Future<DeliveryMapViewState> build() async {
    _disposed = false;
    ref.onDispose(() {
      _disposed = true;
      _pollTimer?.cancel();
      _idleRefreshTimer?.cancel();
      unawaited(_updates?.cancel() ?? Future<void>.value());
    });

    final repository = ref.read(deliveryMapRepositoryProvider);
    final requestSeq = ++_refreshRequestSeq;
    final snapshot = await GetDeliveryMapSnapshotUseCase(repository)(
      _currentOrderId,
    );
    if (_disposed || !ref.mounted || requestSeq != _refreshRequestSeq) {
      return DeliveryMapViewState(snapshot: snapshot);
    }
    _updates = WatchDeliveryMapUseCase(repository)(_currentOrderId).listen(
      _handleUpdate,
      onError: (Object error, StackTrace stackTrace) {
        _onConnectionUnavailable();
      },
    );
    return DeliveryMapViewState(snapshot: snapshot);
  }

  void _handleUpdate(DeliveryMapUpdate update) {
    switch (update.type) {
      case DeliveryMapUpdateType.position:
        final position = update.position;
        if (position != null) _onPositionUpdated(position);
      case DeliveryMapUpdateType.statusChanged:
        if (update.orderId == _currentOrderId) {
          _missionGeneration++;
          _latestStreamPosition = null;
          _latestStreamGeneration = -1;
          final current = state.asData?.value;
          if (current != null) {
            state = AsyncData(
              DeliveryMapViewState(
                snapshot: _copyWithTelemetry(
                  current.snapshot,
                  lastPosition: null,
                  telemetryStale: current.snapshot.telemetryStale,
                ),
                liveUnavailable: current.liveUnavailable,
              ),
            );
          }
          unawaited(_refreshSnapshot());
        }
      case DeliveryMapUpdateType.connected:
        _signalrConnected = true;
        _pollTimer?.cancel();
        _pollTimer = null;
        _scheduleIdleRefresh();
        final current = state.asData?.value;
        if (current != null) {
          state = AsyncData(
            DeliveryMapViewState(
              snapshot: current.snapshot,
              liveUnavailable: false,
            ),
          );
        }
        unawaited(_refreshSnapshot());
      case DeliveryMapUpdateType.reconnecting:
        _signalrConnected = false;
        _idleRefreshTimer?.cancel();
        _idleRefreshTimer = null;
        _onConnectionUnavailable();
    }
  }

  void _onPositionUpdated(DroneMapTelemetry position) {
    final current = state.asData?.value;
    if (current == null) return;
    final currentPosition = current.snapshot.lastPosition;
    final latestStreamPosition = _latestStreamPosition;
    if (currentPosition != null &&
        !position.timestamp.isAfter(currentPosition.timestamp)) {
      return;
    }
    if (latestStreamPosition != null &&
        _latestStreamGeneration == _missionGeneration &&
        !position.timestamp.isAfter(latestStreamPosition.timestamp)) {
      return;
    }

    _latestStreamPosition = position;
    _latestStreamGeneration = _missionGeneration;
    _latestStreamSeq = _refreshRequestSeq;
    _scheduleIdleRefresh();
    final isAged = _isPositionAged(position);
    state = AsyncData(
      DeliveryMapViewState(
        snapshot: current.snapshot.copyWith(
          lastPosition: position,
          telemetryStale: isAged,
        ),
        liveUnavailable: current.liveUnavailable,
      ),
    );
  }

  void _scheduleIdleRefresh() {
    _idleRefreshTimer?.cancel();
    if (!_signalrConnected || _disposed) {
      _idleRefreshTimer = null;
      return;
    }
    _idleRefreshTimer = Timer(const Duration(seconds: 60), () {
      _idleRefreshTimer = null;
      if (_disposed || !_signalrConnected) return;
      unawaited(_refreshSnapshot());
      _scheduleIdleRefresh();
    });
  }

  void _onConnectionUnavailable() {
    if (_disposed) return;
    _signalrConnected = false;
    _idleRefreshTimer?.cancel();
    _idleRefreshTimer = null;
    final current = state.asData?.value;
    if (current != null) {
      state = AsyncData(
        DeliveryMapViewState(snapshot: current.snapshot, liveUnavailable: true),
      );
    }
    _pollTimer ??= Timer.periodic(
      const Duration(seconds: 5),
      (_) => unawaited(_pollSnapshot()),
    );
  }

  Future<void> _pollSnapshot() async {
    if (_disposed || _pollInFlight) return;
    _pollInFlight = true;
    try {
      await _refreshSnapshot();
    } finally {
      _pollInFlight = false;
    }
  }

  Future<void> _refreshSnapshot() async {
    if (_disposed || !ref.mounted) return;
    if (_snapshotRequestInFlight) {
      _queuedRefresh = true;
      return;
    }
    _snapshotRequestInFlight = true;
    final requestGeneration = _missionGeneration;
    final requestSeq = ++_refreshRequestSeq;
    try {
      final response = await GetDeliveryMapSnapshotUseCase(
        ref.read(deliveryMapRepositoryProvider),
      )(_currentOrderId);
      if (_disposed || !ref.mounted) return;
      if (requestGeneration != _missionGeneration) return;

      final snapshot = _snapshotForRefresh(response, requestSeq: requestSeq);
      final current = state.asData?.value;
      state = AsyncData(
        DeliveryMapViewState(
          snapshot: snapshot,
          liveUnavailable: current?.liveUnavailable ?? false,
        ),
      );
    } catch (error, stackTrace) {
      if (!_disposed && ref.mounted) {
        state = AsyncError(error, stackTrace);
        if (error is ApiException &&
            (error.statusCode == 401 ||
                error.statusCode == 403 ||
                error.statusCode == 404)) {
          _pollTimer?.cancel();
          _pollTimer = null;
          _idleRefreshTimer?.cancel();
          _idleRefreshTimer = null;
          _signalrConnected = false;
          _queuedRefresh = false;
          _latestStreamPosition = null;
          _latestStreamGeneration = -1;
          _missionGeneration++;
          unawaited(_updates?.cancel() ?? Future<void>.value());
          _updates = null;
        }
      }
    } finally {
      _snapshotRequestInFlight = false;
      if (!_disposed && ref.mounted && _queuedRefresh) {
        _queuedRefresh = false;
        unawaited(_refreshSnapshot());
      }
    }
  }

  DeliveryMapSnapshot _snapshotForRefresh(
    DeliveryMapSnapshot snapshot, {
    required int requestSeq,
  }) {
    if (snapshot.status != DeliveryMapOrderStatus.inTransit) {
      _latestStreamPosition = null;
      _latestStreamGeneration = -1;
      _latestStreamSeq = 0;
      return _copyWithTelemetry(
        snapshot,
        lastPosition: null,
        telemetryStale: snapshot.telemetryStale,
      );
    }

    final hasInFlightStream =
        _latestStreamPosition != null &&
        _latestStreamGeneration == _missionGeneration &&
        _latestStreamSeq >= requestSeq;
    final latest = hasInFlightStream ? _latestStreamPosition : null;
    if (snapshot.lastPosition == null) {
      if (latest == null) {
        _latestStreamPosition = null;
        _latestStreamGeneration = -1;
        _latestStreamSeq = 0;
        return _copyWithTelemetry(
          snapshot,
          lastPosition: null,
          telemetryStale: snapshot.telemetryStale,
        );
      }
      return _copyWithTelemetry(
        snapshot,
        lastPosition: latest,
        telemetryStale: _isPositionAged(latest),
      );
    }

    if (latest != null &&
        latest.timestamp.isAfter(snapshot.lastPosition!.timestamp)) {
      return _copyWithTelemetry(
        snapshot,
        lastPosition: latest,
        telemetryStale: _isPositionAged(latest),
      );
    }
    _latestStreamPosition = snapshot.lastPosition;
    _latestStreamGeneration = _missionGeneration;
    _latestStreamSeq = requestSeq - 1;
    return _copyWithTelemetry(
      snapshot,
      lastPosition: snapshot.lastPosition,
      telemetryStale:
          snapshot.telemetryStale || _isPositionAged(snapshot.lastPosition!),
    );
  }

  bool _isPositionAged(DroneMapTelemetry position) =>
      DateTime.now().toUtc().difference(position.timestamp.toUtc()) >
      const Duration(seconds: 60);

  DeliveryMapSnapshot _copyWithTelemetry(
    DeliveryMapSnapshot snapshot, {
    required DroneMapTelemetry? lastPosition,
    required bool telemetryStale,
  }) => DeliveryMapSnapshot(
    orderId: snapshot.orderId,
    trackingNumber: snapshot.trackingNumber,
    status: snapshot.status,
    originStation: snapshot.originStation,
    destinationStation: snapshot.destinationStation,
    estimatedDeliveryTime: snapshot.estimatedDeliveryTime,
    lastPosition: lastPosition,
    telemetryStale: telemetryStale,
  );
}
