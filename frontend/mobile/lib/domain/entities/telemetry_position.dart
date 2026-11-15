class TelemetryPosition {
  const TelemetryPosition({
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
