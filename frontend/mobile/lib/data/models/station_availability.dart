class StationAvailability {
  const StationAvailability({
    required this.stationId,
    required this.stationStatus,
    required this.totalSlots,
    required this.emptySlots,
    required this.occupiedSlots,
    required this.reservedSlots,
    required this.errorSlots,
    required this.isAvailableForArrival,
    required this.isAvailableForDeparture,
    required this.checkedAt,
  });

  final String stationId;
  final String stationStatus;
  final int totalSlots;
  final int emptySlots;
  final int occupiedSlots;
  final int reservedSlots;
  final int errorSlots;
  final bool isAvailableForArrival;
  final bool isAvailableForDeparture;
  final DateTime checkedAt;

  int get availableSlots => emptySlots;
  int get activeSlots => occupiedSlots + reservedSlots;
  double get occupancyRate =>
      totalSlots == 0 ? 0.0 : (occupiedSlots + reservedSlots) / totalSlots;
  bool get isOperational => stationStatus == 'OPERATIONAL';

  factory StationAvailability.fromJson(Map<String, dynamic> json) =>
      StationAvailability(
        stationId: json['stationId'] as String? ?? '',
        stationStatus: json['stationStatus'] as String? ?? 'OPERATIONAL',
        totalSlots: json['totalSlots'] as int? ?? 0,
        emptySlots: json['emptySlots'] as int? ?? 0,
        occupiedSlots: json['occupiedSlots'] as int? ?? 0,
        reservedSlots: json['reservedSlots'] as int? ?? 0,
        errorSlots: json['errorSlots'] as int? ?? 0,
        isAvailableForArrival: json['isAvailableForArrival'] as bool? ?? false,
        isAvailableForDeparture:
            json['isAvailableForDeparture'] as bool? ?? false,
        checkedAt: json['checkedAt'] != null
            ? DateTime.parse(json['checkedAt'] as String)
            : DateTime.now().toUtc(),
      );

  Map<String, dynamic> toJson() => {
    'stationId': stationId,
    'stationStatus': stationStatus,
    'totalSlots': totalSlots,
    'emptySlots': emptySlots,
    'occupiedSlots': occupiedSlots,
    'reservedSlots': reservedSlots,
    'errorSlots': errorSlots,
    'isAvailableForArrival': isAvailableForArrival,
    'isAvailableForDeparture': isAvailableForDeparture,
    'checkedAt': checkedAt.toIso8601String(),
  };
}
