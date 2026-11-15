import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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

class OrderTrackingController extends AsyncNotifier<OrderTrackingState> {
  OrderTrackingController(this._orderId);

  final String _orderId;
  TrackingRepository? _repository;
  Timer? _pollingTimer;
  StreamSubscription<TelemetryPosition>? _positionSubscription;
  StreamSubscription<TrackingStatusUpdate>? _statusSubscription;
  StreamSubscription<bool>? _connectionSubscription;
  TelemetryPosition? _latestPosition;
  bool _pollInFlight = false;
  bool _disposed = false;
  int _snapshotRequestId = 0;

  @override
  Future<OrderTrackingState> build() async {
    _disposed = false;
    _latestPosition = null;
    ref.onDispose(_dispose);

    final snapshot = await GetTrackingSnapshotUseCase(
      ref.read(trackingRepositoryProvider),
    )(_orderId);
    if (!ref.mounted) {
      return OrderTrackingState(snapshot: snapshot, isReconnecting: false);
    }

    final repository = ref.read(trackingRepositoryProvider);
    _repository = repository;
    _listenForUpdates(repository);
    final result = OrderTrackingState(snapshot: snapshot, isReconnecting: true);
    Timer.run(() => unawaited(_subscribe()));
    return result;
  }

  Future<void> retry() async {
    state = const AsyncLoading();
    try {
      final snapshot = await GetTrackingSnapshotUseCase(
        ref.read(trackingRepositoryProvider),
      )(_orderId);
      if (!ref.mounted || _disposed) return;
      state = AsyncData(
        OrderTrackingState(snapshot: snapshot, isReconnecting: true),
      );
      _repository ??= ref.read(trackingRepositoryProvider);
      _listenForUpdates(_repository!);
      unawaited(_subscribe());
    } catch (error, stackTrace) {
      if (!ref.mounted || _disposed) return;
      state = AsyncError(error, stackTrace);
    }
  }

  void _listenForUpdates(TrackingRepository repository) {
    if (_positionSubscription != null) return;
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
    if (_disposed || !ref.mounted) return;
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
    if (_disposed || !ref.mounted || !state.hasValue) return;
    _latestPosition = position;
    final current = state.requireValue;
    state = AsyncData(
      current.copyWith(
        snapshot: current.snapshot.copyWith(
          lastPosition: position,
          telemetryStale: false,
        ),
      ),
    );
  }

  void _onStatusChanged(TrackingStatusUpdate update) {
    if (_disposed || !ref.mounted || update.orderId != _orderId) return;
    unawaited(_refreshSnapshot());
  }

  void _onConnectionStateChanged(bool connected) {
    if (_disposed || !ref.mounted) return;
    if (connected) {
      _pollingTimer?.cancel();
      _pollingTimer = null;
      _setReconnecting(false);
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
    if (_pollInFlight || _disposed || !ref.mounted) return;
    _pollInFlight = true;
    try {
      await _refreshSnapshot();
    } finally {
      _pollInFlight = false;
    }
  }

  Future<void> _refreshSnapshot() async {
    if (_disposed || !ref.mounted) return;
    final requestId = ++_snapshotRequestId;
    try {
      var snapshot = await GetTrackingSnapshotUseCase(_repository!)(_orderId);
      if (_disposed || !ref.mounted || requestId != _snapshotRequestId) return;

      final latestPosition = _latestPosition;
      final serverPosition = snapshot.lastPosition;
      if (snapshot.status == DeliveryTrackingStatus.inTransit &&
          latestPosition != null &&
          (serverPosition == null ||
              latestPosition.timestamp.isAfter(serverPosition.timestamp))) {
        snapshot = snapshot.copyWith(
          lastPosition: latestPosition,
          telemetryStale: false,
        );
      }

      final current = state.value;
      if (current != null) {
        state = AsyncData(current.copyWith(snapshot: snapshot));
      }
    } catch (error, stackTrace) {
      if (!_disposed && ref.mounted) {
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
  }

  void _setReconnecting(bool value) {
    final current = state.value;
    if (current == null || current.isReconnecting == value) return;
    state = AsyncData(current.copyWith(isReconnecting: value));
  }

  void _reportStreamError(Object error, StackTrace stackTrace) {
    if (_disposed) return;
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
    _pollingTimer?.cancel();
    _pollingTimer = null;
    _positionSubscription?.cancel();
    _statusSubscription?.cancel();
    _connectionSubscription?.cancel();
    final repository = _repository;
    if (repository != null) {
      unawaited(_unsubscribe(repository));
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

final orderTrackingControllerProvider = AsyncNotifierProvider.autoDispose
    .family<OrderTrackingController, OrderTrackingState, String>(
      OrderTrackingController.new,
      name: 'orderTrackingController',
    );
