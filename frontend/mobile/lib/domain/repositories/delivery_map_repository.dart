import '../entities/delivery_map_snapshot.dart';

abstract interface class DeliveryMapRepository {
  Future<DeliveryMapSnapshot> getMapSnapshot(String orderId);

  Stream<DeliveryMapUpdate> watchOrderMap(String orderId);
}
