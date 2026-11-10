class MarkFailedDeliveryRequestDto {
  const MarkFailedDeliveryRequestDto({
    required this.failureReason,
    required this.failureType,
  });

  final String failureReason;
  final String failureType;

  Map<String, dynamic> toJson() => {
    'failureReason': failureReason,
    'failureType': failureType,
  };
}
