import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

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
  bool _pollInFlight = false;
  bool _disposed = false;
  bool _snapshotRequestInFlight = false;

  @override
  Future<DeliveryMapViewState> build() async {
    _disposed = false;
    ref.onDispose(() {
      _disposed = true;
      _pollTimer?.cancel();
      unawaited(_updates?.cancel() ?? Future<void>.value());
    });

    final repository = ref.read(deliveryMapRepositoryProvider);
    final snapshot = await GetDeliveryMapSnapshotUseCase(repository)(
      _currentOrderId,
    );
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
        final current = state.asData?.value;
        final position = update.position;
        if (current == null || position == null) return;
        final currentPosition = current.snapshot.lastPosition;
        if (currentPosition != null &&
            !position.timestamp.isAfter(currentPosition.timestamp)) {
          return;
        }
        state = AsyncData(
          DeliveryMapViewState(
            snapshot: current.snapshot.copyWith(
              lastPosition: position,
              telemetryStale: false,
            ),
            liveUnavailable: current.liveUnavailable,
          ),
        );
      case DeliveryMapUpdateType.statusChanged:
        if (update.orderId == _currentOrderId) unawaited(_refreshSnapshot());
      case DeliveryMapUpdateType.connected:
        _pollTimer?.cancel();
        _pollTimer = null;
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
        _onConnectionUnavailable();
    }
  }

  void _onConnectionUnavailable() {
    if (_disposed) return;
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
    if (_disposed || _snapshotRequestInFlight) return;
    _snapshotRequestInFlight = true;
    try {
      final snapshot = await GetDeliveryMapSnapshotUseCase(
        ref.read(deliveryMapRepositoryProvider),
      )(_currentOrderId);
      if (_disposed) return;
      final current = state.asData?.value;
      state = AsyncData(
        DeliveryMapViewState(
          snapshot: snapshot,
          liveUnavailable: current?.liveUnavailable ?? false,
        ),
      );
    } catch (error, stackTrace) {
      if (!_disposed) state = AsyncError(error, stackTrace);
    } finally {
      _snapshotRequestInFlight = false;
    }
  }
}
