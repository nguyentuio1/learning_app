import 'station_slot.dart';

class StationDetail {
  const StationDetail({
    required this.id,
    required this.stationCode,
    required this.name,
    required this.address,
    required this.latitude,
    required this.longitude,
    required this.status,
    required this.totalSlots,
    required this.emptySlots,
    this.operatorId,
    this.operatorName,
    required this.createdAt,
    required this.updatedAt,
    required this.slots,
    required this.hostedDroneCount,
  });

  final String id;
  final String stationCode;
  final String name;
  final String address;
  final double latitude;
  final double longitude;
  final String status;
  final int totalSlots;
  final int emptySlots;
  final String? operatorId;
  final String? operatorName;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<StationSlot> slots;
  final int hostedDroneCount;

  factory StationDetail.fromJson(Map<String, dynamic> json) => StationDetail(
    id: json['id'] as String? ?? '',
    stationCode: json['stationCode'] as String? ?? '',
    name: json['name'] as String? ?? '',
    address: json['address'] as String? ?? '',
    latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
    longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
    status: json['status'] as String? ?? 'OPERATIONAL',
    totalSlots: json['totalSlots'] as int? ?? 0,
    emptySlots: json['emptySlots'] as int? ?? 0,
    operatorId: json['operatorId'] as String?,
    operatorName: json['operatorName'] as String?,
    createdAt: json['createdAt'] != null
        ? DateTime.parse(json['createdAt'] as String)
        : DateTime.now().toUtc(),
    updatedAt: json['updatedAt'] != null
        ? DateTime.parse(json['updatedAt'] as String)
        : DateTime.now().toUtc(),
    slots: (json['slots'] as List<dynamic>? ?? const [])
        .map(
          (item) => StationSlot.fromJson(
            Map<String, dynamic>.from(item as Map<dynamic, dynamic>),
          ),
        )
        .toList(growable: false),
    hostedDroneCount: json['hostedDroneCount'] as int? ?? 0,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'stationCode': stationCode,
    'name': name,
    'address': address,
    'latitude': latitude,
    'longitude': longitude,
    'status': status,
    'totalSlots': totalSlots,
    'emptySlots': emptySlots,
    if (operatorId != null) 'operatorId': operatorId,
    if (operatorName != null) 'operatorName': operatorName,
    'createdAt': createdAt.toIso8601String(),
    'updatedAt': updatedAt.toIso8601String(),
    'slots': slots.map((s) => s.toJson()).toList(),
    'hostedDroneCount': hostedDroneCount,
  };
}
