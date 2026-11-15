import 'tracking_snapshot.dart';

class TrackingStatusUpdate {
  const TrackingStatusUpdate({
    required this.orderId,
    required this.trackingNumber,
    required this.status,
    required this.updatedAt,
  });

  final String orderId;
  final String trackingNumber;
  final DeliveryTrackingStatus status;
  final DateTime updatedAt;
}
