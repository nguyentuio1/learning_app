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
    recipientName: json['recipientName'] as String? ?? '',
    recipientPhone: json['recipientPhone'] as String? ?? '',
    originStationId: json['originStationId'] as String? ?? '',
    destinationStationId: json['destinationStationId'] as String? ?? '',
    status: json['status'] as String? ?? '',
    deliveryAddressId: json['deliveryAddressId'] as String?,
    trackingNumber: json['trackingNumber'] as String?,
  );
}

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

class DeliveryOrderResourceSummary {
  const DeliveryOrderResourceSummary({
    required this.id,
    this.name,
    this.label,
    this.status,
  });

  final String id;
  final String? name;
  final String? label;
  final String? status;

  factory DeliveryOrderResourceSummary.fromJson(Map<String, dynamic> json) =>
      DeliveryOrderResourceSummary(
        id: json['id'] as String,
        name: json['packageName'] as String? ?? json['name'] as String?,
        label: json['addressLabel'] as String?,
        status: json['status'] as String?,
      );
}

class CreateDeliveryOrderResponseDto {
  const CreateDeliveryOrderResponseDto({
    required this.id,
    required this.trackingNumber,
    required this.status,
    required this.pickupSecureCode,
    required this.recipientName,
    required this.recipientPhone,
    required this.originStationId,
    required this.destinationStationId,
    this.package,
    this.originStation,
    this.destinationStation,
    this.deliveryAddress,
    this.deliveryAddressId,
  });

  final String id;
  final String trackingNumber;
  final String status;
  final String pickupSecureCode;
  final String recipientName;
  final String recipientPhone;
  final String originStationId;
  final String destinationStationId;
  final String? deliveryAddressId;
  final DeliveryOrderResourceSummary? package;
  final DeliveryOrderResourceSummary? originStation;
  final DeliveryOrderResourceSummary? destinationStation;
  final DeliveryOrderResourceSummary? deliveryAddress;

  factory CreateDeliveryOrderResponseDto.fromJson(Map<String, dynamic> json) =>
      CreateDeliveryOrderResponseDto(
        id: json['id'] as String,
        trackingNumber: json['trackingNumber'] as String,
        status: json['status'] as String,
        pickupSecureCode: json['pickupSecureCode'] as String,
        recipientName: json['recipientName'] as String,
        recipientPhone: json['recipientPhone'] as String,
        originStationId: json['originStationId'] as String,
        destinationStationId: json['destinationStationId'] as String,
        deliveryAddressId: json['deliveryAddressId'] as String?,
        package: _resource(json['package']),
        originStation: _resource(json['originStation']),
        destinationStation: _resource(json['destinationStation']),
        deliveryAddress: _resource(json['deliveryAddress']),
      );

  static DeliveryOrderResourceSummary? _resource(dynamic value) {
    if (value == null) return null;
    if (value is! Map) {
      throw const FormatException('Expected an order resource object.');
    }
    return DeliveryOrderResourceSummary.fromJson(
      Map<String, dynamic>.from(value),
    );
  }
}
