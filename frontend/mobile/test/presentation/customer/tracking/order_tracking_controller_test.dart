import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
// fake_async is already a Flutter test dependency.
// ignore: depend_on_referenced_packages
import 'package:fake_async/fake_async.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/domain/entities/telemetry_position.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_snapshot.dart';
import 'package:smart_drone_delivery/domain/entities/tracking_status_update.dart';
import 'package:smart_drone_delivery/presentation/customer/tracking/order_tracking_controller.dart';

import '../../../support/fake_tracking_repository.dart';

Future<void> _waitForInitialSnapshot(
  ProviderContainer container,
  NotifierProvider<OrderTrackingController, AsyncValue<OrderTrackingState>>
  provider,
) async {
  for (var attempt = 0; attempt < 10; attempt++) {
    if (!container.read(provider).isLoading) return;
    await Future<void>.delayed(Duration.zero);
  }
  fail('Tracking snapshot did not finish loading.');
}

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

      await _waitForInitialSnapshot(container, provider);
      await Future<void>.delayed(Duration.zero);
      expect(repository.requests, ['order-1', 'order-1']);
      expect(repository.subscriptions, ['order-1']);

      final updatedPosition = TelemetryPosition(
        latitude: 10.82,
        longitude: 106.74,
        altitudeMeters: 95,
        speedMps: 13,
        batteryPercentage: 68,
        timestamp: DateTime.now().toUtc(),
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

      await _waitForInitialSnapshot(container, provider);
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

  test(
    'keeps server null position and stale flag canonical after resnapshot',
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

      await _waitForInitialSnapshot(container, provider);
      await Future<void>.delayed(Duration.zero);
      repository.emitPosition(
        TelemetryPosition(
          latitude: 10.83,
          longitude: 106.75,
          altitudeMeters: 70,
          speedMps: 12,
          batteryPercentage: 65,
          timestamp: DateTime.now().toUtc(),
        ),
      );
      await Future<void>.delayed(Duration.zero);
      expect(
        container.read(provider).requireValue.snapshot.lastPosition,
        isNotNull,
      );

      repository.response = trackingSnapshot(
        status: DeliveryTrackingStatus.inTransit,
        includePosition: false,
        telemetryStale: false,
      );
      repository.emitStatus(
        TrackingStatusUpdate(
          orderId: 'order-1',
          trackingNumber: 'DD-202611-0012',
          status: DeliveryTrackingStatus.inTransit,
          updatedAt: DateTime.now().toUtc(),
        ),
      );
      await Future<void>.delayed(Duration.zero);

      final snapshot = container.read(provider).requireValue.snapshot;
      expect(snapshot.lastPosition, isNull);
      expect(snapshot.telemetryStale, isFalse);

      repository.response = trackingSnapshot(
        status: DeliveryTrackingStatus.inTransit,
        includePosition: false,
        telemetryStale: true,
      );
      repository.emitStatus(
        TrackingStatusUpdate(
          orderId: 'order-1',
          trackingNumber: 'DD-202611-0012',
          status: DeliveryTrackingStatus.inTransit,
          updatedAt: DateTime.now().toUtc(),
        ),
      );
      await Future<void>.delayed(Duration.zero);

      final staleSnapshot = container.read(provider).requireValue.snapshot;
      expect(staleSnapshot.lastPosition, isNull);
      expect(staleSnapshot.telemetryStale, isTrue);
    },
  );

  test(
    'preserves server stale flag when device clock is 61 seconds behind',
    () {
      fakeAsync((async) {
        final deviceClock = DateTime.utc(2026, 11, 15, 11, 58, 59);
        final repository = FakeTrackingRepository()
          ..response = trackingSnapshot(
            position: TelemetryPosition(
              latitude: 10.8,
              longitude: 106.7,
              altitudeMeters: 80,
              speedMps: 11,
              batteryPercentage: 71,
              timestamp: DateTime.utc(2026, 11, 15, 12),
            ),
            telemetryStale: true,
          );
        final provider = orderTrackingControllerProvider('order-1');
        final container = ProviderContainer(
          overrides: [
            trackingRepositoryProvider.overrideWithValue(repository),
            provider.overrideWith(
              () => OrderTrackingController('order-1', now: () => deviceClock),
            ),
          ],
          retry: (_, _) => null,
        );
        final subscription = container.listen(provider, (_, _) {});

        container.read(provider);
        async.flushMicrotasks();
        async.elapse(Duration.zero);
        async.flushMicrotasks();

        final snapshot = container.read(provider).requireValue.snapshot;
        expect(snapshot.lastPosition, isNotNull);
        expect(snapshot.telemetryStale, isTrue);

        subscription.close();
        container.dispose();
        repository.close();
      });
    },
  );

  test('marks connected idle telemetry stale after 61 seconds and rejects stale samples', () {
    fakeAsync((async) {
      var now = DateTime.utc(2026, 11, 15, 12);
      final repository = FakeTrackingRepository()
        ..response = trackingSnapshot(
          position: TelemetryPosition(
            latitude: 10.8,
            longitude: 106.7,
            altitudeMeters: 80,
            speedMps: 11,
            batteryPercentage: 71,
            timestamp: now,
          ),
        );
      final provider = orderTrackingControllerProvider('order-1');
      final container = ProviderContainer(
        overrides: [
          trackingRepositoryProvider.overrideWithValue(repository),
          provider.overrideWith(
            () => OrderTrackingController('order-1', now: () => now),
          ),
        ],
        retry: (_, _) => null,
      );
      final subscription = container.listen(provider, (_, _) {});

      container.read(provider);
      async.flushMicrotasks();
      async.elapse(Duration.zero);
      async.flushMicrotasks();
      expect(container.read(provider).requireValue.isReconnecting, isFalse);
      expect(
        container.read(provider).requireValue.snapshot.telemetryStale,
        isFalse,
      );

      now = now.add(const Duration(seconds: 61));
      async.elapse(const Duration(seconds: 61));
      async.flushMicrotasks();
      expect(
        container.read(provider).requireValue.snapshot.telemetryStale,
        isTrue,
      );

      repository.emitPosition(
        TelemetryPosition(
          latitude: 10.81,
          longitude: 106.71,
          altitudeMeters: 75,
          speedMps: 10,
          batteryPercentage: 70,
          timestamp: now.subtract(const Duration(seconds: 61)),
        ),
      );
      async.flushMicrotasks();
      expect(
        container.read(provider).requireValue.snapshot.telemetryStale,
        isTrue,
      );

      subscription.close();
      container.dispose();
      repository.close();
    });
  });

  for (final statusCode in [401, 403, 404]) {
    test(
      'HTTP $statusCode clears protected tracking data and stops polling',
      () {
        fakeAsync((async) {
          var now = DateTime.utc(2026, 11, 15, 12);
          final repository = FakeTrackingRepository()
            ..connectOnSubscribe = null;
          final provider = orderTrackingControllerProvider('order-1');
          final container = ProviderContainer(
            overrides: [
              trackingRepositoryProvider.overrideWithValue(repository),
              provider.overrideWith(
                () => OrderTrackingController('order-1', now: () => now),
              ),
            ],
            retry: (_, _) => null,
          );
          final subscription = container.listen(provider, (_, _) {});
          container.read(provider);
          async.flushMicrotasks();
          async.elapse(Duration.zero);
          async.flushMicrotasks();
          expect(container.read(provider).hasValue, isTrue);

          repository.emitConnection(false);
          async.flushMicrotasks();
          repository.queuedSnapshots.add(
            () async => throw ApiException(
              message: 'Tracking access denied.',
              statusCode: statusCode,
            ),
          );
          async.elapse(const Duration(seconds: 5));
          async.flushMicrotasks();
          async.elapse(Duration.zero);
          async.flushMicrotasks();

          expect(repository.requests, hasLength(2));
          final failed = container.read(provider);
          expect(failed.hasError, isTrue);
          expect(failed.hasValue, isFalse);
          expect(repository.unsubscribeCalls, 1);
          final requestCountAfterError = repository.requests.length;

          now = now.add(const Duration(seconds: 61));
          async.elapse(const Duration(seconds: 20));
          async.flushMicrotasks();
          expect(repository.requests, hasLength(requestCountAfterError));
          expect(repository.unsubscribeCalls, 1);

          subscription.close();
          container.dispose();
          repository.close();
        });
      },
    );
  }

  test('disposal unsubscribes and ignores late stream updates', () async {
    final repository = FakeTrackingRepository();
    final container = ProviderContainer(
      overrides: [trackingRepositoryProvider.overrideWithValue(repository)],
      retry: (_, _) => null,
    );
    final provider = orderTrackingControllerProvider('order-1');
    final subscription = container.listen(provider, (_, _) {});
    await _waitForInitialSnapshot(container, provider);
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
