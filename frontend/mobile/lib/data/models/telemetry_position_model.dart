import '../../domain/entities/telemetry_position.dart';

class TelemetryPositionModel extends TelemetryPosition {
  const TelemetryPositionModel({
    required super.latitude,
    required super.longitude,
    required super.altitudeMeters,
    required super.speedMps,
    required super.batteryPercentage,
    required super.timestamp,
  });

  factory TelemetryPositionModel.fromJson(Map<String, dynamic> json) =>
      TelemetryPositionModel(
        latitude: _number(json, 'latitude'),
        longitude: _number(json, 'longitude'),
        altitudeMeters: _number(json, 'altitudeMeters'),
        speedMps: _number(json, 'speedMps'),
        batteryPercentage: _number(json, 'batteryPercentage').round(),
        timestamp: _date(json, 'timestamp'),
      );

  static double _number(Map<String, dynamic> json, String key) {
    final value = json[key];
    if (value is! num) throw FormatException('Expected numeric $key.');
    return value.toDouble();
  }

  static DateTime _date(Map<String, dynamic> json, String key) {
    final value = json[key];
    if (value is! String) throw FormatException('Expected timestamp $key.');
    final result = DateTime.tryParse(value);
    if (result == null) throw FormatException('Invalid timestamp $key.');
    return result;
  }
}
