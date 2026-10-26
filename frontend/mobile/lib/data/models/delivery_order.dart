class DeliveryOrder {
  const DeliveryOrder({
    required this.id,
    required this.recipientName,
    required this.recipientPhone,
    required this.originStationId,
    required this.destinationStationId,
    required this.status,
    this.deliveryAddressId,
    this.trackingNumber,
  });

  final String id;
  final String recipientName;
  final String recipientPhone;
  final String originStationId;
  final String destinationStationId;
  final String status;
  final String? deliveryAddressId;
  final String? trackingNumber;

  factory DeliveryOrder.fromJson(Map<String, dynamic> json) => DeliveryOrder(
    id: json['id'] as String,
    recipientName: json['recipientName'] as String,
    recipientPhone: json['recipientPhone'] as String,
    originStationId: json['originStationId'] as String,
    destinationStationId: json['destinationStationId'] as String,
    status: json['status'] as String,
    deliveryAddressId: json['deliveryAddressId'] as String?,
    trackingNumber: json['trackingNumber'] as String?,
  );
}
