class OrderStation {
  const OrderStation({
    required this.id,
    required this.stationCode,
    required this.name,
    required this.status,
  });

  final String id;
  final String stationCode;
  final String name;
  final String status;

  factory OrderStation.fromJson(Map<String, dynamic> json) => OrderStation(
    id: json['id'] as String,
    stationCode: json['stationCode'] as String,
    name: json['name'] as String,
    status: json['status'] as String,
  );
}
