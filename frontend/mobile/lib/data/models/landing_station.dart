class LandingStation {
  const LandingStation({
    required this.id,
    required this.stationCode,
    required this.name,
    required this.address,
    required this.status,
    this.latitude,
    this.longitude,
    this.totalSlots,
    this.availableSlots,
    this.operatorId,
    this.operatorName,
  });

  final String id;
  final String stationCode;
  final String name;
  final String address;
  final String status;
  final double? latitude;
  final double? longitude;
  final int? totalSlots;
  final int? availableSlots;
  final String? operatorId;
  final String? operatorName;

  factory LandingStation.fromJson(Map<String, dynamic> json) => LandingStation(
    id: json['id'] as String,
    stationCode: json['stationCode'] as String? ?? '',
    name: json['name'] as String? ?? '',
    address: json['address'] as String? ?? '',
    status: json['status'] as String? ?? 'OPERATIONAL',
    latitude: (json['latitude'] as num?)?.toDouble(),
    longitude: (json['longitude'] as num?)?.toDouble(),
    totalSlots: json['totalSlots'] as int?,
    availableSlots: json['availableSlots'] as int?,
    operatorId: json['operatorId'] as String?,
    operatorName: json['operatorName'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'stationCode': stationCode,
    'name': name,
    'address': address,
    'status': status,
    if (latitude != null) 'latitude': latitude,
    if (longitude != null) 'longitude': longitude,
    if (totalSlots != null) 'totalSlots': totalSlots,
    if (availableSlots != null) 'availableSlots': availableSlots,
    if (operatorId != null) 'operatorId': operatorId,
    if (operatorName != null) 'operatorName': operatorName,
  };
}
