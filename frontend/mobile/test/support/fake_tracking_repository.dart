import 'dart:async';
import 'dart:collection';

import 'package:smart_drone_delivery/domain/entities/telemetry_position.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_snapshot.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_status_update.dart';
import 'package:smart_drone_delivery/domain/repositories/tracking_repository.dart';

TrackingSnapshot trackingSnapshot({
  DeliveryTrackingStatus status = DeliveryTrackingStatus.inTransit,
  TelemetryPosition? position,
  bool includePosition = true,
  bool telemetryStale = false,
}) => TrackingSnapshot(
  orderId: 'order-1',
  trackingNumber: 'DD-202611-0012',
  status: status,
  originStation: const TrackingStation(
    id: 'origin-1',
    name: 'District 1 Hub',
    latitude: 10.7769,
    longitude: 106.7009,
  ),
  destinationStation: const TrackingStation(
    id: 'destination-1',
    name: 'Thu Duc Hub',
    latitude: 10.8506,
    longitude: 106.7719,
  ),
  estimatedDeliveryTime: DateTime.utc(2026, 11, 12, 8, 24),
  lastPosition: position ?? (includePosition ? _defaultPosition() : null),
  telemetryStale: telemetryStale,
);

TelemetryPosition _defaultPosition() => TelemetryPosition(
  latitude: 10.8012,
  longitude: 106.7311,
  altitudeMeters: 80,
  speedMps: 11.5,
  batteryPercentage: 71,
  timestamp: DateTime.utc(2026, 11, 12, 8, 10, 5),
);

class FakeTrackingRepository implements TrackingRepository {
  final _positionEvents = StreamController<TelemetryPosition>.broadcast();
  final _statusEvents = StreamController<TrackingStatusUpdate>.broadcast();
  final _connectionEvents = StreamController<bool>.broadcast();
  final queuedSnapshots = Queue<Future<TrackingSnapshot> Function()>();
  final requests = <String>[];
  final subscriptions = <String>[];
  int unsubscribeCalls = 0;
  TrackingSnapshot response = trackingSnapshot();
  bool? connectOnSubscribe = true;

  @override
  Stream<TelemetryPosition> get positions => _positionEvents.stream;

  @override
  Stream<TrackingStatusUpdate> get statusUpdates => _statusEvents.stream;

  @override
  Stream<bool> get connectionStates => _connectionEvents.stream;

  @override
  Future<TrackingSnapshot> getSnapshot(String orderId) {
    requests.add(orderId);
    if (queuedSnapshots.isNotEmpty) return queuedSnapshots.removeFirst()();
    return Future.value(response);
  }

  @override
  Future<void> subscribe(String orderId) async {
    subscriptions.add(orderId);
    final connected = connectOnSubscribe;
    if (connected != null) _connectionEvents.add(connected);
  }

  @override
  Future<void> unsubscribe() async {
    unsubscribeCalls++;
  }

  void emitPosition(TelemetryPosition position) =>
      _positionEvents.add(position);

  void emitStatus(TrackingStatusUpdate update) => _statusEvents.add(update);

  void emitConnection(bool connected) => _connectionEvents.add(connected);

  Future<void> close() async {
    await Future.wait([
      _positionEvents.close(),
      _statusEvents.close(),
      _connectionEvents.close(),
    ]);
  }
}
