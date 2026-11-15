import '../entities/tracking_snapshot.dart';
import '../repositories/tracking_repository.dart';

class GetTrackingSnapshotUseCase {
  const GetTrackingSnapshotUseCase(this._repository);

  final TrackingRepository _repository;

  Future<TrackingSnapshot> call(String orderId) =>
      _repository.getSnapshot(orderId);
}
