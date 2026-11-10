class StationSlot {
  const StationSlot({
    required this.id,
    required this.stationId,
    required this.slotNumber,
    required this.status,
    required this.lastUpdated,
    this.currentOrderId,
    this.currentMissionId,
  });

  final String id;
  final String stationId;
  final int slotNumber;
  final String status; // EMPTY, OCCUPIED, RESERVED, ERROR
  final DateTime lastUpdated;
  final String? currentOrderId;
  final String? currentMissionId;

  bool get isEmpty => status == 'EMPTY';
  bool get isOccupied => status == 'OCCUPIED';
  bool get isReserved => status == 'RESERVED';
  bool get isError => status == 'ERROR';

  factory StationSlot.fromJson(Map<String, dynamic> json) => StationSlot(
    id: json['id'] as String? ?? '',
    stationId: json['stationId'] as String? ?? '',
    slotNumber: json['slotNumber'] as int? ?? 0,
    status: json['status'] as String? ?? 'EMPTY',
    lastUpdated: json['lastUpdated'] != null
        ? DateTime.parse(json['lastUpdated'] as String)
        : DateTime.now().toUtc(),
    currentOrderId: json['currentOrderId'] as String?,
    currentMissionId: json['currentMissionId'] as String?,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'stationId': stationId,
    'slotNumber': slotNumber,
    'status': status,
    'lastUpdated': lastUpdated.toIso8601String(),
    if (currentOrderId != null) 'currentOrderId': currentOrderId,
    if (currentMissionId != null) 'currentMissionId': currentMissionId,
  };
}
