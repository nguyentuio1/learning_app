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

DeliveryMapSnapshot _snapshot({
  DroneMapTelemetry? position,
  bool includePosition = false,
  bool stale = false,
}) => DeliveryMapSnapshot(
  orderId: _orderId,
  trackingNumber: 'TRACK-1',
  status: DeliveryMapOrderStatus.inTransit,
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
  Completer<DeliveryMapSnapshot>? pendingSnapshot;

  @override
  Future<DeliveryMapSnapshot> getMapSnapshot(String orderId) {
    snapshotRequests++;
    final pending = pendingSnapshot;
    if (pending != null) {
      pendingSnapshot = null;
      return pending.future;
    }
    return Future.value(initialSnapshot);
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
    repository.pendingSnapshot = pendingSnapshot;
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
}
