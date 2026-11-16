import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/data/models/expired_parcel.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/station_availability.dart';
import 'package:smart_drone_delivery/data/models/station_detail.dart';
import 'package:smart_drone_delivery/data/models/station_operation_models.dart';
import 'package:smart_drone_delivery/data/models/station_slot.dart';
import 'package:smart_drone_delivery/data/providers/expired_parcel_providers.dart';
import 'package:smart_drone_delivery/data/providers/landing_station_providers.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/data/services/landing_station_api_service.dart';
import 'package:smart_drone_delivery/data/services/station_operations_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/home/operator_home_page.dart';
import 'package:smart_drone_delivery/presentation/operator/home/station_availability_providers.dart';
import 'package:smart_drone_delivery/presentation/operator/intake/package_intake_page.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/pickup_controller.dart';

class _FakeStationOperationsApiService extends StationOperationsApiService {
  _FakeStationOperationsApiService({
    required this.orderInfo,
  }) : super(Dio());

  StationOrderInfo orderInfo;
  PackageIntakeRequest? lastIntakeRequest;
  RejectIntakeRequest? lastRejectRequest;
  String? lastStationId;

  @override
  Future<StationOrderInfo> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async {
    lastStationId = stationId;
    return orderInfo;
  }

  @override
  Future<PackageIntakeResponse> confirmIntake({
    required String stationId,
    required PackageIntakeRequest request,
  }) async {
    lastStationId = stationId;
    lastIntakeRequest = request;
    return PackageIntakeResponse(
      orderId: orderInfo.orderId,
      trackingNumber: request.trackingNumber,
      status: 'IN_ORIGIN_STATION',
      slotId: request.slotId,
      slotNumber: 1,
      confirmedAt: DateTime.utc(2026, 11, 16, 12),
    );
  }

  @override
  Future<RejectIntakeResponse> rejectIntake({
    required String stationId,
    required RejectIntakeRequest request,
  }) async {
    lastStationId = stationId;
    lastRejectRequest = request;
    return RejectIntakeResponse(
      orderId: orderInfo.orderId,
      trackingNumber: request.trackingNumber,
      status: 'REJECTED',
    );
  }
}

class _FakeLandingStationApiService extends LandingStationApiService {
  _FakeLandingStationApiService() : super(Dio());

  String? lastSlotId;
  UpdateSlotStatusRequest? lastSlotRequest;

  @override
  Future<StationSlot> updateSlotStatus({
    required String slotId,
    required UpdateSlotStatusRequest request,
  }) async {
    lastSlotId = slotId;
    lastSlotRequest = request;
    return StationSlot(
      id: slotId,
      stationId: 'station-1',
      slotNumber: 1,
      status: request.targetStatus,
      lastUpdated: DateTime.utc(2026, 11, 16, 12),
    );
  }
}

class _StubStationAvailabilityNotifier extends StationAvailabilityNotifier {
  _StubStationAvailabilityNotifier(this._state);

  final StationAvailabilityState _state;
  int refreshCalls = 0;

  @override
  Future<StationAvailabilityState> build() async => _state;

  @override
  Future<void> refresh() async {
    refreshCalls++;
    state = AsyncData(_state);
  }
}

void main() {
  const stationId = 'station-1';
  const trackingNumber = 'TRK-2026-0001';

  final station = LandingStation(
    id: stationId,
    stationCode: 'SGN-01',
    name: 'District 1 Hub',
    address: '123 Nguyen Hue',
    latitude: 10.77,
    longitude: 106.70,
    status: 'OPERATIONAL',
    totalSlots: 3,
    availableSlots: 1,
  );

  final availability = StationAvailability(
    stationId: stationId,
    stationStatus: 'OPERATIONAL',
    totalSlots: 3,
    emptySlots: 1,
    occupiedSlots: 1,
    reservedSlots: 0,
    errorSlots: 1,
    isAvailableForArrival: true,
    isAvailableForDeparture: true,
    checkedAt: DateTime.utc(2026, 11, 16, 12),
  );

  final slots = [
    StationSlot(
      id: 'slot-empty-1',
      stationId: stationId,
      slotNumber: 1,
      status: 'EMPTY',
      lastUpdated: DateTime.utc(2026, 11, 16, 12),
    ),
    StationSlot(
      id: 'slot-error-2',
      stationId: stationId,
      slotNumber: 2,
      status: 'ERROR',
      lastUpdated: DateTime.utc(2026, 11, 16, 12),
    ),
    StationSlot(
      id: 'slot-occupied-3',
      stationId: stationId,
      slotNumber: 3,
      status: 'OCCUPIED',
      lastUpdated: DateTime.utc(2026, 11, 16, 12),
      currentOrderId: 'order-99',
    ),
  ];

  final detail = StationDetail(
    id: stationId,
    stationCode: 'SGN-01',
    name: 'District 1 Hub',
    address: '123 Nguyen Hue',
    latitude: 10.77,
    longitude: 106.70,
    status: 'OPERATIONAL',
    totalSlots: 3,
    emptySlots: 1,
    createdAt: DateTime.utc(2026, 11, 1),
    updatedAt: DateTime.utc(2026, 11, 16),
    slots: slots,
    hostedDroneCount: 1,
  );

  final stationState = StationAvailabilityState(
    stations: [station],
    selectedStationId: stationId,
    availability: availability,
    detail: detail,
  );

  StationOrderInfo buildOrder({
    String role = 'ORIGIN',
    String status = 'APPROVED',
  }) =>
      StationOrderInfo(
        orderId: 'order-1',
        trackingNumber: trackingNumber,
        status: status,
        role: role,
        packageName: 'Medical Kit',
        declaredWeightGram: 450,
        declaredLengthCm: 20,
        declaredWidthCm: 15,
        declaredHeightCm: 10,
        isFragile: true,
        originStationId: stationId,
        destinationStationId: 'station-2',
      );

  group('LA-170 models and endpoints', () {
    test('endpoints match backend controller routes', () {
      expect(
        ApiEndpoints.stationIntake('st-1'),
        '/landing-stations/st-1/operations/intake',
      );
      expect(
        ApiEndpoints.stationIntakeReject('st-1'),
        '/landing-stations/st-1/operations/intake-reject',
      );
      expect(
        ApiEndpoints.updateSlotStatus('slot-1'),
        '/station-slots/slot-1/status',
      );
    });

    test('UpdateSlotStatusRequest omits blank reason', () {
      const noReason = UpdateSlotStatusRequest(
        targetStatus: 'ERROR',
        reason: '   ',
      );
      expect(noReason.toJson(), {'targetStatus': 'ERROR'});

      const withReason = UpdateSlotStatusRequest(
        targetStatus: 'ERROR',
        reason: 'Sensor fault',
      );
      expect(withReason.toJson(), {
        'targetStatus': 'ERROR',
        'reason': 'Sensor fault',
      });
    });

    test('StationOrderInfo.isEligibleForIntake requires ORIGIN and APPROVED', () {
      expect(buildOrder(role: 'ORIGIN', status: 'APPROVED').isEligibleForIntake,
          isTrue);
      expect(
          buildOrder(role: 'DESTINATION', status: 'APPROVED').isEligibleForIntake,
          isFalse);
      expect(buildOrder(role: 'ORIGIN', status: 'ARRIVED').isEligibleForIntake,
          isFalse);
    });
  });

  group('PackageIntakePage', () {
    testWidgets('confirms origin intake with measured dimensions and slot',
        (tester) async {
      final fakeOps = _FakeStationOperationsApiService(orderInfo: buildOrder());
      final stubNotifier = _StubStationAvailabilityNotifier(stationState);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeOps),
            stationAvailabilityNotifierProvider
                .overrideWith(() => stubNotifier),
          ],
          child: const MaterialApp(home: PackageIntakePage()),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('intake-tracking-input')),
        'trk-2026-0001',
      );
      await tester.tap(find.byKey(const Key('intake-lookup-button')));
      await tester.pumpAndSettle();

      expect(find.text('Medical Kit'), findsOneWidget);
      expect(find.text('Fragile handling required'), findsOneWidget);

      await tester.ensureVisible(
        find.byKey(const Key('intake-confirm-button')),
      );
      await tester.tap(find.byKey(const Key('intake-confirm-button')));
      await tester.pumpAndSettle();

      expect(fakeOps.lastIntakeRequest, isNotNull);
      expect(fakeOps.lastIntakeRequest!.trackingNumber, trackingNumber);
      expect(fakeOps.lastIntakeRequest!.slotId, 'slot-empty-1');
      expect(fakeOps.lastIntakeRequest!.measuredWeightGram, 450);
      expect(fakeOps.lastIntakeRequest!.visualInspectionPassed, isTrue);
      expect(find.text('Package Intake Confirmed'), findsOneWidget);
      expect(stubNotifier.refreshCalls, 1);
    });

    testWidgets('rejects origin intake with reason dialog', (tester) async {
      final fakeOps = _FakeStationOperationsApiService(orderInfo: buildOrder());
      final stubNotifier = _StubStationAvailabilityNotifier(stationState);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeOps),
            stationAvailabilityNotifierProvider
                .overrideWith(() => stubNotifier),
          ],
          child: const MaterialApp(home: PackageIntakePage()),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('intake-tracking-input')),
        trackingNumber,
      );
      await tester.tap(find.byKey(const Key('intake-lookup-button')));
      await tester.pumpAndSettle();

      await tester.ensureVisible(
        find.byKey(const Key('intake-reject-button')),
      );
      await tester.tap(find.byKey(const Key('intake-reject-button')));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('intake-reject-reason-input')),
        'Box torn',
      );
      await tester.tap(find.byKey(const Key('intake-reject-confirm-button')));
      await tester.pumpAndSettle();

      expect(fakeOps.lastRejectRequest, isNotNull);
      expect(fakeOps.lastRejectRequest!.reason, 'Box torn');
      expect(find.text('Package Intake Rejected'), findsOneWidget);
      expect(stubNotifier.refreshCalls, 1);
    });

    testWidgets('blocks intake for non-ORIGIN or non-APPROVED order',
        (tester) async {
      final fakeOps = _FakeStationOperationsApiService(
        orderInfo: buildOrder(role: 'DESTINATION', status: 'ARRIVED'),
      );
      final stubNotifier = _StubStationAvailabilityNotifier(stationState);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeOps),
            stationAvailabilityNotifierProvider
                .overrideWith(() => stubNotifier),
          ],
          child: const MaterialApp(home: PackageIntakePage()),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('intake-tracking-input')),
        trackingNumber,
      );
      await tester.tap(find.byKey(const Key('intake-lookup-button')));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'Only ORIGIN orders in APPROVED status can be accepted for intake at this station.',
        ),
        findsOneWidget,
      );
      expect(find.byKey(const Key('intake-confirm-button')), findsNothing);
    });
  });

  group('OperatorHomePage slot management & notifications', () {
    testWidgets(
        'renders intake, pickup, notification badge, and toggles EMPTY/ERROR slots',
        (tester) async {
      final fakeStationApi = _FakeLandingStationApiService();
      final stubNotifier = _StubStationAvailabilityNotifier(stationState);

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            landingStationApiServiceProvider.overrideWithValue(fakeStationApi),
            stationAvailabilityNotifierProvider
                .overrideWith(() => stubNotifier),
            expiredParcelsProvider.overrideWith(
              (_, _) async => const <ExpiredParcel>[],
            ),
            unreadNotificationsCountProvider.overrideWith((_) async => 3),
          ],
          child: const MaterialApp(home: OperatorHomePage()),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('operator-intake-button')), findsOneWidget);
      expect(find.byKey(const Key('operator-pickup-button')), findsOneWidget);
      expect(
        find.byKey(const Key('operator-notifications-button')),
        findsOneWidget,
      );
      expect(find.text('3'), findsOneWidget);

      // Tap Slot #1 (EMPTY) -> Mark Maintenance (ERROR)
      await tester.ensureVisible(find.text('Slot #1'));
      await tester.tap(find.text('Slot #1'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('slot-status-reason-input')),
        'Door stuck',
      );
      await tester.tap(find.byKey(const Key('slot-status-toggle-button')));
      await tester.pumpAndSettle();

      expect(fakeStationApi.lastSlotId, 'slot-empty-1');
      expect(fakeStationApi.lastSlotRequest?.targetStatus, 'ERROR');
      expect(fakeStationApi.lastSlotRequest?.reason, 'Door stuck');
      expect(stubNotifier.refreshCalls, 1);

      // Tap Slot #2 (ERROR) -> Restore Operational (EMPTY)
      await tester.ensureVisible(find.text('Slot #2'));
      await tester.tap(find.text('Slot #2'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('slot-status-toggle-button')));
      await tester.pumpAndSettle();

      expect(fakeStationApi.lastSlotId, 'slot-error-2');
      expect(fakeStationApi.lastSlotRequest?.targetStatus, 'EMPTY');
      expect(stubNotifier.refreshCalls, 2);

      // Tap Slot #3 (OCCUPIED) -> no toggle button
      await tester.ensureVisible(find.text('Slot #3'));
      await tester.tap(find.text('Slot #3'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('slot-status-toggle-button')), findsNothing);
    });
  });
}
