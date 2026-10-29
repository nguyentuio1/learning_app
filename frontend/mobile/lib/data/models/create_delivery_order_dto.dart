class CreateDeliveryOrderDto {
  const CreateDeliveryOrderDto({
    required this.packageId,
    required this.originStationId,
    required this.destinationStationId,
    required this.recipientName,
    required this.recipientPhone,
    this.deliveryAddressId,
  });

  final String packageId;
  final String originStationId;
  final String destinationStationId;
  final String? deliveryAddressId;
  final String recipientName;
  final String recipientPhone;

  Map<String, dynamic> toJson() => {
    'packageId': packageId,
    'originStationId': originStationId,
    'destinationStationId': destinationStationId,
    'deliveryAddressId': deliveryAddressId,
    'recipientName': recipientName,
    'recipientPhone': recipientPhone,
  };
}
