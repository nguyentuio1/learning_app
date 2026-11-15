import '../entities/delivery_map_snapshot.dart';
import '../repositories/delivery_map_repository.dart';

class GetDeliveryMapSnapshotUseCase {
  const GetDeliveryMapSnapshotUseCase(this._repository);

  final DeliveryMapRepository _repository;

  Future<DeliveryMapSnapshot> call(String orderId) =>
      _repository.getMapSnapshot(orderId);
}
