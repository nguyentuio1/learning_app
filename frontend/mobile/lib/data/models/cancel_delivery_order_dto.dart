class CancelDeliveryOrderDto {
  const CancelDeliveryOrderDto({required this.cancellationReason});

  final String cancellationReason;

  Map<String, dynamic> toJson() => {'cancellationReason': cancellationReason};
}
