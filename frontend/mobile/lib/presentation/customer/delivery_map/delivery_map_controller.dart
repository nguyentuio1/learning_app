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
  bool _queuedMissionBoundary = false;
  bool _signalrConnected = false;
  DroneMapTelemetry? _latestStreamPosition;
  int _streamGeneration = 0;

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
    final snapshot = await GetDeliveryMapSnapshotUseCase(repository)(
      _currentOrderId,
    );
    if (_disposed || !ref.mounted) {
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
          unawaited(_refreshSnapshot(isMissionBoundary: true));
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
        !position.timestamp.isAfter(latestStreamPosition.timestamp)) {
      return;
    }

    _latestStreamPosition = position;
    _scheduleIdleRefresh();
    final isAged =
        DateTime.now().toUtc().difference(position.timestamp.toUtc()) >
        const Duration(seconds: 60);
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

  Future<void> _refreshSnapshot({bool isMissionBoundary = false}) async {
    if (_disposed || !ref.mounted) return;
    if (isMissionBoundary) {
      _streamGeneration++;
      _latestStreamPosition = null;
    }
    if (_snapshotRequestInFlight) {
      _queuedRefresh = true;
      _queuedMissionBoundary = _queuedMissionBoundary || isMissionBoundary;
      return;
    }
    _snapshotRequestInFlight = true;
    final requestGeneration = _streamGeneration;
    try {
      final response = await GetDeliveryMapSnapshotUseCase(
        ref.read(deliveryMapRepositoryProvider),
      )(_currentOrderId);
      if (_disposed || !ref.mounted) return;
      if (requestGeneration != _streamGeneration) return;

      final snapshot = _snapshotForRefresh(
        response,
        isMissionBoundary: isMissionBoundary,
      );
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
          _queuedMissionBoundary = false;
          _latestStreamPosition = null;
          _streamGeneration++;
          unawaited(_updates?.cancel() ?? Future<void>.value());
          _updates = null;
        }
      }
    } finally {
      _snapshotRequestInFlight = false;
      if (!_disposed && ref.mounted && _queuedRefresh) {
        _queuedRefresh = false;
        final nextBoundary = _queuedMissionBoundary;
        _queuedMissionBoundary = false;
        unawaited(_refreshSnapshot(isMissionBoundary: nextBoundary));
      }
    }
  }

  DeliveryMapSnapshot _snapshotForRefresh(
    DeliveryMapSnapshot snapshot, {
    required bool isMissionBoundary,
  }) {
    if (snapshot.status != DeliveryMapOrderStatus.inTransit ||
        snapshot.lastPosition == null) {
      return _copyWithTelemetry(
        snapshot,
        lastPosition: null,
        telemetryStale: snapshot.status == DeliveryMapOrderStatus.inTransit,
      );
    }

    if (isMissionBoundary) return snapshot;

    final latest = _latestStreamPosition;
    if (latest != null &&
        latest.timestamp.isAfter(snapshot.lastPosition!.timestamp)) {
      final isAged =
          DateTime.now().toUtc().difference(latest.timestamp.toUtc()) >
          const Duration(seconds: 60);
      return _copyWithTelemetry(
        snapshot,
        lastPosition: latest,
        telemetryStale: isAged,
      );
    }
    return snapshot;
  }

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
