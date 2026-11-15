import '../../domain/entities/delivery_map_snapshot.dart';

class DeliveryMapSnapshotModel extends DeliveryMapSnapshot {
  const DeliveryMapSnapshotModel({
    required super.orderId,
    required super.trackingNumber,
    required super.status,
    required super.originStation,
    required super.destinationStation,
    required super.estimatedDeliveryTime,
    required super.lastPosition,
    required super.telemetryStale,
  });

  factory DeliveryMapSnapshotModel.fromJson(Map<String, dynamic> json) =>
      DeliveryMapSnapshotModel(
        orderId: _requiredString(json, 'orderId'),
        trackingNumber: _requiredString(json, 'trackingNumber'),
        status: _parseStatus(_requiredString(json, 'status')),
        originStation: MapStationPointModel.fromJson(
          _requiredMap(json, 'originStation'),
        ),
        destinationStation: MapStationPointModel.fromJson(
          _requiredMap(json, 'destinationStation'),
        ),
        estimatedDeliveryTime: _parseDate(json['estimatedDeliveryTime']),
        lastPosition: json['lastPosition'] == null
            ? null
            : DroneMapTelemetryModel.fromJson(
                _asMap(json['lastPosition'], 'lastPosition'),
              ),
        telemetryStale: _requiredBool(json, 'telemetryStale'),
      );
}

class MapStationPointModel extends MapStationPoint {
  const MapStationPointModel({
    required super.id,
    required super.name,
    required super.latitude,
    required super.longitude,
  });

  factory MapStationPointModel.fromJson(Map<String, dynamic> json) =>
      MapStationPointModel(
        id: _requiredString(json, 'id'),
        name: _requiredString(json, 'name'),
        latitude: _requiredNumber(json, 'latitude'),
        longitude: _requiredNumber(json, 'longitude'),
      );
}

class DroneMapTelemetryModel extends DroneMapTelemetry {
  const DroneMapTelemetryModel({
    required super.latitude,
    required super.longitude,
    required super.altitudeMeters,
    required super.speedMps,
    required super.batteryPercentage,
    required super.timestamp,
  });

  factory DroneMapTelemetryModel.fromJson(Map<String, dynamic> json) =>
      DroneMapTelemetryModel(
        latitude: _requiredNumber(json, 'latitude'),
        longitude: _requiredNumber(json, 'longitude'),
        altitudeMeters: _requiredNumber(json, 'altitudeMeters'),
        speedMps: _requiredNumber(json, 'speedMps'),
        batteryPercentage: _requiredInteger(json, 'batteryPercentage'),
        timestamp:
            _parseDate(json['timestamp']) ??
            (throw const FormatException('Missing timestamp.')),
      );
}

DeliveryMapOrderStatus _parseStatus(String status) => switch (status) {
  'PENDING' => DeliveryMapOrderStatus.pending,
  'APPROVED' => DeliveryMapOrderStatus.approved,
  'READY_FOR_TAKEOFF' => DeliveryMapOrderStatus.readyForTakeoff,
  'IN_TRANSIT' => DeliveryMapOrderStatus.inTransit,
  'ARRIVED' => DeliveryMapOrderStatus.arrived,
  'DELIVERED' => DeliveryMapOrderStatus.delivered,
  'CANCELLED' => DeliveryMapOrderStatus.cancelled,
  'FAILED' => DeliveryMapOrderStatus.failed,
  _ => DeliveryMapOrderStatus.unknown,
};

String _requiredString(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is String) return value;
  throw FormatException('Expected "$key" to be a string.');
}

double _requiredNumber(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is num) return value.toDouble();
  throw FormatException('Expected "$key" to be numeric.');
}

int _requiredInteger(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is int) return value;
  throw FormatException('Expected "$key" to be an integer.');
}

bool _requiredBool(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is bool) return value;
  throw FormatException('Expected "$key" to be a boolean.');
}

Map<String, dynamic> _requiredMap(Map<String, dynamic> json, String key) =>
    _asMap(json[key], key);

Map<String, dynamic> _asMap(Object? value, String key) {
  if (value is Map<String, dynamic>) return value;
  if (value is Map) return Map<String, dynamic>.from(value);
  throw FormatException('Expected "$key" to be an object.');
}

DateTime? _parseDate(Object? value) {
  if (value == null) return null;
  if (value is! String) throw const FormatException('Expected an ISO date.');
  return DateTime.parse(value).toUtc();
}
