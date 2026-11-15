import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/telemetry_position.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_snapshot.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_status_update.dart';
import 'package:smart_drone_delivery/presentation/customer/tracking/order_tracking_controller.dart';

import '../../../support/fake_tracking_repository.dart';

void main() {
  test(
    'loads snapshot, applies position events locally, and refreshes on status',
    () async {
      final repository = FakeTrackingRepository();
      final container = ProviderContainer(
        overrides: [trackingRepositoryProvider.overrideWithValue(repository)],
        retry: (_, _) => null,
      );
      addTearDown(container.dispose);
      addTearDown(repository.close);
      final provider = orderTrackingControllerProvider('order-1');
      final subscription = container.listen(provider, (_, _) {});
      addTearDown(subscription.close);

      await container.read(provider.future);
      await Future<void>.delayed(Duration.zero);
      expect(repository.requests, ['order-1', 'order-1']);
      expect(repository.subscriptions, ['order-1']);

      final updatedPosition = TelemetryPosition(
        latitude: 10.82,
        longitude: 106.74,
        altitudeMeters: 95,
        speedMps: 13,
        batteryPercentage: 68,
        timestamp: DateTime.utc(2026, 11, 12, 8, 11),
      );
      repository.emitPosition(updatedPosition);
      await Future<void>.delayed(Duration.zero);
      var tracking = container.read(provider).requireValue;
      expect(tracking.snapshot.lastPosition?.latitude, 10.82);
      expect(tracking.snapshot.lastPosition?.batteryPercentage, 68);
      expect(tracking.snapshot.telemetryStale, isFalse);
      expect(repository.requests, hasLength(2));

      repository.emitStatus(
        TrackingStatusUpdate(
          orderId: 'another-order',
          trackingNumber: 'DD-OTHER',
          status: DeliveryTrackingStatus.arrived,
          updatedAt: _updatedAt,
        ),
      );
      await Future<void>.delayed(Duration.zero);
      expect(repository.requests, hasLength(2));

      repository.emitStatus(
        TrackingStatusUpdate(
          orderId: 'order-1',
          trackingNumber: 'DD-202611-0012',
          status: DeliveryTrackingStatus.arrived,
          updatedAt: _updatedAt,
        ),
      );
      repository.response = trackingSnapshot(
        status: DeliveryTrackingStatus.arrived,
        includePosition: false,
      );
      await Future<void>.delayed(Duration.zero);
      expect(repository.requests, hasLength(3));
      tracking = container.read(provider).requireValue;
      expect(tracking.snapshot.status, DeliveryTrackingStatus.arrived);
      expect(tracking.snapshot.lastPosition, isNull);
    },
  );

  test(
    'uses five-second fallback polling with one in-flight request',
    () async {
      final repository = FakeTrackingRepository()..connectOnSubscribe = null;
      final pendingPoll = Completer<TrackingSnapshot>();
      final container = ProviderContainer(
        overrides: [trackingRepositoryProvider.overrideWithValue(repository)],
        retry: (_, _) => null,
      );
      addTearDown(container.dispose);
      addTearDown(repository.close);
      final provider = orderTrackingControllerProvider('order-1');
      final subscription = container.listen(provider, (_, _) {});
      addTearDown(subscription.close);

      await container.read(provider.future);
      repository.emitConnection(false);
      repository.queuedSnapshots.add(() async => pendingPoll.future);
      await Future<void>.delayed(Duration.zero);
      expect(container.read(provider).requireValue.isReconnecting, isTrue);

      await Future<void>.delayed(const Duration(milliseconds: 5100));
      expect(repository.requests, hasLength(2));
      await Future<void>.delayed(const Duration(milliseconds: 5100));
      expect(repository.requests, hasLength(2));

      pendingPoll.complete(repository.response);
      await Future<void>.delayed(Duration.zero);
      repository.emitConnection(true);
      await Future<void>.delayed(Duration.zero);
      expect(container.read(provider).requireValue.isReconnecting, isFalse);
      expect(repository.requests, hasLength(3));
      await Future<void>.delayed(const Duration(milliseconds: 5100));
      expect(repository.requests, hasLength(3));
    },
  );

  test('disposal unsubscribes and ignores late stream updates', () async {
    final repository = FakeTrackingRepository();
    final container = ProviderContainer(
      overrides: [trackingRepositoryProvider.overrideWithValue(repository)],
      retry: (_, _) => null,
    );
    final provider = orderTrackingControllerProvider('order-1');
    final subscription = container.listen(provider, (_, _) {});
    await container.read(provider.future);
    await Future<void>.delayed(Duration.zero);

    subscription.close();
    await Future<void>.delayed(Duration.zero);
    expect(repository.unsubscribeCalls, 1);
    repository.emitPosition(
      TelemetryPosition(
        latitude: 11,
        longitude: 107,
        altitudeMeters: 20,
        speedMps: 5,
        batteryPercentage: 90,
        timestamp: DateTime.utc(2026, 11, 12, 8, 12),
      ),
    );

    container.dispose();
    await repository.close();
  });
}

final _updatedAt = DateTime.utc(2026, 11, 12, 8, 12);
