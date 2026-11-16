enum DeliveryMapOrderStatus {
  pending,
  approved,
  readyForTakeoff,
  inTransit,
  arrived,
  delivered,
  cancelled,
  failed,
  unknown,
}

class MapStationPoint {
  const MapStationPoint({
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

class DroneMapTelemetry {
  const DroneMapTelemetry({
    required this.latitude,
    required this.longitude,
    required this.altitudeMeters,
    required this.speedMps,
    required this.batteryPercentage,
    required this.timestamp,
  });

  final double latitude;
  final double longitude;
  final double altitudeMeters;
  final double speedMps;
  final int batteryPercentage;
  final DateTime timestamp;
}

class DeliveryMapSnapshot {
  const DeliveryMapSnapshot({
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
  final DeliveryMapOrderStatus status;
  final MapStationPoint originStation;
  final MapStationPoint destinationStation;
  final DateTime? estimatedDeliveryTime;
  final DroneMapTelemetry? lastPosition;
  final bool telemetryStale;

  DeliveryMapSnapshot copyWith({
    DroneMapTelemetry? lastPosition,
    bool? telemetryStale,
  }) => DeliveryMapSnapshot(
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

enum DeliveryMapUpdateType { position, statusChanged, connected, reconnecting }

class DeliveryMapUpdate {
  const DeliveryMapUpdate({required this.type, this.position, this.orderId});

  final DeliveryMapUpdateType type;
  final DroneMapTelemetry? position;
  final String? orderId;
}
