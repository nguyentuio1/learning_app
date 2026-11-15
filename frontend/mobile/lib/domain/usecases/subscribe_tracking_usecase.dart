import '../repositories/tracking_repository.dart';

class SubscribeTrackingUseCase {
  const SubscribeTrackingUseCase(this._repository);

  final TrackingRepository _repository;

  Future<void> call(String orderId) => _repository.subscribe(orderId);

  Future<void> unsubscribe() => _repository.unsubscribe();
}
