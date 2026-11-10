import '../../domain/entities/resolution_action.dart';

class ResolveFailedDeliveryRequestDto {
  const ResolveFailedDeliveryRequestDto({
    required this.resolutionAction,
    required this.resolutionNote,
  });

  final ResolutionAction resolutionAction;
  final String resolutionNote;

  Map<String, dynamic> toJson() => {
    'resolutionAction': resolutionAction.value,
    'resolutionNote': resolutionNote,
  };
}
