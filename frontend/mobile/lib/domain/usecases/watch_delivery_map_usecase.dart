import '../entities/delivery_map_snapshot.dart';
import '../repositories/delivery_map_repository.dart';

class WatchDeliveryMapUseCase {
  const WatchDeliveryMapUseCase(this._repository);

  final DeliveryMapRepository _repository;

  Stream<DeliveryMapUpdate> call(String orderId) =>
      _repository.watchOrderMap(orderId);
}
