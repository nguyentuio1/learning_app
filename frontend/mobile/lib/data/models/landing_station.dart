class LandingStation {
  const LandingStation({
    required this.id,
    required this.stationCode,
    required this.name,
    required this.address,
    required this.status,
  });

  final String id;
  final String stationCode;
  final String name;
  final String address;
  final String status;

  factory LandingStation.fromJson(Map<String, dynamic> json) => LandingStation(
    id: json['id'] as String,
    stationCode: json['stationCode'] as String,
    name: json['name'] as String,
    address: json['address'] as String,
    status: json['status'] as String,
  );
}
