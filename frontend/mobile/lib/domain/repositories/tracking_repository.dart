import '../entities/telemetry_position.dart';
import '../entities/tracking_snapshot.dart';
import '../entities/tracking_status_update.dart';

abstract interface class TrackingRepository {
  Stream<TelemetryPosition> get positions;
  Stream<TrackingStatusUpdate> get statusUpdates;
  Stream<bool> get connectionStates;

  Future<TrackingSnapshot> getSnapshot(String orderId);
  Future<void> subscribe(String orderId);
  Future<void> unsubscribe();
}
