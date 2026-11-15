class ExpiredParcel {
  const ExpiredParcel({
    required this.slotId,
    required this.slotNumber,
    required this.trackingNumber,
    required this.orderStatus,
    required this.slotStatus,
    required this.cancelReason,
    required this.arrivedAt,
    required this.elapsedDuration,
  });

  static const pickupTimeoutReason = 'Pickup window expired (24h timeout)';

  final String slotId;
  final int slotNumber;
  final String trackingNumber;
  final String orderStatus;
  final String slotStatus;
  final String cancelReason;
  final DateTime arrivedAt;
  final Duration elapsedDuration;

  factory ExpiredParcel.fromJson(Map<String, dynamic> json) => ExpiredParcel(
    slotId: (json['slotId'] ?? json['id']) as String,
    slotNumber: (json['slotNumber'] as num).toInt(),
    trackingNumber: json['trackingNumber'] as String,
    orderStatus: json['orderStatus'] as String,
    slotStatus: (json['slotStatus'] ?? json['status']) as String,
    cancelReason: json['cancelReason'] as String,
    arrivedAt: DateTime.parse(json['arrivedAt'] as String),
    elapsedDuration: _parseDuration(json['elapsedDuration']),
  );

  Map<String, dynamic> toJson() => {
    'slotId': slotId,
    'slotNumber': slotNumber,
    'trackingNumber': trackingNumber,
    'orderStatus': orderStatus,
    'slotStatus': slotStatus,
    'cancelReason': cancelReason,
    'arrivedAt': arrivedAt.toIso8601String(),
    'elapsedDuration': elapsedDuration.inSeconds,
  };

  static Duration _parseDuration(dynamic value) {
    if (value is num) {
      return Duration(seconds: value.toInt());
    }

    if (value is String) {
      final seconds = int.tryParse(value);
      if (seconds != null) {
        return Duration(seconds: seconds);
      }

      final match = RegExp(r'^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.\d+)?$')
          .firstMatch(value);
      if (match != null) {
        final days = int.parse(match[1] ?? '0');
        final hours = int.parse(match[2]!);
        final minutes = int.parse(match[3]!);
        final seconds = int.parse(match[4]!);
        return Duration(
          days: days,
          hours: hours,
          minutes: minutes,
          seconds: seconds,
        );
      }
    }

    throw const FormatException(
      'Expected elapsedDuration in seconds or time format.',
    );
  }
}

class ClearExpiredParcelRequest {
  ClearExpiredParcelRequest({required String slotId, required String reason})
    : slotId = slotId.trim(),
      reason = reason.trim() {
    if (!_uuidPattern.hasMatch(this.slotId)) {
      throw ArgumentError.value(slotId, 'slotId', 'Must be a UUID.');
    }
    if (this.reason.length < 5 || this.reason.length > 500) {
      throw ArgumentError.value(
        reason,
        'reason',
        'Must be between 5 and 500 characters.',
      );
    }
  }

  static final _uuidPattern = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
  );

  final String slotId;
  final String reason;

  Map<String, dynamic> toJson() => {'slotId': slotId, 'reason': reason};
}

class ClearExpiredParcelResponse {
  const ClearExpiredParcelResponse({
    required this.orderId,
    required this.trackingNumber,
    required this.slotId,
    required this.slotStatus,
    required this.clearedAt,
  });

  final String orderId;
  final String trackingNumber;
  final String slotId;
  final String slotStatus;
  final DateTime clearedAt;

  factory ClearExpiredParcelResponse.fromJson(Map<String, dynamic> json) =>
      ClearExpiredParcelResponse(
        orderId: json['orderId'] as String,
        trackingNumber: json['trackingNumber'] as String,
        slotId: json['slotId'] as String,
        slotStatus: json['slotStatus'] as String,
        clearedAt: DateTime.parse(json['clearedAt'] as String),
      );
}

class PickupTimeoutAlert {
  const PickupTimeoutAlert({
    required this.id,
    required this.slotId,
    required this.trackingNumber,
    required this.title,
    required this.message,
  });

  final String id;
  final String slotId;
  final String trackingNumber;
  final String title;
  final String message;

  factory PickupTimeoutAlert.fromExpiredParcel(
    ExpiredParcel parcel,
  ) => PickupTimeoutAlert(
    id: parcel.slotId,
    slotId: parcel.slotId,
    trackingNumber: parcel.trackingNumber,
    title: 'Pickup window expired',
    message:
        'Order ${parcel.trackingNumber} was not collected within 24 hours and has failed. Move the parcel out of its slot.',
  );
}
