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
  final int declaredWeightGram;
  final int declaredLengthCm;
  final int declaredWidthCm;
  final int declaredHeightCm;
  final bool isFragile;
  final String originStationId;
  final String destinationStationId;

  bool get isOrigin => role == 'ORIGIN';
  bool get isDestination => role == 'DESTINATION';

  factory StationOrderInfo.fromJson(Map<String, dynamic> json) =>
      StationOrderInfo(
        orderId: json['orderId'] as String? ?? '',
        trackingNumber: json['trackingNumber'] as String? ?? '',
        status: json['status'] as String? ?? '',
        role: json['role'] as String? ?? 'ORIGIN',
        packageName: json['packageName'] as String? ?? '',
        declaredWeightGram: json['declaredWeightGram'] as int? ?? 0,
        declaredLengthCm: json['declaredLengthCm'] as int? ?? 0,
        declaredWidthCm: json['declaredWidthCm'] as int? ?? 0,
        declaredHeightCm: json['declaredHeightCm'] as int? ?? 0,
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

class PackageIntakeRequest {
  const PackageIntakeRequest({
    required this.trackingNumber,
    required this.slotId,
    required this.measuredWeightGram,
    required this.measuredLengthCm,
    required this.measuredWidthCm,
    required this.measuredHeightCm,
    required this.visualInspectionPassed,
  });

  final String trackingNumber;
  final String slotId;
  final int measuredWeightGram;
  final int measuredLengthCm;
  final int measuredWidthCm;
  final int measuredHeightCm;
  final bool visualInspectionPassed;

  Map<String, dynamic> toJson() => {
        'trackingNumber': trackingNumber,
        'slotId': slotId,
        'measuredWeightGram': measuredWeightGram,
        'measuredLengthCm': measuredLengthCm,
        'measuredWidthCm': measuredWidthCm,
        'measuredHeightCm': measuredHeightCm,
        'visualInspectionPassed': visualInspectionPassed,
      };
}

class PackageIntakeResponse {
  const PackageIntakeResponse({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.slotId,
    required this.slotNumber,
    required this.confirmedAt,
  });

  final String orderId;
  final String trackingNumber;
  final String status;
  final String slotId;
  final int slotNumber;
  final DateTime confirmedAt;

  factory PackageIntakeResponse.fromJson(Map<String, dynamic> json) =>
      PackageIntakeResponse(
        orderId: json['orderId'] as String? ?? '',
        trackingNumber: json['trackingNumber'] as String? ?? '',
        status: json['status'] as String? ?? 'READY_FOR_TAKEOFF',
        slotId: json['slotId'] as String? ?? '',
        slotNumber: json['slotNumber'] as int? ?? 0,
        confirmedAt: json['confirmedAt'] != null
            ? DateTime.parse(json['confirmedAt'] as String)
            : DateTime.now().toUtc(),
      );
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

class ValidateCodeResponse {
  const ValidateCodeResponse({
    required this.isValid,
    required this.attemptsRemaining,
    required this.isLocked,
    this.lockoutExpiresAt,
  });

  final bool isValid;
  final int attemptsRemaining;
  final bool isLocked;
  final DateTime? lockoutExpiresAt;

  factory ValidateCodeResponse.fromJson(Map<String, dynamic> json) =>
      ValidateCodeResponse(
        isValid: json['isValid'] as bool? ?? false,
        attemptsRemaining: json['attemptsRemaining'] as int? ?? 0,
        isLocked: json['isLocked'] as bool? ?? false,
        lockoutExpiresAt: json['lockoutExpiresAt'] != null
            ? DateTime.parse(json['lockoutExpiresAt'] as String)
            : null,
      );
}
