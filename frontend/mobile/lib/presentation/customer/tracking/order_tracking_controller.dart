import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/providers/repository_providers.dart';
import '../../../domain/entities/telemetry_position.dart';
import '../../../domain/entities/tracking_snapshot.dart';
import '../../../domain/entities/tracking_status_update.dart';
import '../../../domain/repositories/tracking_repository.dart';
import '../../../domain/usecases/get_tracking_snapshot_usecase.dart';
import '../../../domain/usecases/subscribe_tracking_usecase.dart';

class OrderTrackingState {
  const OrderTrackingState({
    required this.snapshot,
    required this.isReconnecting,
  });

  final TrackingSnapshot snapshot;
  final bool isReconnecting;

  OrderTrackingState copyWith({
    TrackingSnapshot? snapshot,
    bool? isReconnecting,
  }) => OrderTrackingState(
    snapshot: snapshot ?? this.snapshot,
    isReconnecting: isReconnecting ?? this.isReconnecting,
  );
}

class OrderTrackingController extends Notifier<AsyncValue<OrderTrackingState>> {
  OrderTrackingController(this._orderId, {DateTime Function()? now})
    : _now = now ?? DateTime.now;

  final String _orderId;
  final DateTime Function() _now;
  TrackingRepository? _repository;
  Timer? _pollingTimer;
  Timer? _freshnessTimer;
  StreamSubscription<TelemetryPosition>? _positionSubscription;
  StreamSubscription<TrackingStatusUpdate>? _statusSubscription;
  StreamSubscription<bool>? _connectionSubscription;
  TelemetryPosition? _latestPosition;
  bool _pollInFlight = false;
  bool _disposed = false;
  bool _trackingStopped = false;
  int _snapshotRequestId = 0;

  @override
  AsyncValue<OrderTrackingState> build() {
    _disposed = false;
    _trackingStopped = false;
    _latestPosition = null;
    ref.onDispose(_dispose);
    unawaited(_loadInitialSnapshot());
    return const AsyncLoading();
  }

  Future<void> _loadInitialSnapshot() async {
    try {
      final snapshot = await GetTrackingSnapshotUseCase(
        ref.read(trackingRepositoryProvider),
      )(_orderId);
      if (!ref.mounted || _disposed) return;
      final repository = ref.read(trackingRepositoryProvider);
      _repository = repository;
      _listenForUpdates(repository);
      state = AsyncData(
        OrderTrackingState(
          snapshot: _normalizeSnapshot(snapshot),
          isReconnecting: true,
        ),
      );
      Timer.run(() => unawaited(_subscribe()));
    } catch (error, stackTrace) {
      if (!ref.mounted || _disposed) return;
      if (_isProtectedAccessError(error)) _stopTracking();
      state = AsyncError(error, stackTrace);
    }
  }

  Future<void> retry() async {
    state = const AsyncLoading();
    try {
      final snapshot = await GetTrackingSnapshotUseCase(
        ref.read(trackingRepositoryProvider),
      )(_orderId);
      if (!ref.mounted || _disposed) return;
      _trackingStopped = false;
      state = AsyncData(
        OrderTrackingState(
          snapshot: _normalizeSnapshot(snapshot),
          isReconnecting: true,
        ),
      );
      _repository ??= ref.read(trackingRepositoryProvider);
      _listenForUpdates(_repository!);
      unawaited(_subscribe());
    } catch (error, stackTrace) {
      if (!ref.mounted || _disposed) return;
      if (_isProtectedAccessError(error)) {
        _stopTracking();
      }
      state = AsyncError(error, stackTrace);
    }
  }

  void _listenForUpdates(TrackingRepository repository) {
    if (_trackingStopped || _positionSubscription != null) return;
    _positionSubscription = repository.positions.listen(
      _onPositionUpdated,
      onError: (Object error, StackTrace stackTrace) =>
          _reportStreamError(error, stackTrace),
    );
    _statusSubscription = repository.statusUpdates.listen(
      _onStatusChanged,
      onError: (Object error, StackTrace stackTrace) =>
          _reportStreamError(error, stackTrace),
    );
    _connectionSubscription = repository.connectionStates.listen(
      _onConnectionStateChanged,
      onError: (Object error, StackTrace stackTrace) =>
          _reportStreamError(error, stackTrace),
    );
  }

  Future<void> _subscribe() async {
    if (_disposed || _trackingStopped || !ref.mounted) return;
    _setReconnecting(true);
    try {
      final repository = _repository;
      if (repository == null) return;
      await SubscribeTrackingUseCase(repository)(_orderId);
    } catch (_) {
      if (!_disposed && ref.mounted) _onConnectionStateChanged(false);
    }
  }

  void _onPositionUpdated(TelemetryPosition position) {
    if (_disposed || _trackingStopped || !ref.mounted || !state.hasValue) {
      return;
    }
    _latestPosition = position;
    _freshnessTimer?.cancel();
    _freshnessTimer = null;
    final current = state.requireValue;
    final snapshot = current.snapshot.copyWith(
      lastPosition: position,
      telemetryStale: _isTelemetryStale(
        current.snapshot.status,
        position,
        _now().toUtc(),
      ),
    );
    state = AsyncData(current.copyWith(snapshot: snapshot));
    _scheduleFreshnessTimer(snapshot);
  }

  void _onStatusChanged(TrackingStatusUpdate update) {
    if (_disposed ||
        _trackingStopped ||
        !ref.mounted ||
        update.orderId != _orderId) {
      return;
    }
    unawaited(_refreshSnapshot());
  }

  void _onConnectionStateChanged(bool connected) {
    if (_disposed || _trackingStopped || !ref.mounted) return;
    if (connected) {
      _pollingTimer?.cancel();
      _pollingTimer = null;
      _setReconnecting(false);
      final current = state.value;
      if (current != null) _scheduleFreshnessTimer(current.snapshot);
      unawaited(_refreshSnapshot());
      return;
    }
    _setReconnecting(true);
    _pollingTimer ??= Timer.periodic(
      const Duration(seconds: 5),
      (_) => _pollSnapshot(),
    );
  }

  Future<void> _pollSnapshot() async {
    if (_pollInFlight || _disposed || _trackingStopped || !ref.mounted) {
      return;
    }
    _pollInFlight = true;
    try {
      await _refreshSnapshot();
    } finally {
      _pollInFlight = false;
    }
  }

  Future<void> _refreshSnapshot() async {
    if (_disposed || _trackingStopped || !ref.mounted) return;
    final requestId = ++_snapshotRequestId;
    try {
      var snapshot = await GetTrackingSnapshotUseCase(_repository!)(_orderId);
      if (_disposed || !ref.mounted || requestId != _snapshotRequestId) return;

      final latestPosition = _latestPosition;
      final serverPosition = snapshot.lastPosition;
      if (serverPosition == null) {
        _latestPosition = null;
      } else if (snapshot.status == DeliveryTrackingStatus.inTransit &&
          latestPosition != null &&
          latestPosition.timestamp.isAfter(serverPosition.timestamp)) {
        snapshot = snapshot.copyWith(lastPosition: latestPosition);
      } else {
        _latestPosition = serverPosition;
      }

      snapshot = _normalizeSnapshot(snapshot);
      final current = state.value;
      if (current != null) {
        state = AsyncData(current.copyWith(snapshot: snapshot));
      }
      _scheduleFreshnessTimer(snapshot);
    } catch (error, stackTrace) {
      if (_disposed || !ref.mounted) return;
      if (_isProtectedAccessError(error)) {
        _stopTracking();
        state = AsyncError(error, stackTrace);
        return;
      }
      if (requestId != _snapshotRequestId) return;
      FlutterError.reportError(
        FlutterErrorDetails(
          exception: error,
          stack: stackTrace,
          library: 'order tracking',
          context: ErrorDescription('while refreshing a tracking snapshot'),
        ),
      );
    }
  }

  void _setReconnecting(bool value) {
    final current = state.value;
    if (current == null || current.isReconnecting == value) return;
    state = AsyncData(current.copyWith(isReconnecting: value));
  }

  void _reportStreamError(Object error, StackTrace stackTrace) {
    if (_disposed || _trackingStopped) return;
    FlutterError.reportError(
      FlutterErrorDetails(
        exception: error,
        stack: stackTrace,
        library: 'order tracking',
        context: ErrorDescription('while processing a live tracking event'),
      ),
    );
    _onConnectionStateChanged(false);
  }

  void _dispose() {
    _disposed = true;
    _cancelTimers();
    _cancelSubscriptions();
    final repository = _repository;
    if (repository != null) {
      unawaited(_unsubscribe(repository));
    }
  }

  TrackingSnapshot _normalizeSnapshot(TrackingSnapshot snapshot) {
    final position = snapshot.lastPosition;
    if (snapshot.status != DeliveryTrackingStatus.inTransit ||
        position == null) {
      _latestPosition = null;
    } else {
      final latest = _latestPosition;
      if (latest == null || !latest.timestamp.isAfter(position.timestamp)) {
        _latestPosition = position;
      }
    }
    return snapshot.copyWith(
      telemetryStale: _isTelemetryStale(
        snapshot.status,
        position,
        _now().toUtc(),
      ),
    );
  }

  bool _isTelemetryStale(
    DeliveryTrackingStatus status,
    TelemetryPosition? position,
    DateTime now,
  ) {
    if (status != DeliveryTrackingStatus.inTransit) return false;
    if (position == null) return true;
    return now.difference(position.timestamp.toUtc()) >
        const Duration(seconds: 60);
  }

  void _scheduleFreshnessTimer(TrackingSnapshot snapshot) {
    _freshnessTimer?.cancel();
    _freshnessTimer = null;
    if (_disposed ||
        _trackingStopped ||
        snapshot.status != DeliveryTrackingStatus.inTransit ||
        snapshot.lastPosition == null ||
        snapshot.telemetryStale) {
      return;
    }

    final deadline = snapshot.lastPosition!.timestamp.toUtc().add(
      const Duration(seconds: 60, milliseconds: 1),
    );
    final delay = deadline.difference(_now().toUtc());
    _freshnessTimer = Timer(
      delay.isNegative ? Duration.zero : delay,
      _onFreshnessDeadline,
    );
  }

  void _onFreshnessDeadline() {
    _freshnessTimer = null;
    if (_disposed || _trackingStopped || !ref.mounted || !state.hasValue) {
      return;
    }
    final current = state.requireValue;
    final snapshot = current.snapshot;
    final stale = _isTelemetryStale(
      snapshot.status,
      snapshot.lastPosition,
      _now().toUtc(),
    );
    if (snapshot.telemetryStale != stale) {
      state = AsyncData(
        current.copyWith(snapshot: snapshot.copyWith(telemetryStale: stale)),
      );
    }
    if (!stale) _scheduleFreshnessTimer(snapshot);
  }

  bool _isProtectedAccessError(Object error) =>
      error is ApiException &&
      (error.statusCode == 401 ||
          error.statusCode == 403 ||
          error.statusCode == 404);

  void _stopTracking() {
    if (_trackingStopped) return;
    _trackingStopped = true;
    _snapshotRequestId++;
    _cancelTimers();
    _cancelSubscriptions();
    final repository = _repository;
    if (repository != null) unawaited(_unsubscribe(repository));
  }

  void _cancelTimers() {
    _pollingTimer?.cancel();
    _pollingTimer = null;
    _freshnessTimer?.cancel();
    _freshnessTimer = null;
  }

  void _cancelSubscriptions() {
    final subscriptions = [
      _positionSubscription,
      _statusSubscription,
      _connectionSubscription,
    ];
    _positionSubscription = null;
    _statusSubscription = null;
    _connectionSubscription = null;
    for (final subscription in subscriptions) {
      if (subscription != null) unawaited(subscription.cancel());
    }
  }

  Future<void> _unsubscribe(TrackingRepository repository) async {
    try {
      await SubscribeTrackingUseCase(repository).unsubscribe();
    } catch (error, stackTrace) {
      FlutterError.reportError(
        FlutterErrorDetails(
          exception: error,
          stack: stackTrace,
          library: 'order tracking',
          context: ErrorDescription('while closing the tracking connection'),
        ),
      );
    }
  }
}

final orderTrackingControllerProvider = NotifierProvider.autoDispose
    .family<OrderTrackingController, AsyncValue<OrderTrackingState>, String>(
      OrderTrackingController.new,
      name: 'orderTrackingController',
    );
