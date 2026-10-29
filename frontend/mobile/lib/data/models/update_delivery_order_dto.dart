class UpdateDeliveryOrderDto {
  const UpdateDeliveryOrderDto({
    required this.recipientName,
    required this.recipientPhone,
    required this.destinationStationId,
    this.deliveryAddressId,
  });

  final String recipientName;
  final String recipientPhone;
  final String destinationStationId;
  final String? deliveryAddressId;

  Map<String, dynamic> toJson() => {
    'recipientName': recipientName,
    'recipientPhone': recipientPhone,
    'destinationStationId': destinationStationId,
    'deliveryAddressId': deliveryAddressId,
  };
}
