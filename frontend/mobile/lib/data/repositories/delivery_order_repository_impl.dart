import '../../domain/entities/resolution_action.dart';
import '../../domain/repositories/delivery_order_repository.dart';
import '../models/mark_failed_delivery_request_dto.dart';
import '../models/resolve_failed_delivery_request_dto.dart';
import '../services/delivery_order_api_service.dart';

class DeliveryOrderRepositoryImpl implements DeliveryOrderRepository {
  DeliveryOrderRepositoryImpl(this._apiService);

  final DeliveryOrderApiService _apiService;

  @override
  Future<void> markFailed({
    required String id,
    required String failureReason,
    required String failureType,
  }) => _apiService.markFailed(
    id,
    MarkFailedDeliveryRequestDto(
      failureReason: failureReason,
      failureType: failureType,
    ),
  );

  @override
  Future<void> resolveFailedDelivery({
    required String id,
    required ResolutionAction resolutionAction,
    required String resolutionNote,
  }) => _apiService.resolveFailedDelivery(
    id,
    ResolveFailedDeliveryRequestDto(
      resolutionAction: resolutionAction,
      resolutionNote: resolutionNote,
    ),
  );
}
