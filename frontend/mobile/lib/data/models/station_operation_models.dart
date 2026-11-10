class StationOrderInfo {
  const StationOrderInfo({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.role,
    required this.packageName,
    required this.declaredWeightGram,
    required this.declaredLengthCm,
    required this.declaredWidthCm,
    required this.declaredHeightCm,
    required this.isFragile,
    required this.originStationId,
    required this.destinationStationId,
  });

  final String orderId;
  final String trackingNumber;
  final String status;
  final String role; // 'ORIGIN' or 'DESTINATION'
  final String packageName;
  final double declaredWeightGram;
  final double declaredLengthCm;
  final double declaredWidthCm;
  final double declaredHeightCm;
  final bool isFragile;
  final String originStationId;
  final String destinationStationId;

  bool get isOrigin => role == 'ORIGIN';
  bool get isDestination => role == 'DESTINATION';
  bool get isEligibleForPickup => isDestination && status == 'ARRIVED';

  static String formatDecimal(double value) =>
      value == value.roundToDouble()
          ? value.toInt().toString()
          : value.toString();

  factory StationOrderInfo.fromJson(Map<String, dynamic> json) =>
      StationOrderInfo(
        orderId: json['orderId'] as String? ?? '',
        trackingNumber: json['trackingNumber'] as String? ?? '',
        status: json['status'] as String? ?? 'UNKNOWN',
        role: json['role'] as String? ?? 'UNKNOWN',
        packageName: json['packageName'] as String? ?? '',
        declaredWeightGram:
            (json['declaredWeightGram'] as num?)?.toDouble() ?? 0,
        declaredLengthCm: (json['declaredLengthCm'] as num?)?.toDouble() ?? 0,
        declaredWidthCm: (json['declaredWidthCm'] as num?)?.toDouble() ?? 0,
        declaredHeightCm: (json['declaredHeightCm'] as num?)?.toDouble() ?? 0,
        isFragile: json['isFragile'] as bool? ?? false,
        originStationId: json['originStationId'] as String? ?? '',
        destinationStationId: json['destinationStationId'] as String? ?? '',
      );

  Map<String, dynamic> toJson() => {
        'orderId': orderId,
        'trackingNumber': trackingNumber,
        'status': status,
        'role': role,
        'packageName': packageName,
        'declaredWeightGram': declaredWeightGram,
        'declaredLengthCm': declaredLengthCm,
        'declaredWidthCm': declaredWidthCm,
        'declaredHeightCm': declaredHeightCm,
        'isFragile': isFragile,
        'originStationId': originStationId,
        'destinationStationId': destinationStationId,
      };
}

class ConfirmPickupRequest {
  const ConfirmPickupRequest({
    required this.trackingNumber,
    required this.pickupSecureCode,
  });

  final String trackingNumber;
  final String pickupSecureCode;

  Map<String, dynamic> toJson() => {
        'trackingNumber': trackingNumber,
        'pickupSecureCode': pickupSecureCode,
      };
}

class ConfirmPickupResponse {
  const ConfirmPickupResponse({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.actualDeliveryTime,
    required this.slotId,
    required this.slotReleased,
  });

  final String orderId;
  final String trackingNumber;
  final String status;
  final DateTime actualDeliveryTime;
  final String slotId;
  final bool slotReleased;

  factory ConfirmPickupResponse.fromJson(Map<String, dynamic> json) =>
      ConfirmPickupResponse(
        orderId: json['orderId'] as String? ?? '',
        trackingNumber: json['trackingNumber'] as String? ?? '',
        status: json['status'] as String? ?? 'DELIVERED',
        actualDeliveryTime: json['actualDeliveryTime'] != null
            ? DateTime.parse(json['actualDeliveryTime'] as String)
            : DateTime.now().toUtc(),
        slotId: json['slotId'] as String? ?? '',
        slotReleased: json['slotReleased'] as bool? ?? false,
      );
}
