import '../../domain/entities/tracking_snapshot.dart';
import 'telemetry_position_model.dart';

class TrackingSnapshotModel extends TrackingSnapshot {
  const TrackingSnapshotModel({
    required super.orderId,
    required super.trackingNumber,
    required super.status,
    required super.originStation,
    required super.destinationStation,
    required super.estimatedDeliveryTime,
    required super.lastPosition,
    required super.telemetryStale,
  });

  factory TrackingSnapshotModel.fromJson(Map<String, dynamic> json) {
    final rawPosition = json['lastPosition'];
    final estimated = json['estimatedDeliveryTime'];
    if (rawPosition != null && rawPosition is! Map) {
      throw const FormatException('Expected lastPosition to be an object.');
    }
    if (estimated != null && estimated is! String) {
      throw const FormatException(
        'Expected estimatedDeliveryTime to be a timestamp.',
      );
    }
    if (json['telemetryStale'] is! bool) {
      throw const FormatException('Expected telemetryStale to be a boolean.');
    }
    return TrackingSnapshotModel(
      orderId: _string(json, 'orderId'),
      trackingNumber: _string(json, 'trackingNumber'),
      status: DeliveryTrackingStatus.parse(_string(json, 'status')),
      originStation: _station(json['originStation'], 'originStation'),
      destinationStation: _station(
        json['destinationStation'],
        'destinationStation',
      ),
      estimatedDeliveryTime: estimated == null
          ? null
          : DateTime.tryParse(estimated as String) ??
                (throw const FormatException(
                  'Invalid estimatedDeliveryTime timestamp.',
                )),
      lastPosition: rawPosition == null
          ? null
          : TelemetryPositionModel.fromJson(
              Map<String, dynamic>.from(rawPosition as Map),
            ),
      telemetryStale: json['telemetryStale'] as bool,
    );
  }

  static String _string(Map<String, dynamic> json, String key) {
    final value = json[key];
    if (value is! String) throw FormatException('Expected string $key.');
    return value;
  }

  static TrackingStation _station(dynamic value, String key) {
    if (value is! Map) throw FormatException('Expected $key object.');
    final json = Map<String, dynamic>.from(value);
    return TrackingStation(
      id: _string(json, 'id'),
      name: _string(json, 'name'),
      latitude: _number(json, 'latitude'),
      longitude: _number(json, 'longitude'),
    );
  }

  static double _number(Map<String, dynamic> json, String key) {
    final value = json[key];
    if (value is! num) throw FormatException('Expected numeric $key.');
    return value.toDouble();
  }
}
