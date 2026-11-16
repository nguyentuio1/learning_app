import '../../domain/entities/delivery_map_snapshot.dart';
import '../../domain/repositories/delivery_map_repository.dart';
import '../datasources/remote/delivery_map_remote_data_source.dart';
import '../datasources/remote/delivery_map_signalr_service.dart';

class DeliveryMapRepositoryImpl implements DeliveryMapRepository {
  const DeliveryMapRepositoryImpl(
    this._remoteDataSource,
    this._signalRServiceFactory,
  );

  final DeliveryMapRemoteDataSource _remoteDataSource;
  final DeliveryMapSignalRService Function() _signalRServiceFactory;

  @override
  Future<DeliveryMapSnapshot> getMapSnapshot(String orderId) =>
      _remoteDataSource.getMapSnapshot(orderId);

  @override
  Stream<DeliveryMapUpdate> watchOrderMap(String orderId) {
    final service = _signalRServiceFactory();
    return service.watchOrderMap(orderId).map((event) {
      if (event.position != null) {
        return DeliveryMapUpdate(
          type: DeliveryMapUpdateType.position,
          position: event.position,
        );
      }
      if (event.orderId != null) {
        return DeliveryMapUpdate(
          type: DeliveryMapUpdateType.statusChanged,
          orderId: event.orderId,
        );
      }
      return DeliveryMapUpdate(
        type: event.isConnected == true
            ? DeliveryMapUpdateType.connected
            : DeliveryMapUpdateType.reconnecting,
      );
    });
  }
}
