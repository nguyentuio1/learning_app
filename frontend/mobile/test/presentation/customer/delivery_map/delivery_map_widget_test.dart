import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/domain/entities/delivery_map_snapshot.dart';
import 'package:smart_drone_delivery/presentation/customer/delivery_map/delivery_map_widget.dart';

DeliveryMapSnapshot _snapshot({
  DeliveryMapOrderStatus status = DeliveryMapOrderStatus.inTransit,
  DroneMapTelemetry? position,
  bool includePosition = false,
  bool stale = false,
}) => DeliveryMapSnapshot(
  orderId: 'order-1',
  trackingNumber: 'TRACK-1',
  status: status,
  originStation: const MapStationPoint(
    id: 'origin',
    name: 'Origin Hub',
    latitude: 10.77,
    longitude: 106.7,
  ),
  destinationStation: const MapStationPoint(
    id: 'destination',
    name: 'Destination Hub',
    latitude: 10.85,
    longitude: 106.77,
  ),
  estimatedDeliveryTime: null,
  lastPosition: includePosition ? position : null,
  telemetryStale: stale,
);

void main() {
  testWidgets('renders station markers and route polyline without telemetry', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 700);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: DeliveryMapWidget(snapshot: _snapshot())),
      ),
    );
    await tester.pump();

    expect(find.text('Origin Hub'), findsOneWidget);
    expect(find.text('Destination Hub'), findsOneWidget);
    expect(find.byType(PolylineLayer), findsOneWidget);
    expect(
      find.byKey(const ValueKey('delivery-map-drone-marker')),
      findsNothing,
    );
  });

  testWidgets('shows a drone only in transit and displays stale banner', (
    tester,
  ) async {
    final position = DroneMapTelemetry(
      latitude: 10.8,
      longitude: 106.72,
      altitudeMeters: 60,
      speedMps: 8,
      batteryPercentage: 50,
      timestamp: DateTime.utc(2026, 11, 15),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DeliveryMapWidget(
            snapshot: _snapshot(
              position: position,
              includePosition: true,
              stale: true,
            ),
            liveUnavailable: true,
          ),
        ),
      ),
    );
    await tester.pump();

    expect(
      find.byKey(const ValueKey('delivery-map-drone-marker')),
      findsOneWidget,
    );
    expect(find.text('Live position stale (>60s)'), findsOneWidget);
    expect(
      find.text('Live tracking is temporarily unavailable. Reconnecting…'),
      findsOneWidget,
    );

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DeliveryMapWidget(
            snapshot: _snapshot(
              status: DeliveryMapOrderStatus.delivered,
              position: position,
              includePosition: true,
            ),
          ),
        ),
      ),
    );
    await tester.pump();
    expect(
      find.byKey(const ValueKey('delivery-map-drone-marker')),
      findsNothing,
    );
  });

  testWidgets('focus and fit controls are tappable without layout overflow', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 700);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: DeliveryMapWidget(
            snapshot: _snapshot(
              position: DroneMapTelemetry(
                latitude: 10.8,
                longitude: 106.72,
                altitudeMeters: 60,
                speedMps: 8,
                batteryPercentage: 50,
                timestamp: DateTime.utc(2026, 11, 15),
              ),
              includePosition: true,
            ),
          ),
        ),
      ),
    );
    await tester.pump();

    await tester.tap(find.byKey(const Key('map-focus-drone-button')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('map-fit-route-button')));
    await tester.pump();
    expect(tester.takeException(), isNull);
  });
}
