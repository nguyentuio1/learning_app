import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/delivery_map_snapshot.dart';
import 'package:smart_drone_delivery/domain/repositories/delivery_map_repository.dart';
import 'package:smart_drone_delivery/presentation/customer/delivery_map/delivery_map_controller.dart';

const _orderId = 'order-1';
final _initialPosition = DroneMapTelemetry(
  latitude: 10.8,
  longitude: 106.7,
  altitudeMeters: 80,
  speedMps: 11,
  batteryPercentage: 70,
  timestamp: DateTime.utc(2026, 11, 15, 8),
);
final _newPosition = DroneMapTelemetry(
  latitude: 10.81,
  longitude: 106.71,
  altitudeMeters: 82,
  speedMps: 12,
  batteryPercentage: 69,
  timestamp: DateTime.utc(2026, 11, 15, 8, 1),
);
final _oldMissionPosition = DroneMapTelemetry(
  latitude: 10.79,
  longitude: 106.69,
  altitudeMeters: 75,
  speedMps: 9,
  batteryPercentage: 72,
  timestamp: DateTime.utc(2026, 11, 15, 7, 50),
);
final _boundarySnapshotPosition = DroneMapTelemetry(
  latitude: 10.8,
  longitude: 106.7,
  altitudeMeters: 79,
  speedMps: 10,
  batteryPercentage: 71,
  timestamp: DateTime.utc(2026, 11, 15, 8),
);

DeliveryMapSnapshot _snapshot({
  DroneMapTelemetry? position,
  bool includePosition = false,
  bool stale = false,
  DeliveryMapOrderStatus status = DeliveryMapOrderStatus.inTransit,
}) => DeliveryMapSnapshot(
  orderId: _orderId,
  trackingNumber: 'TRACK-1',
  status: status,
  originStation: const MapStationPoint(
    id: 'origin',
    name: 'Origin',
    latitude: 10.77,
    longitude: 106.7,
  ),
  destinationStation: const MapStationPoint(
    id: 'destination',
    name: 'Destination',
    latitude: 10.85,
    longitude: 106.77,
  ),
  estimatedDeliveryTime: null,
  lastPosition: includePosition ? (position ?? _initialPosition) : null,
  telemetryStale: stale,
);

class _FakeDeliveryMapRepository implements DeliveryMapRepository {
  _FakeDeliveryMapRepository(this.initialSnapshot);

  final DeliveryMapSnapshot initialSnapshot;
  final updates = StreamController<DeliveryMapUpdate>.broadcast();
  int snapshotRequests = 0;
  final pendingSnapshots = <Completer<DeliveryMapSnapshot>>[];
  final queuedSnapshots = <DeliveryMapSnapshot>[];
  int activeSnapshotRequests = 0;
  int maximumConcurrentSnapshotRequests = 0;
  bool get hasStreamListener => updates.hasListener;

  @override
  Future<DeliveryMapSnapshot> getMapSnapshot(String orderId) async {
    snapshotRequests++;
    activeSnapshotRequests++;
    if (activeSnapshotRequests > maximumConcurrentSnapshotRequests) {
      maximumConcurrentSnapshotRequests = activeSnapshotRequests;
    }
    try {
      if (pendingSnapshots.isNotEmpty) {
        return await pendingSnapshots.removeAt(0).future;
      }
      if (queuedSnapshots.isNotEmpty) return queuedSnapshots.removeAt(0);
      return initialSnapshot;
    } finally {
      activeSnapshotRequests--;
    }
  }

  @override
  Stream<DeliveryMapUpdate> watchOrderMap(String orderId) => updates.stream;

  void emit(DeliveryMapUpdate update) => updates.add(update);
}

void main() {
  testWidgets('accepts only strictly newer position telemetry', (tester) async {
    final repository = _FakeDeliveryMapRepository(
      _snapshot(includePosition: true),
    );
    final container = ProviderContainer(
      overrides: [deliveryMapRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);
    final subscription = container.listen(
      deliveryMapControllerProvider(_orderId),
      (_, _) {},
    );
    addTearDown(subscription.close);
    await container.read(deliveryMapControllerProvider(_orderId).future);

    repository.emit(
      DeliveryMapUpdate(
        type: DeliveryMapUpdateType.position,
        position: _newPosition,
      ),
    );
    await tester.pump();
    expect(
      container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot
          .lastPosition
          ?.timestamp,
      _newPosition.timestamp,
    );

    for (final timestamp in [
      _newPosition.timestamp,
      _initialPosition.timestamp,
    ]) {
      repository.emit(
        DeliveryMapUpdate(
          type: DeliveryMapUpdateType.position,
          position: DroneMapTelemetry(
            latitude: 10.79,
            longitude: 106.69,
            altitudeMeters: 75,
            speedMps: 9,
            batteryPercentage: 68,
            timestamp: timestamp,
          ),
        ),
      );
      await tester.pump();
      expect(
        container
            .read(deliveryMapControllerProvider(_orderId))
            .requireValue
            .snapshot
            .lastPosition
            ?.latitude,
        _newPosition.latitude,
      );
    }
  });

  testWidgets('does not invent a position for missing or stale telemetry', (
    tester,
  ) async {
    for (final snapshot in [
      _snapshot(),
      _snapshot(stale: true),
      _snapshot(includePosition: true, stale: true),
    ]) {
      final repository = _FakeDeliveryMapRepository(snapshot);
      final container = ProviderContainer(
        overrides: [
          deliveryMapRepositoryProvider.overrideWithValue(repository),
        ],
      );
      final subscription = container.listen(
        deliveryMapControllerProvider(_orderId),
        (_, _) {},
      );
      await container.read(deliveryMapControllerProvider(_orderId).future);
      final current = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      if (snapshot.lastPosition == null) {
        expect(current.lastPosition, isNull);
      } else {
        expect(current.lastPosition, snapshot.lastPosition);
        expect(current.telemetryStale, isTrue);
      }

      subscription.close();
      container.dispose();
    }
  });

  testWidgets('refreshes on status changes and polls with a single request', (
    tester,
  ) async {
    final repository = _FakeDeliveryMapRepository(
      _snapshot(includePosition: true),
    );
    final container = ProviderContainer(
      overrides: [deliveryMapRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);
    final subscription = container.listen(
      deliveryMapControllerProvider(_orderId),
      (_, _) {},
    );
    addTearDown(subscription.close);
    await container.read(deliveryMapControllerProvider(_orderId).future);

    repository.emit(
      const DeliveryMapUpdate(
        type: DeliveryMapUpdateType.statusChanged,
        orderId: _orderId,
      ),
    );
    await tester.pump();
    expect(repository.snapshotRequests, 2);

    final pendingSnapshot = Completer<DeliveryMapSnapshot>();
    repository.pendingSnapshots.add(pendingSnapshot);
    repository.emit(
      const DeliveryMapUpdate(type: DeliveryMapUpdateType.reconnecting),
    );
    await tester.pump();
    expect(
      container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .liveUnavailable,
      isTrue,
    );
    await tester.pump(const Duration(seconds: 5));
    expect(repository.snapshotRequests, 3);
    await tester.pump(const Duration(seconds: 10));
    expect(repository.snapshotRequests, 3);

    pendingSnapshot.complete(_snapshot());
    await tester.pump();
    repository.emit(
      const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
    );
    await tester.pump();
    expect(
      container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .liveUnavailable,
      isFalse,
    );
    expect(repository.snapshotRequests, 4);
    await tester.pump(const Duration(seconds: 5));
    expect(repository.snapshotRequests, 4);
    subscription.close();
    container.dispose();
    await tester.pump();
  });

  testWidgets('cancels the polling timer when the provider is disposed', (
    tester,
  ) async {
    final repository = _FakeDeliveryMapRepository(_snapshot());
    final container = ProviderContainer(
      overrides: [deliveryMapRepositoryProvider.overrideWithValue(repository)],
    );
    final subscription = container.listen(
      deliveryMapControllerProvider(_orderId),
      (_, _) {},
    );
    await container.read(deliveryMapControllerProvider(_orderId).future);
    repository.emit(
      const DeliveryMapUpdate(type: DeliveryMapUpdateType.reconnecting),
    );
    await tester.pump();
    subscription.close();
    await tester.pump(const Duration(seconds: 5));
    expect(repository.snapshotRequests, 1);
    container.dispose();
  });

  testWidgets(
    'refreshes idle connected telemetry and marks aged position events stale',
    (tester) async {
      final now = DateTime.now().toUtc();
      final freshPosition = DroneMapTelemetry(
        latitude: 10.8,
        longitude: 106.7,
        altitudeMeters: 80,
        speedMps: 11,
        batteryPercentage: 70,
        timestamp: now.subtract(const Duration(seconds: 10)),
      );
      final repository =
          _FakeDeliveryMapRepository(
              _snapshot(position: freshPosition, includePosition: true),
            )
            ..queuedSnapshots.addAll([
              _snapshot(position: freshPosition, includePosition: true),
              _snapshot(position: freshPosition, includePosition: true),
              _snapshot(stale: true),
            ]);
      final container = ProviderContainer(
        overrides: [
          deliveryMapRepositoryProvider.overrideWithValue(repository),
        ],
      );
      addTearDown(container.dispose);
      final subscription = container.listen(
        deliveryMapControllerProvider(_orderId),
        (_, _) {},
      );
      addTearDown(subscription.close);
      await container.read(deliveryMapControllerProvider(_orderId).future);

      repository.emit(
        const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
      );
      await tester.pump();
      expect(repository.snapshotRequests, 2);

      await tester.pump(const Duration(seconds: 61));
      await tester.pump();
      expect(repository.snapshotRequests, 3);
      expect(
        container
            .read(deliveryMapControllerProvider(_orderId))
            .requireValue
            .snapshot
            .telemetryStale,
        isTrue,
      );

      final agedPosition = DroneMapTelemetry(
        latitude: 10.81,
        longitude: 106.71,
        altitudeMeters: 82,
        speedMps: 12,
        batteryPercentage: 69,
        timestamp: DateTime.now().toUtc().subtract(const Duration(seconds: 61)),
      );
      repository.emit(
        DeliveryMapUpdate(
          type: DeliveryMapUpdateType.position,
          position: agedPosition,
        ),
      );
      await tester.pump();
      final current = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      expect(current.lastPosition?.timestamp, agedPosition.timestamp);
      expect(current.telemetryStale, isTrue);
      subscription.close();
      container.dispose();
      await tester.pump();
    },
  );

  testWidgets(
    'queues mission refresh behind an in-flight snapshot without overlap',
    (tester) async {
      final repository = _FakeDeliveryMapRepository(
        _snapshot(includePosition: true),
      );
      final firstPending = Completer<DeliveryMapSnapshot>();
      final secondPending = Completer<DeliveryMapSnapshot>();
      final container = ProviderContainer(
        overrides: [
          deliveryMapRepositoryProvider.overrideWithValue(repository),
        ],
      );
      addTearDown(container.dispose);
      final subscription = container.listen(
        deliveryMapControllerProvider(_orderId),
        (_, _) {},
      );
      addTearDown(subscription.close);
      await container.read(deliveryMapControllerProvider(_orderId).future);

      repository.pendingSnapshots.add(firstPending);
      repository.emit(
        const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
      );
      await tester.pump();
      expect(repository.snapshotRequests, 2);
      repository.pendingSnapshots.add(secondPending);
      repository.emit(
        const DeliveryMapUpdate(
          type: DeliveryMapUpdateType.statusChanged,
          orderId: _orderId,
        ),
      );
      await tester.pump();
      expect(repository.snapshotRequests, 2);

      firstPending.complete(_snapshot(includePosition: true));
      await tester.pump();
      await tester.pump();
      expect(repository.snapshotRequests, 3);
      expect(repository.maximumConcurrentSnapshotRequests, 1);

      secondPending.complete(_snapshot(stale: true));
      await tester.pump();
      await tester.pump();
      final result = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      expect(result.lastPosition, isNull);
      expect(result.telemetryStale, isTrue);
      expect(repository.maximumConcurrentSnapshotRequests, 1);
      subscription.close();
      container.dispose();
      await tester.pump();
    },
  );

  testWidgets('keeps a newer stream position over a slow older HTTP snapshot', (
    tester,
  ) async {
    final repository = _FakeDeliveryMapRepository(
      _snapshot(includePosition: true),
    );
    final pending = Completer<DeliveryMapSnapshot>();
    final container = ProviderContainer(
      overrides: [deliveryMapRepositoryProvider.overrideWithValue(repository)],
    );
    addTearDown(container.dispose);
    final subscription = container.listen(
      deliveryMapControllerProvider(_orderId),
      (_, _) {},
    );
    addTearDown(subscription.close);
    await container.read(deliveryMapControllerProvider(_orderId).future);

    repository.pendingSnapshots.add(pending);
    repository.emit(
      const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
    );
    await tester.pump();
    repository.emit(
      DeliveryMapUpdate(
        type: DeliveryMapUpdateType.position,
        position: _newPosition,
      ),
    );
    await tester.pump();
    pending.complete(_snapshot(includePosition: true));
    await tester.pump();
    await tester.pump();

    expect(
      container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot
          .lastPosition
          ?.timestamp,
      _newPosition.timestamp,
    );
    subscription.close();
    container.dispose();
    await tester.pump();
  });

  testWidgets('does not open the stream after disposal during initial GET', (
    tester,
  ) async {
    final repository = _FakeDeliveryMapRepository(_snapshot());
    final pending = Completer<DeliveryMapSnapshot>();
    repository.pendingSnapshots.add(pending);
    final container = ProviderContainer(
      overrides: [deliveryMapRepositoryProvider.overrideWithValue(repository)],
    );
    container.listen(deliveryMapControllerProvider(_orderId), (_, _) {});
    await tester.pump();
    expect(repository.snapshotRequests, 1);

    container.dispose();
    pending.complete(_snapshot());
    await tester.pump();
    await tester.pump();
    expect(repository.hasStreamListener, isFalse);
  });

  testWidgets(
    'preserves server stale authority for null and slow-device timestamps',
    (tester) async {
      final futurePosition = DroneMapTelemetry(
        latitude: 10.81,
        longitude: 106.71,
        altitudeMeters: 82,
        speedMps: 12,
        batteryPercentage: 69,
        timestamp: DateTime.now().toUtc().add(const Duration(seconds: 61)),
      );
      final repository = _FakeDeliveryMapRepository(_snapshot(stale: false))
        ..queuedSnapshots.addAll([
          _snapshot(stale: false),
          _snapshot(stale: false),
          _snapshot(
            position: futurePosition,
            includePosition: true,
            stale: true,
          ),
        ]);
      final container = ProviderContainer(
        overrides: [
          deliveryMapRepositoryProvider.overrideWithValue(repository),
        ],
      );
      final subscription = container.listen(
        deliveryMapControllerProvider(_orderId),
        (_, _) {},
      );
      await container.read(deliveryMapControllerProvider(_orderId).future);

      final initial = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      expect(initial.lastPosition, isNull);
      expect(initial.telemetryStale, isFalse);

      repository.emit(
        const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
      );
      await tester.pump();
      await tester.pump();
      final nullPositionSnapshot = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      expect(nullPositionSnapshot.lastPosition, isNull);
      expect(nullPositionSnapshot.telemetryStale, isFalse);

      repository.emit(
        const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
      );
      await tester.pump();
      final refreshed = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      expect(refreshed.lastPosition, futurePosition);
      expect(refreshed.telemetryStale, isTrue);

      subscription.close();
      container.dispose();
      await tester.pump();
    },
  );

  testWidgets(
    'keeps post-boundary stream position over an older StatusChanged response',
    (tester) async {
      final repository = _FakeDeliveryMapRepository(
        _snapshot(position: _oldMissionPosition, includePosition: true),
      );
      final pending = Completer<DeliveryMapSnapshot>();
      final container = ProviderContainer(
        overrides: [
          deliveryMapRepositoryProvider.overrideWithValue(repository),
        ],
      );
      final subscription = container.listen(
        deliveryMapControllerProvider(_orderId),
        (_, _) {},
      );
      await container.read(deliveryMapControllerProvider(_orderId).future);

      repository.pendingSnapshots.add(pending);
      repository.emit(
        const DeliveryMapUpdate(
          type: DeliveryMapUpdateType.statusChanged,
          orderId: _orderId,
        ),
      );
      await tester.pump();
      expect(
        container
            .read(deliveryMapControllerProvider(_orderId))
            .requireValue
            .snapshot
            .lastPosition,
        isNull,
      );

      repository.emit(
        DeliveryMapUpdate(
          type: DeliveryMapUpdateType.position,
          position: _newPosition,
        ),
      );
      await tester.pump();
      pending.complete(
        _snapshot(position: _boundarySnapshotPosition, includePosition: true),
      );
      await tester.pump();
      await tester.pump();

      expect(
        container
            .read(deliveryMapControllerProvider(_orderId))
            .requireValue
            .snapshot
            .lastPosition
            ?.timestamp,
        _newPosition.timestamp,
      );
      subscription.close();
      container.dispose();
      await tester.pump();
    },
  );

  testWidgets(
    'respects canonical null after reconnect when StatusChanged was missed',
    (tester) async {
      final repository = _FakeDeliveryMapRepository(
        _snapshot(position: _oldMissionPosition, includePosition: true),
      );
      final pending = Completer<DeliveryMapSnapshot>();
      final container = ProviderContainer(
        overrides: [
          deliveryMapRepositoryProvider.overrideWithValue(repository),
        ],
      );
      final subscription = container.listen(
        deliveryMapControllerProvider(_orderId),
        (_, _) {},
      );
      await container.read(deliveryMapControllerProvider(_orderId).future);

      repository.emit(
        DeliveryMapUpdate(
          type: DeliveryMapUpdateType.position,
          position: _boundarySnapshotPosition,
        ),
      );
      await tester.pump();
      expect(
        container
            .read(deliveryMapControllerProvider(_orderId))
            .requireValue
            .snapshot
            .lastPosition,
        _boundarySnapshotPosition,
      );

      repository.emit(
        const DeliveryMapUpdate(type: DeliveryMapUpdateType.reconnecting),
      );
      repository.pendingSnapshots.add(pending);
      repository.emit(
        const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
      );
      await tester.pump();
      expect(repository.snapshotRequests, 2);

      pending.complete(_snapshot(stale: false));
      await tester.pump();
      await tester.pump();
      final snapshot = container
          .read(deliveryMapControllerProvider(_orderId))
          .requireValue
          .snapshot;
      expect(snapshot.lastPosition, isNull);
      expect(snapshot.telemetryStale, isFalse);

      subscription.close();
      container.dispose();
      await tester.pump();
    },
  );
}
