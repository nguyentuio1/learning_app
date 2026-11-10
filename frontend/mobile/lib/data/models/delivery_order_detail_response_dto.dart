class OrderPackageDto {
  const OrderPackageDto({
    this.id,
    this.packageName,
    this.weightGram,
    this.lengthCm,
    this.widthCm,
    this.heightCm,
    this.packageImageUrl,
    this.isFragile,
  });

  final String? id;
  final String? packageName;
  final double? weightGram;
  final double? lengthCm;
  final double? widthCm;
  final double? heightCm;
  final String? packageImageUrl;
  final bool? isFragile;

  factory OrderPackageDto.fromJson(Map<String, dynamic> json) =>
      OrderPackageDto(
        id: json['id'] as String?,
        packageName: json['packageName'] as String?,
        weightGram: (json['weightGram'] as num?)?.toDouble(),
        lengthCm: (json['lengthCm'] as num?)?.toDouble(),
        widthCm: (json['widthCm'] as num?)?.toDouble(),
        heightCm: (json['heightCm'] as num?)?.toDouble(),
        packageImageUrl: json['packageImageUrl'] as String?,
        isFragile: json['isFragile'] as bool?,
      );
}

class OrderStationDto {
  const OrderStationDto({
    this.id,
    this.stationCode,
    this.name,
    this.address,
    this.latitude,
    this.longitude,
  });

  final String? id;
  final String? stationCode;
  final String? name;
  final String? address;
  final double? latitude;
  final double? longitude;

  factory OrderStationDto.fromJson(Map<String, dynamic> json) =>
      OrderStationDto(
        id: json['id'] as String?,
        stationCode: json['stationCode'] as String?,
        name: json['name'] as String?,
        address: json['address'] as String?,
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
      );
}

class OrderDeliveryAddressDto {
  const OrderDeliveryAddressDto({
    this.id,
    this.addressLabel,
    this.recipientName,
    this.recipientPhone,
    this.streetAddress,
  });

  final String? id;
  final String? addressLabel;
  final String? recipientName;
  final String? recipientPhone;
  final String? streetAddress;

  factory OrderDeliveryAddressDto.fromJson(
    Map<String, dynamic> json,
  ) => OrderDeliveryAddressDto(
    id: json['id'] as String?,
    addressLabel: json['label'] as String? ?? json['addressLabel'] as String?,
    recipientName:
        json['contactName'] as String? ?? json['recipientName'] as String?,
    recipientPhone:
        json['contactPhone'] as String? ?? json['recipientPhone'] as String?,
    streetAddress:
        json['addressLine'] as String? ?? json['streetAddress'] as String?,
  );
}

class OrderAiEtaPredictionDto {
  const OrderAiEtaPredictionDto({
    this.calculatedDistanceKm,
    this.predictedDurationMinutes,
    this.weatherCondition,
    this.confidenceScore,
    this.createdAt,
  });

  final double? calculatedDistanceKm;
  final int? predictedDurationMinutes;
  final String? weatherCondition;
  final double? confidenceScore;
  final DateTime? createdAt;

  factory OrderAiEtaPredictionDto.fromJson(Map<String, dynamic> json) =>
      OrderAiEtaPredictionDto(
        calculatedDistanceKm: (json['calculatedDistanceKm'] as num?)
            ?.toDouble(),
        predictedDurationMinutes: json['predictedDurationMinutes'] as int?,
        weatherCondition: json['weatherCondition'] as String?,
        confidenceScore: (json['confidenceScore'] as num?)?.toDouble(),
        createdAt: _parseDate(json['createdAt']),
      );
}

class DeliveryOrderStatusLogDto {
  const DeliveryOrderStatusLogDto({
    required this.toStatus,
    required this.createdAt,
    this.fromStatus,
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
    required this.recipientName,
    required this.recipientPhone,
    required this.originStationId,
    required this.destinationStationId,
    required this.status,
    required this.statusLogs,
    this.trackingNumber,
    this.packageId,
    this.deliveryAddressId,
    this.cancelReason,
    this.createdAt,
    this.updatedAt,
    this.scheduledDepartureTime,
    this.actualDepartureTime,
    this.estimatedDeliveryTime,
    this.actualDeliveryTime,
    this.package,
    this.originStation,
    this.destinationStation,
    this.deliveryAddress,
    this.aiEtaPrediction,
  });

  final String id;
  final String? trackingNumber;
  final String recipientName;
  final String recipientPhone;
  final String originStationId;
  final String destinationStationId;
  final String status;
  final String? packageId;
  final String? deliveryAddressId;
  final String? cancelReason;
  final DateTime? createdAt;
  final DateTime? updatedAt;
  final DateTime? scheduledDepartureTime;
  final DateTime? actualDepartureTime;
  final DateTime? estimatedDeliveryTime;
  final DateTime? actualDeliveryTime;
  final OrderPackageDto? package;
  final OrderStationDto? originStation;
  final OrderStationDto? destinationStation;
  final OrderDeliveryAddressDto? deliveryAddress;
  final OrderAiEtaPredictionDto? aiEtaPrediction;
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

    return DeliveryOrderDetailResponseDto(
      id: json['id'] as String,
      trackingNumber: json['trackingNumber'] as String?,
      recipientName: json['recipientName'] as String? ?? '',
      recipientPhone: json['recipientPhone'] as String? ?? '',
      originStationId: json['originStationId'] as String? ?? '',
      destinationStationId: json['destinationStationId'] as String? ?? '',
      status: json['status'] as String? ?? '',
      packageId: json['packageId'] as String?,
      deliveryAddressId: json['deliveryAddressId'] as String?,
      cancelReason: json['cancelReason'] as String?,
      createdAt: _parseDate(json['createdAt']),
      updatedAt: _parseDate(json['updatedAt']),
      scheduledDepartureTime: _parseDate(json['scheduledDepartureTime']),
      actualDepartureTime: _parseDate(json['actualDepartureTime']),
      estimatedDeliveryTime: _parseDate(json['estimatedDeliveryTime']),
      actualDeliveryTime: _parseDate(json['actualDeliveryTime']),
      package: _parseNested(json['package'], OrderPackageDto.fromJson),
      originStation: _parseNested(
        json['originStation'],
        OrderStationDto.fromJson,
      ),
      destinationStation: _parseNested(
        json['destinationStation'],
        OrderStationDto.fromJson,
      ),
      deliveryAddress: _parseNested(
        json['deliveryAddress'],
        OrderDeliveryAddressDto.fromJson,
      ),
      aiEtaPrediction: _parseNested(
        json['aiEtaPrediction'],
        OrderAiEtaPredictionDto.fromJson,
      ),
      statusLogs: List.unmodifiable(logs),
    );
  }
}

T? _parseNested<T>(dynamic value, T Function(Map<String, dynamic>) parse) {
  if (value == null) return null;
  if (value is! Map) {
    throw const FormatException('Expected an order detail object.');
  }
  return parse(Map<String, dynamic>.from(value));
}

DateTime? _parseDate(dynamic value) =>
    value == null ? null : DateTime.parse(value as String).toUtc();
