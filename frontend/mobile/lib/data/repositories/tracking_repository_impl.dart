import '../../domain/entities/telemetry_position.dart';
import '../../domain/entities/tracking_snapshot.dart';
import '../../domain/entities/tracking_status_update.dart';
import '../../domain/repositories/tracking_repository.dart';
import '../datasources/remote/tracking_remote_data_source.dart';
import '../datasources/remote/tracking_signalr_service.dart';

class TrackingRepositoryImpl implements TrackingRepository {
  const TrackingRepositoryImpl(this._remoteDataSource, this._signalRService);

  final TrackingRemoteDataSource _remoteDataSource;
  final TrackingSignalRService _signalRService;

  @override
  Stream<TelemetryPosition> get positions => _signalRService.positions;

  @override
  Stream<TrackingStatusUpdate> get statusUpdates =>
      _signalRService.statusUpdates;

  @override
  Stream<bool> get connectionStates => _signalRService.connectionStates;

  @override
  Future<TrackingSnapshot> getSnapshot(String orderId) =>
      _remoteDataSource.getSnapshot(orderId);

  @override
  Future<void> subscribe(String orderId) => _signalRService.subscribe(orderId);

  @override
  Future<void> unsubscribe() => _signalRService.unsubscribe();
}
