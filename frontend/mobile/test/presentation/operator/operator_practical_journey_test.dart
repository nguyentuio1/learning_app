import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/session/auth_controller.dart';
import 'package:smart_drone_delivery/data/models/expired_parcel.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/station_availability.dart';
import 'package:smart_drone_delivery/data/providers/expired_parcel_providers.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/presentation/operator/home/operator_home_page.dart';
import 'package:smart_drone_delivery/presentation/operator/home/station_availability_providers.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

class _TestStationAvailabilityNotifier extends StationAvailabilityNotifier {
  _TestStationAvailabilityNotifier(
    this._initialState, {
    this.initialError,
    this.loading = false,
  });

  final StationAvailabilityState _initialState;
  final String? initialError;
  final bool loading;
  int refreshCalls = 0;

  @override
  Future<StationAvailabilityState> build() async {
    if (loading) {
      await Completer<StationAvailabilityState>().future;
    }
    if (initialError != null) {
      throw StateError(initialError!);
    }
    return _initialState;
  }

  @override
  Future<void> refresh() async {
    refreshCalls++;
    state = AsyncData(_initialState);
  }
}

class _TestCurrentUserController extends CurrentUserController {
  @override
  User? build() => User(
    id: 'operator-1',
    email: 'operator@example.com',
    fullName: 'Station Operator',
    phoneNumber: '0900000001',
    roleCode: 'STATION_OPERATOR',
    status: 'ACTIVE',
    createdAt: DateTime.utc(2026, 11, 1),
  );
}

void main() {
  const stationId = 'station-1';
  const assignedStation = LandingStation(
    id: stationId,
    stationCode: 'SGN-01',
    name: 'Assigned Hub',
    address: '123 Nguyen Hue',
    latitude: 10.77,
    longitude: 106.70,
    status: 'OPERATIONAL',
    totalSlots: 4,
    availableSlots: 2,
  );

  StationAvailabilityState assignedState({
    int emptySlots = 2,
    int occupiedSlots = 1,
  }) => StationAvailabilityState(
    stations: const [assignedStation],
    selectedStationId: stationId,
    availability: StationAvailability(
      stationId: stationId,
      stationStatus: 'OPERATIONAL',
      totalSlots: emptySlots + occupiedSlots,
      emptySlots: emptySlots,
      occupiedSlots: occupiedSlots,
      reservedSlots: 0,
      errorSlots: 0,
      isAvailableForArrival: emptySlots > 0,
      isAvailableForDeparture: occupiedSlots > 0,
      checkedAt: DateTime.utc(2026, 11, 18),
    ),
    detail: null,
  );

  Future<void> pumpHome(
    WidgetTester tester, {
    required Widget app,
    required _TestStationAvailabilityNotifier notifier,
    List<PickupTimeoutAlert> alerts = const [],
  }) => tester.pumpWidget(
    ProviderScope(
      overrides: [
        stationAvailabilityNotifierProvider.overrideWith(() => notifier),
        currentUserProvider.overrideWith(_TestCurrentUserController.new),
        pickupTimeoutAlertsProvider.overrideWith((_, _) async => alerts),
        unreadNotificationsCountProvider.overrideWith((_) async => 0),
      ],
      child: app,
    ),
  );

  testWidgets(
    'assigned operator sees assigned station and pending action summary',
    (tester) async {
      final notifier = _TestStationAvailabilityNotifier(assignedState());
      await pumpHome(
        tester,
        notifier: notifier,
        app: const MaterialApp(home: OperatorHomePage()),
        alerts: const [
          PickupTimeoutAlert(
            id: 'alert-1',
            slotId: 'slot-1',
            trackingNumber: 'TRK-001',
            title: 'Pickup window expired',
            message: 'Move parcel from slot.',
          ),
          PickupTimeoutAlert(
            id: 'alert-2',
            slotId: 'slot-2',
            trackingNumber: 'TRK-002',
            title: 'Pickup window expired',
            message: 'Move parcel from slot.',
          ),
        ],
      );
      await tester.pumpAndSettle();

      expect(find.text('Assigned Hub'), findsOneWidget);
      expect(
        find.byKey(const Key('operator-assigned-station-badge')),
        findsOneWidget,
      );
      expect(find.text('Assigned station'), findsOneWidget);
      expect(find.text('Defaulted to your assigned station'), findsOneWidget);
      expect(
        find.byKey(const Key('operator-action-queue-card')),
        findsOneWidget,
      );
      expect(find.text('Station Action Queue'), findsOneWidget);
      expect(find.text('2 pickup timeout alerts'), findsOneWidget);
      expect(find.text('Review expired'), findsOneWidget);
      expect(find.text('2 empty slots ready for intake'), findsOneWidget);
      expect(find.text('1 active slots in station'), findsOneWidget);
    },
  );

  testWidgets('unassigned operator sees guidance but no station actions', (
    tester,
  ) async {
    final notifier = _TestStationAvailabilityNotifier(
      const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
        isUnassignedOperator: true,
      ),
    );
    await pumpHome(
      tester,
      notifier: notifier,
      app: const MaterialApp(home: OperatorHomePage()),
    );
    await tester.pumpAndSettle();

    expect(find.text('No station assigned'), findsOneWidget);
    expect(find.byKey(const Key('operator-intake-button')), findsNothing);
    expect(find.byKey(const Key('operator-pickup-button')), findsNothing);
    expect(find.byKey(const Key('operator-action-queue-card')), findsNothing);
  });

  testWidgets('empty station list hides station actions', (tester) async {
    final notifier = _TestStationAvailabilityNotifier(
      const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
      ),
    );
    await pumpHome(
      tester,
      notifier: notifier,
      app: const MaterialApp(home: OperatorHomePage()),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('operator-intake-button')), findsNothing);
    expect(find.byKey(const Key('operator-pickup-button')), findsNothing);
    expect(find.byKey(const Key('operator-action-queue-card')), findsNothing);
  });

  testWidgets('loading state shows progress indicator', (tester) async {
    final notifier = _TestStationAvailabilityNotifier(
      assignedState(),
      loading: true,
    );
    await pumpHome(
      tester,
      notifier: notifier,
      app: const MaterialApp(home: OperatorHomePage()),
    );
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('error state Retry refreshes station availability', (
    tester,
  ) async {
    final notifier = _TestStationAvailabilityNotifier(
      assignedState(),
      initialError: 'Station load failed',
    );
    await pumpHome(
      tester,
      notifier: notifier,
      app: const MaterialApp(home: OperatorHomePage()),
    );
    await tester.pumpAndSettle();

    expect(find.text('Retry'), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();

    expect(notifier.refreshCalls, 1);
    expect(find.text('Assigned Hub'), findsOneWidget);
  });

  testWidgets('intake and pickup navigation refreshes on return', (
    tester,
  ) async {
    final notifier = _TestStationAvailabilityNotifier(assignedState());
    final router = GoRouter(
      initialLocation: RoutePaths.operatorHome,
      routes: [
        GoRoute(
          path: RoutePaths.operatorHome,
          builder: (context, state) => const OperatorHomePage(),
        ),
        GoRoute(
          path: RoutePaths.operatorIntake,
          builder: (context, state) =>
              const Scaffold(body: Text('Origin Intake route')),
        ),
        GoRoute(
          path: RoutePaths.operatorPickup,
          builder: (context, state) =>
              const Scaffold(body: Text('Confirm Pickup route')),
        ),
      ],
    );
    addTearDown(router.dispose);

    await pumpHome(
      tester,
      notifier: notifier,
      app: MaterialApp.router(routerConfig: router),
    );
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byKey(const Key('operator-intake-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('operator-intake-button')));
    await tester.pumpAndSettle();
    expect(find.text('Origin Intake route'), findsOneWidget);
    router.pop();
    await tester.pumpAndSettle();
    expect(notifier.refreshCalls, 1);

    await tester.ensureVisible(find.byKey(const Key('operator-pickup-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('operator-pickup-button')));
    await tester.pumpAndSettle();
    expect(find.text('Confirm Pickup route'), findsOneWidget);
    router.pop();
    await tester.pumpAndSettle();
    expect(notifier.refreshCalls, 2);
  });
}
