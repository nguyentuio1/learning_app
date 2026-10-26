class DeliveryOrderStatusLogDto {
  const DeliveryOrderStatusLogDto({
    required this.fromStatus,
    required this.toStatus,
    required this.createdAt,
    this.remarks,
  });

  final String? fromStatus;
  final String toStatus;
  final String? remarks;
  final DateTime createdAt;

  factory DeliveryOrderStatusLogDto.fromJson(Map<String, dynamic> json) =>
      DeliveryOrderStatusLogDto(
        fromStatus: json['fromStatus'] as String?,
        toStatus: json['toStatus'] as String,
        remarks: json['remarks'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String).toUtc(),
      );
}

class DeliveryOrderDetailResponseDto {
  const DeliveryOrderDetailResponseDto({
    required this.id,
    required this.trackingNumber,
    required this.recipientName,
    required this.recipientPhone,
    required this.originStationId,
    required this.destinationStationId,
    required this.status,
    required this.statusLogs,
    this.packageId,
    this.packageName,
    this.deliveryAddressId,
    this.cancelReason,
    this.scheduledDepartureTime,
    this.actualDepartureTime,
    this.estimatedDeliveryTime,
    this.actualDeliveryTime,
    this.createdAt,
    this.updatedAt,
  });

  final String id;
  final String trackingNumber;
  final String recipientName;
  final String recipientPhone;
  final String originStationId;
  final String destinationStationId;
  final String status;
  final String? packageId;
  final String? packageName;
  final String? deliveryAddressId;
  final String? cancelReason;
  final DateTime? scheduledDepartureTime;
  final DateTime? actualDepartureTime;
  final DateTime? estimatedDeliveryTime;
  final DateTime? actualDeliveryTime;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final List<DeliveryOrderStatusLogDto> statusLogs;

  factory DeliveryOrderDetailResponseDto.fromJson(Map<String, dynamic> json) {
    final logs =
        (json['statusLogs'] as List<dynamic>? ?? [])
            .map(
              (log) => DeliveryOrderStatusLogDto.fromJson(
                Map<String, dynamic>.from(log as Map),
              ),
            )
            .toList()
          ..sort((left, right) => left.createdAt.compareTo(right.createdAt));
    final package = json['package'];

    return DeliveryOrderDetailResponseDto(
      id: json['id'] as String,
      trackingNumber: json['trackingNumber'] as String? ?? json['id'] as String,
      recipientName: json['recipientName'] as String,
      recipientPhone: json['recipientPhone'] as String,
      originStationId: json['originStationId'] as String,
      destinationStationId: json['destinationStationId'] as String,
      status: json['status'] as String,
      packageId: json['packageId'] as String?,
      packageName:
          json['packageName'] as String? ??
          (package is Map ? package['packageName'] as String? : null),
      deliveryAddressId: json['deliveryAddressId'] as String?,
      cancelReason: json['cancelReason'] as String?,
      scheduledDepartureTime: _date(json['scheduledDepartureTime']),
      actualDepartureTime: _date(json['actualDepartureTime']),
      estimatedDeliveryTime: _date(json['estimatedDeliveryTime']),
      actualDeliveryTime: _date(json['actualDeliveryTime']),
      createdAt: _date(json['createdAt']),
      updatedAt: _date(json['updatedAt']),
      statusLogs: List.unmodifiable(logs),
    );
  }

  static DateTime? _date(dynamic value) =>
      value == null ? null : DateTime.parse(value as String).toUtc();
}
