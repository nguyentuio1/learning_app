import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/telemetry_position.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_snapshot.dart';
import 'package:smart_drone_delivery/presentation/customer/tracking/order_tracking_page.dart';

import '../../../support/fake_tracking_repository.dart';

Future<void> _pumpPage(
  WidgetTester tester,
  FakeTrackingRepository repository,
) async {
  tester.view.physicalSize = const Size(1000, 2200);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    ProviderScope(
      retry: (_, _) => null,
      overrides: [trackingRepositoryProvider.overrideWithValue(repository)],
      child: const MaterialApp(home: OrderTrackingPage(orderId: 'order-1')),
    ),
  );
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 100));
}

void main() {
  testWidgets('shows Retry after an API error and retries the snapshot', (
    tester,
  ) async {
    final repository = FakeTrackingRepository()
      ..queuedSnapshots.add(
        () async => throw const ApiException(
          message: 'Order not found.',
          statusCode: 404,
        ),
      );
    addTearDown(repository.close);
    await _pumpPage(tester, repository);

    expect(find.text('Retry'), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.text('DD-202611-0012'), findsOneWidget);
  });

  testWidgets('shows a loading indicator while the first snapshot is pending', (
    tester,
  ) async {
    final pendingSnapshot = Completer<TrackingSnapshot>();
    final repository = FakeTrackingRepository()
      ..queuedSnapshots.add(() async => pendingSnapshot.future);
    addTearDown(repository.close);
    await _pumpPage(tester, repository);

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    pendingSnapshot.complete(trackingSnapshot());
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.text('DD-202611-0012'), findsOneWidget);
  });

  testWidgets('renders in-transit tracking details and drone map marker', (
    tester,
  ) async {
    final repository = FakeTrackingRepository();
    addTearDown(repository.close);
    await _pumpPage(tester, repository);

    expect(find.text('DD-202611-0012'), findsOneWidget);
    expect(
      find.descendant(
        of: find.byKey(const Key('tracking-status-badge')),
        matching: find.text('IN TRANSIT'),
      ),
      findsOneWidget,
    );
    expect(find.byKey(const Key('origin-marker')), findsOneWidget);
    expect(find.byKey(const Key('destination-marker')), findsOneWidget);
    expect(find.byKey(const Key('drone-marker')), findsOneWidget);
    expect(find.byKey(const Key('tracking-stepper')), findsOneWidget);
    expect(find.byKey(const Key('live-telemetry-card')), findsOneWidget);
    expect(find.text('11.5 m/s'), findsOneWidget);
    expect(find.text('80.0 m'), findsOneWidget);
    expect(find.text('71%'), findsOneWidget);
    final eta = DateTime.utc(2026, 11, 12, 8, 24).toLocal();
    expect(
      find.text(
        '${eta.hour.toString().padLeft(2, '0')}:'
        '${eta.minute.toString().padLeft(2, '0')}',
      ),
      findsOneWidget,
    );
  });

  testWidgets('stale telemetry banner clears when a fresh position arrives', (
    tester,
  ) async {
    final repository = FakeTrackingRepository()
      ..response = trackingSnapshot(telemetryStale: true)
      ..connectOnSubscribe = null;
    addTearDown(repository.close);
    await _pumpPage(tester, repository);

    expect(find.text('Live position stale (>60s)'), findsOneWidget);
    final oldPosition = trackingSnapshot().lastPosition!;
    repository.emitPosition(
      TelemetryPosition(
        latitude: oldPosition.latitude,
        longitude: oldPosition.longitude,
        altitudeMeters: oldPosition.altitudeMeters,
        speedMps: oldPosition.speedMps,
        batteryPercentage: oldPosition.batteryPercentage,
        timestamp: DateTime.now().toUtc(),
      ),
    );
    await tester.pump();
    await tester.pump();

    expect(find.text('Live position stale (>60s)'), findsNothing);
    expect(
      find.byKey(const Key('tracking-reconnecting-banner')),
      findsOneWidget,
    );
  });

  testWidgets(
    'keeps telemetry card visible when an in-transit drone has no fix',
    (tester) async {
      final repository = FakeTrackingRepository()
        ..response = trackingSnapshot(includePosition: false)
        ..connectOnSubscribe = null;
      addTearDown(repository.close);
      await _pumpPage(tester, repository);

      expect(find.byKey(const Key('live-telemetry-card')), findsOneWidget);
      expect(find.byKey(const Key('drone-marker')), findsNothing);
      expect(find.text('Unavailable'), findsNWidgets(3));
    },
  );

  for (final status in [
    DeliveryTrackingStatus.readyForTakeoff,
    DeliveryTrackingStatus.arrived,
    DeliveryTrackingStatus.delivered,
  ]) {
    testWidgets('${status.apiValue} without telemetry hides live drone data', (
      tester,
    ) async {
      final repository = FakeTrackingRepository()
        ..response = trackingSnapshot(status: status, includePosition: false)
        ..connectOnSubscribe = null;
      addTearDown(repository.close);
      await _pumpPage(tester, repository);

      expect(find.byKey(const Key('origin-marker')), findsOneWidget);
      expect(find.byKey(const Key('destination-marker')), findsOneWidget);
      expect(find.byKey(const Key('drone-marker')), findsNothing);
      expect(find.byKey(const Key('live-telemetry-card')), findsNothing);
      expect(
        find.descendant(
          of: find.byKey(const Key('tracking-status-badge')),
          matching: find.text(status.apiValue.replaceAll('_', ' ')),
        ),
        findsOneWidget,
      );
    });
  }
}
