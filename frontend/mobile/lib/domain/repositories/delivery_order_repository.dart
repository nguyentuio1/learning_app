import '../entities/resolution_action.dart';

abstract interface class DeliveryOrderRepository {
  Future<void> markFailed({
    required String id,
    required String failureReason,
    required String failureType,
  });

  Future<void> resolveFailedDelivery({
    required String id,
    required ResolutionAction resolutionAction,
    required String resolutionNote,
  });
}
