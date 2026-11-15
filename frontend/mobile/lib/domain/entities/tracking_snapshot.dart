import 'telemetry_position.dart';

enum DeliveryTrackingStatus {
  pending,
  approved,
  readyForTakeoff,
  inTransit,
  arrived,
  delivered,
  cancelled,
  failed;

  static DeliveryTrackingStatus parse(String value) => switch (value) {
    'PENDING' => DeliveryTrackingStatus.pending,
    'APPROVED' => DeliveryTrackingStatus.approved,
    'READY_FOR_TAKEOFF' => DeliveryTrackingStatus.readyForTakeoff,
    'IN_TRANSIT' => DeliveryTrackingStatus.inTransit,
    'ARRIVED' => DeliveryTrackingStatus.arrived,
    'DELIVERED' => DeliveryTrackingStatus.delivered,
    'CANCELLED' => DeliveryTrackingStatus.cancelled,
    'FAILED' => DeliveryTrackingStatus.failed,
    _ => throw FormatException('Unknown delivery tracking status: $value'),
  };

  String get apiValue => switch (this) {
    DeliveryTrackingStatus.pending => 'PENDING',
    DeliveryTrackingStatus.approved => 'APPROVED',
    DeliveryTrackingStatus.readyForTakeoff => 'READY_FOR_TAKEOFF',
    DeliveryTrackingStatus.inTransit => 'IN_TRANSIT',
    DeliveryTrackingStatus.arrived => 'ARRIVED',
    DeliveryTrackingStatus.delivered => 'DELIVERED',
    DeliveryTrackingStatus.cancelled => 'CANCELLED',
    DeliveryTrackingStatus.failed => 'FAILED',
  };
}

class TrackingStation {
  const TrackingStation({
    required this.id,
    required this.name,
    required this.latitude,
    required this.longitude,
  });

  final String id;
  final String name;
  final double latitude;
  final double longitude;
}

class TrackingSnapshot {
  const TrackingSnapshot({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.originStation,
    required this.destinationStation,
    required this.estimatedDeliveryTime,
    required this.lastPosition,
    required this.telemetryStale,
  });

  final String orderId;
  final String trackingNumber;
  final DeliveryTrackingStatus status;
  final TrackingStation originStation;
  final TrackingStation destinationStation;
  final DateTime? estimatedDeliveryTime;
  final TelemetryPosition? lastPosition;
  final bool telemetryStale;

  TrackingSnapshot copyWith({
    TelemetryPosition? lastPosition,
    bool? telemetryStale,
  }) => TrackingSnapshot(
    orderId: orderId,
    trackingNumber: trackingNumber,
    status: status,
    originStation: originStation,
    destinationStation: destinationStation,
    estimatedDeliveryTime: estimatedDeliveryTime,
    lastPosition: lastPosition ?? this.lastPosition,
    telemetryStale: telemetryStale ?? this.telemetryStale,
  );
}
