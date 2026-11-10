import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/core/storage/storage_providers.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/station_availability.dart';
import 'package:smart_drone_delivery/data/models/station_detail.dart';
import 'package:smart_drone_delivery/data/models/station_slot.dart';
import 'package:smart_drone_delivery/data/providers/landing_station_providers.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/data/services/landing_station_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/home/operator_home_page.dart';
import 'package:smart_drone_delivery/presentation/operator/home/station_availability_providers.dart';

import '../../support/fake_http_adapter.dart';
import '../../support/test_helpers.dart';

class _FakeStationService extends LandingStationApiService {
  _FakeStationService({
    required this.onGetStations,
    required this.onGetAvailability,
    required this.onGetDetail,
  }) : super(Dio());

  final Future<List<LandingStation>> Function() onGetStations;
  final Future<StationAvailability> Function(String stationId) onGetAvailability;
  final Future<StationDetail> Function(String stationId) onGetDetail;

  @override
  Future<List<LandingStation>> getStations({
    String? status,
    String? search,
  }) =>
      onGetStations();

  @override
  Future<StationAvailability> getStationAvailability(String stationId) =>
      onGetAvailability(stationId);

  @override
  Future<StationDetail> getStationDetail(String stationId) =>
      onGetDetail(stationId);
}

void main() {
  const stationId = '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44';
  const stationId2 = '8e3f6b01-2c5d-4e49-b7e2-0d9f1c4a6b55';
  const operatorUserId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

  final fakeStationsJson = [
    {
      'id': stationId,
      'stationCode': 'HCM-01',
      'name': 'District 1 Hub',
      'address': '123 Le Loi, Ben Nghe, District 1, HCMC',
      'latitude': 10.7769,
      'longitude': 106.7009,
      'status': 'OPERATIONAL',
      'totalSlots': 4,
      'emptySlots': 2,
      'operatorId': operatorUserId,
      'operatorName': 'Operator One',
    },
    {
      'id': stationId2,
      'stationCode': 'HCM-02',
      'name': 'Thu Duc Hub',
      'address': '10 Vo Van Ngan, Thu Duc',
      'latitude': 10.8506,
      'longitude': 106.7719,
      'status': 'OPERATIONAL',
      'totalSlots': 6,
      'emptySlots': 4,
      'operatorId': null,
      'operatorName': null,
    },
  ];

  final fakeAvailabilityJson = {
    'stationId': stationId,
    'stationStatus': 'OPERATIONAL',
    'totalSlots': 4,
    'emptySlots': 2,
    'occupiedSlots': 1,
    'reservedSlots': 1,
    'errorSlots': 0,
    'isAvailableForArrival': true,
    'isAvailableForDeparture': true,
    'checkedAt': '2026-11-06T03:15:00.000Z',
  };

  final fakeAvailabilityJson2 = {
    'stationId': stationId2,
    'stationStatus': 'OPERATIONAL',
    'totalSlots': 6,
    'emptySlots': 4,
    'occupiedSlots': 1,
    'reservedSlots': 1,
    'errorSlots': 0,
    'isAvailableForArrival': true,
    'isAvailableForDeparture': true,
    'checkedAt': '2026-11-06T03:16:00.000Z',
  };

  final fakeDetailJson = {
    'id': stationId,
    'stationCode': 'HCM-01',
    'name': 'District 1 Hub',
    'address': '123 Le Loi, Ben Nghe, District 1, HCMC',
    'latitude': 10.7769,
    'longitude': 106.7009,
    'status': 'OPERATIONAL',
    'totalSlots': 4,
    'emptySlots': 2,
    'operatorId': operatorUserId,
    'operatorName': 'Operator One',
    'createdAt': '2026-10-15T08:00:00.000Z',
    'updatedAt': '2026-10-15T08:00:00.000Z',
    'slots': [
      {
        'id': 'slot-1',
        'stationId': stationId,
        'slotNumber': 1,
        'status': 'EMPTY',
        'lastUpdated': '2026-11-06T03:00:00.000Z',
        'currentOrderId': null,
        'currentMissionId': null,
      },
      {
        'id': 'slot-2',
        'stationId': stationId,
        'slotNumber': 2,
        'status': 'EMPTY',
        'lastUpdated': '2026-11-06T03:00:00.000Z',
        'currentOrderId': null,
        'currentMissionId': null,
      },
      {
        'id': 'slot-3',
        'stationId': stationId,
        'slotNumber': 3,
        'status': 'OCCUPIED',
        'lastUpdated': '2026-11-06T03:10:00.000Z',
        'currentOrderId': 'order-123',
        'currentMissionId': 'mission-456',
      },
      {
        'id': 'slot-4',
        'stationId': stationId,
        'slotNumber': 4,
        'status': 'RESERVED',
        'lastUpdated': '2026-11-06T03:12:00.000Z',
        'currentOrderId': 'order-789',
        'currentMissionId': 'mission-101',
      },
    ],
    'hostedDroneCount': 2,
  };

  final defaultStations = fakeStationsJson
      .map((e) => LandingStation.fromJson(Map<String, dynamic>.from(e)))
      .toList();
  final defaultAvail1 = StationAvailability.fromJson(fakeAvailabilityJson);
  final defaultAvail2 = StationAvailability.fromJson(fakeAvailabilityJson2);
  final defaultDetail1 = StationDetail.fromJson(fakeDetailJson);

  _FakeStationService buildDefaultFakeService() => _FakeStationService(
        onGetStations: () async => defaultStations,
        onGetAvailability: (id) async =>
            id == stationId2 ? defaultAvail2 : defaultAvail1,
        onGetDetail: (id) async {
          if (id == stationId) return defaultDetail1;
          throw Exception('403 Forbidden');
        },
      );

  group('Station Availability Models', () {
    test('StationAvailability deserializes correctly and computes properties', () {
      final availability = StationAvailability.fromJson(fakeAvailabilityJson);

      expect(availability.stationId, stationId);
      expect(availability.stationStatus, 'OPERATIONAL');
      expect(availability.totalSlots, 4);
      expect(availability.emptySlots, 2);
      expect(availability.occupiedSlots, 1);
      expect(availability.reservedSlots, 1);
      expect(availability.errorSlots, 0);
      expect(availability.isAvailableForArrival, true);
      expect(availability.isAvailableForDeparture, true);
      expect(availability.availableSlots, 2);
      expect(availability.activeSlots, 2);
      expect(availability.occupancyRate, 0.5);
      expect(availability.isOperational, true);

      final json = availability.toJson();
      expect(json['stationId'], stationId);
      expect(json['emptySlots'], 2);
      expect(json['isAvailableForArrival'], true);
    });

    test('StationSlot deserializes correctly with status helpers', () {
      final emptySlot = StationSlot.fromJson(
        Map<String, dynamic>.from(
          (fakeDetailJson['slots'] as List).first as Map,
        ),
      );

      expect(emptySlot.slotNumber, 1);
      expect(emptySlot.status, 'EMPTY');
      expect(emptySlot.isEmpty, true);
      expect(emptySlot.isOccupied, false);
      expect(emptySlot.isReserved, false);
      expect(emptySlot.isError, false);

      final occupiedSlot = StationSlot.fromJson(
        Map<String, dynamic>.from(
          (fakeDetailJson['slots'] as List)[2] as Map,
        ),
      );

      expect(occupiedSlot.slotNumber, 3);
      expect(occupiedSlot.status, 'OCCUPIED');
      expect(occupiedSlot.isEmpty, false);
      expect(occupiedSlot.isOccupied, true);
      expect(occupiedSlot.currentOrderId, 'order-123');
      expect(occupiedSlot.currentMissionId, 'mission-456');
    });

    test('StationDetail deserializes correctly with slots and hostedDroneCount', () {
      final detail = StationDetail.fromJson(fakeDetailJson);

      expect(detail.id, stationId);
      expect(detail.stationCode, 'HCM-01');
      expect(detail.name, 'District 1 Hub');
      expect(detail.totalSlots, 4);
      expect(detail.emptySlots, 2);
      expect(detail.hostedDroneCount, 2);
      expect(detail.slots.length, 4);
      expect(detail.slots[0].status, 'EMPTY');
      expect(detail.slots[2].status, 'OCCUPIED');
    });

    test('LandingStation deserializes real backend LandingStationDto with emptySlots', () {
      final backendDtoJson = {
        'id': stationId,
        'stationCode': 'HCM-01',
        'name': 'District 1 Hub',
        'address': '123 Le Loi, Ben Nghe, District 1, HCMC',
        'latitude': 10.7769,
        'longitude': 106.7009,
        'status': 'OPERATIONAL',
        'totalSlots': 4,
        'emptySlots': 2,
        'operatorId': operatorUserId,
        'operatorName': 'Operator One',
        'createdAt': '2026-10-15T08:00:00.000Z',
        'updatedAt': '2026-10-15T08:00:00.000Z',
      };

      final station = LandingStation.fromJson(backendDtoJson);
      expect(station.id, stationId);
      expect(station.stationCode, 'HCM-01');
      expect(station.totalSlots, 4);
      // Verified: availableSlots correctly reads emptySlots from backend DTO
      expect(station.availableSlots, 2);
      expect(station.operatorId, operatorUserId);
    });
  });

  group('LandingStationApiService', () {
    late Dio dio;
    late LandingStationApiService service;

    setUp(() {
      dio = Dio(BaseOptions(baseUrl: 'http://test/api'));
      dio.httpClientAdapter = FakeHttpAdapter((options) {
        if (options.path.endsWith('/availability')) {
          return jsonBody(fakeAvailabilityJson);
        }
        if (options.path == '${ApiEndpoints.landingStations}/$stationId') {
          return jsonBody(fakeDetailJson);
        }
        if (options.path == ApiEndpoints.landingStations) {
          return jsonBody(fakeStationsJson);
        }
        return jsonBody({'message': 'Not Found'}, 404);
      });
      service = LandingStationApiService(dio);
    });

    test('getStations returns list of landing stations', () async {
      final stations = await service.getStations();
      expect(stations.length, 2);
      expect(stations.first.stationCode, 'HCM-01');
      expect(stations.first.name, 'District 1 Hub');
    });

    test('getStationAvailability returns availability metrics', () async {
      final availability = await service.getStationAvailability(stationId);
      expect(availability.totalSlots, 4);
      expect(availability.emptySlots, 2);
      expect(availability.isAvailableForArrival, true);
    });

    test('getStationDetail returns details with slots breakdown', () async {
      final detail = await service.getStationDetail(stationId);
      expect(detail.name, 'District 1 Hub');
      expect(detail.slots.length, 4);
      expect(detail.hostedDroneCount, 2);
    });
  });

  group('StationAvailabilityNotifier', () {
    late InMemoryUserStorage userStorage;

    setUp(() {
      userStorage = InMemoryUserStorage();
    });

    test('build() initializes with operator assigned station', () async {
      await userStorage.write(
        User(
          id: operatorUserId,
          email: 'operator@example.com',
          fullName: 'Operator One',
          phoneNumber: '0901234567',
          status: 'ACTIVE',
          roleCode: 'STATION_OPERATOR',
          createdAt: DateTime(2026, 1, 1),
        ),
      );

      final container = createContainer(
        overrides: [
          landingStationApiServiceProvider
              .overrideWithValue(buildDefaultFakeService()),
          userStorageProvider.overrideWithValue(userStorage),
        ],
      );
      final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});
      addTearDown(sub.close);

      final state = await container.read(
        stationAvailabilityNotifierProvider.future,
      );

      // Station operator dropdown is restricted to their assigned station
      expect(state.stations.length, 1);
      expect(state.isUnassignedOperator, false);
      expect(state.selectedStationId, stationId);
      expect(state.availability?.totalSlots, 4);
      expect(state.availability?.emptySlots, 2);
      expect(state.detail?.slots.length, 4);
    });

    test('selectStation clears old detail if new station detail returns 403/error', () async {
      final container = createContainer(
        overrides: [
          landingStationApiServiceProvider
              .overrideWithValue(buildDefaultFakeService()),
          userStorageProvider.overrideWithValue(userStorage),
        ],
      );
      final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});
      addTearDown(sub.close);

      // Ban đầu ở trạm A có detail
      await container.read(stationAvailabilityNotifierProvider.future);
      expect(
        container.read(stationAvailabilityNotifierProvider).value?.detail,
        isNotNull,
      );

      final notifier =
          container.read(stationAvailabilityNotifierProvider.notifier);

      // Chuyển sang trạm B (detail trả về 403)
      await notifier.selectStation(stationId2);

      final currentState =
          container.read(stationAvailabilityNotifierProvider).value;
      expect(currentState?.selectedStationId, stationId2);
      expect(currentState?.availability?.totalSlots, 6);
      // detail của trạm A phải được dọn sạch, không bị giữ lại
      expect(currentState?.detail, isNull);
      expect(currentState?.filteredSlots.length, 0);
    });

    test('setSlotFilter filters slots properly', () async {
      final container = createContainer(
        overrides: [
          landingStationApiServiceProvider
              .overrideWithValue(buildDefaultFakeService()),
          userStorageProvider.overrideWithValue(userStorage),
        ],
      );
      final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});
      addTearDown(sub.close);

      await container.read(stationAvailabilityNotifierProvider.future);
      final notifier =
          container.read(stationAvailabilityNotifierProvider.notifier);

      expect(
        container
            .read(stationAvailabilityNotifierProvider)
            .value
            ?.filteredSlots
            .length,
        4,
      );

      notifier.setSlotFilter('EMPTY');
      expect(
        container
            .read(stationAvailabilityNotifierProvider)
            .value
            ?.filteredSlots
            .length,
        2,
      );

      notifier.setSlotFilter('OCCUPIED');
      expect(
        container
            .read(stationAvailabilityNotifierProvider)
            .value
            ?.filteredSlots
            .length,
        1,
      );

      notifier.setSlotFilter('ERROR');
      expect(
        container
            .read(stationAvailabilityNotifierProvider)
            .value
            ?.filteredSlots
            .length,
        0,
      );
    });

    test('operator without assigned station shows isUnassignedOperator state', () async {
      await userStorage.write(
        User(
          id: 'unassigned-operator-id',
          email: 'unassigned@example.com',
          fullName: 'Unassigned Operator',
          phoneNumber: '0909999999',
          status: 'ACTIVE',
          roleCode: 'STATION_OPERATOR',
          createdAt: DateTime(2026, 1, 1),
        ),
      );

      final container = createContainer(
        overrides: [
          landingStationApiServiceProvider
              .overrideWithValue(buildDefaultFakeService()),
          userStorageProvider.overrideWithValue(userStorage),
        ],
      );
      final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});
      addTearDown(sub.close);

      final state = await container.read(
        stationAvailabilityNotifierProvider.future,
      );

      expect(state.isUnassignedOperator, true);
      expect(state.selectedStationId, isNull);
      expect(state.stations.isEmpty, true);
      expect(state.availability, isNull);
    });

    testWidgets(
      'periodic polling updates availability and slots without manual refresh',
      (tester) async {
        var currentEmptySlots = 2;
        final service = _FakeStationService(
          onGetStations: () async => defaultStations,
          onGetAvailability: (_) async => StationAvailability.fromJson({
            ...fakeAvailabilityJson,
            'emptySlots': currentEmptySlots,
            'occupiedSlots': 4 - currentEmptySlots,
          }),
          onGetDetail: (_) async => StationDetail.fromJson({
            ...fakeDetailJson,
            'emptySlots': currentEmptySlots,
            'slots': [
              {
                'id': 'slot-1',
                'stationId': stationId,
                'slotNumber': 1,
                'status': currentEmptySlots >= 2 ? 'EMPTY' : 'OCCUPIED',
                'lastUpdated': '2026-11-06T03:00:00.000Z',
                'currentOrderId': null,
                'currentMissionId': null,
              },
              {
                'id': 'slot-2',
                'stationId': stationId,
                'slotNumber': 2,
                'status': currentEmptySlots >= 1 ? 'EMPTY' : 'OCCUPIED',
                'lastUpdated': '2026-11-06T03:00:00.000Z',
                'currentOrderId': null,
                'currentMissionId': null,
              },
              {
                'id': 'slot-3',
                'stationId': stationId,
                'slotNumber': 3,
                'status': 'OCCUPIED',
                'lastUpdated': '2026-11-06T03:10:00.000Z',
                'currentOrderId': 'order-123',
                'currentMissionId': 'mission-456',
              },
              {
                'id': 'slot-4',
                'stationId': stationId,
                'slotNumber': 4,
                'status': 'RESERVED',
                'lastUpdated': '2026-11-06T03:12:00.000Z',
                'currentOrderId': 'order-789',
                'currentMissionId': 'mission-101',
              },
            ],
          }),
        );

        final container = ProviderContainer(
          overrides: [
            landingStationApiServiceProvider.overrideWithValue(service),
            userStorageProvider.overrideWithValue(userStorage),
          ],
          retry: (_, _) => null,
        );
        final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});

        final initialState = await container.read(
          stationAvailabilityNotifierProvider.future,
        );
        expect(initialState.availability?.emptySlots, 2);
        expect(initialState.detail?.slots[0].status, 'EMPTY');

        // Simulate slot state change on backend: slot 1 becomes OCCUPIED
        currentEmptySlots = 1;

        // Elapse 15 seconds (polling interval)
        await tester.pump(const Duration(seconds: 15));
        await tester.pump();

        final polledState =
            container.read(stationAvailabilityNotifierProvider).value;
        expect(polledState?.availability?.emptySlots, 1);
        expect(polledState?.detail?.slots[0].status, 'OCCUPIED');

        sub.close();
        container.dispose();
      },
    );

    testWidgets(
      'polling stops after container / notifier is disposed',
      (tester) async {
        var pollCount = 0;
        final service = _FakeStationService(
          onGetStations: () async => defaultStations,
          onGetAvailability: (_) async {
            pollCount++;
            return defaultAvail1;
          },
          onGetDetail: (_) async => defaultDetail1,
        );

        final container = ProviderContainer(
          overrides: [
            landingStationApiServiceProvider.overrideWithValue(service),
            userStorageProvider.overrideWithValue(userStorage),
          ],
          retry: (_, _) => null,
        );
        final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});

        await container.read(stationAvailabilityNotifierProvider.future);
        expect(pollCount, 1); // 1 initial call

        // Elapse 15s -> triggers 1 poll
        await tester.pump(const Duration(seconds: 15));
        await tester.pump();
        expect(pollCount, 2);

        // Dispose subscription and container
        sub.close();
        container.dispose();

        // Elapse another 30s -> no further poll should happen
        await tester.pump(const Duration(seconds: 30));
        await tester.pump();
        expect(pollCount, 2);
      },
    );

    testWidgets(
      'stale poll in flight does not overwrite newer station selection',
      (tester) async {
        final station1PollCompleter = Completer<StationAvailability>();
        var isInitialLoad = true;

        final delayedService = _FakeStationService(
          onGetStations: () async => defaultStations,
          onGetAvailability: (id) {
            if (id == stationId) {
              if (isInitialLoad) {
                isInitialLoad = false;
                return Future.value(defaultAvail1);
              }
              return station1PollCompleter.future;
            }
            return Future.value(defaultAvail2);
          },
          onGetDetail: (id) async {
            if (id == stationId) return defaultDetail1;
            throw Exception('403 Forbidden');
          },
        );

        final container = ProviderContainer(
          overrides: [
            landingStationApiServiceProvider.overrideWithValue(delayedService),
            userStorageProvider.overrideWithValue(userStorage),
          ],
          retry: (_, _) => null,
        );
        final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});

        await container.read(stationAvailabilityNotifierProvider.future);
        expect(
          container
              .read(stationAvailabilityNotifierProvider)
              .value
              ?.selectedStationId,
          stationId,
        );

        // Trigger background poll at 15s (which hangs waiting on station1PollCompleter)
        await tester.pump(const Duration(seconds: 15));

        // While the poll for stationId is in flight, user switches to stationId2
        final notifier =
            container.read(stationAvailabilityNotifierProvider.notifier);
        await notifier.selectStation(stationId2);

        expect(
          container
              .read(stationAvailabilityNotifierProvider)
              .value
              ?.selectedStationId,
          stationId2,
        );

        // Now the stale poll for stationId finally completes
        station1PollCompleter.complete(defaultAvail1);
        await tester.pump();

        // Selected station and data must remain stationId2, NOT overwritten by stationId
        final finalState =
            container.read(stationAvailabilityNotifierProvider).value;
        expect(finalState?.selectedStationId, stationId2);
        expect(finalState?.availability?.totalSlots, 6);
        expect(finalState?.detail, isNull);

        sub.close();
        container.dispose();
      },
    );

    testWidgets(
      'silent polling failure does not clear or disrupt current display data',
      (tester) async {
        var failPolling = false;
        final service = _FakeStationService(
          onGetStations: () async => defaultStations,
          onGetAvailability: (_) async {
            if (failPolling) throw Exception('Network Error');
            return defaultAvail1;
          },
          onGetDetail: (_) async {
            if (failPolling) throw Exception('Network Error');
            return defaultDetail1;
          },
        );

        final container = ProviderContainer(
          overrides: [
            landingStationApiServiceProvider.overrideWithValue(service),
            userStorageProvider.overrideWithValue(userStorage),
          ],
          retry: (_, _) => null,
        );
        final sub = container.listen(stationAvailabilityNotifierProvider, (_, _) {});

        final stateBefore = await container.read(
          stationAvailabilityNotifierProvider.future,
        );
        expect(stateBefore.availability?.emptySlots, 2);
        expect(stateBefore.detail?.slots.length, 4);

        // Network error during background polling
        failPolling = true;
        await tester.pump(const Duration(seconds: 15));
        await tester.pump();

        final stateAfterFail =
            container.read(stationAvailabilityNotifierProvider).value;
        // Data must be preserved, not overwritten with null or error
        expect(stateAfterFail?.availability?.emptySlots, 2);
        expect(stateAfterFail?.detail?.slots.length, 4);
        expect(
          container.read(stationAvailabilityNotifierProvider).hasError,
          false,
        );

        sub.close();
        container.dispose();
      },
    );
  });

  group('OperatorHomePage Widget', () {
    late InMemoryUserStorage userStorage;

    setUp(() {
      userStorage = InMemoryUserStorage();
    });

    testWidgets('renders station availability and slot breakdown', (tester) async {
      tester.view.physicalSize = const Size(800, 1800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await userStorage.write(
        User(
          id: operatorUserId,
          email: 'operator@example.com',
          fullName: 'Operator One',
          phoneNumber: '0901234567',
          status: 'ACTIVE',
          roleCode: 'STATION_OPERATOR',
          createdAt: DateTime(2026, 1, 1),
        ),
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            landingStationApiServiceProvider
                .overrideWithValue(buildDefaultFakeService()),
            userStorageProvider.overrideWithValue(userStorage),
          ],
          child: const MaterialApp(home: OperatorHomePage()),
        ),
      );

      // Loading state
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      // Pump until loaded
      await tester.pumpAndSettle();

      // Station info header
      expect(find.text('District 1 Hub'), findsOneWidget);
      expect(find.text('HCM-01'), findsOneWidget);
      expect(find.text('OPERATIONAL'), findsWidgets);
      expect(find.text('2 hosted'), findsOneWidget);

      // Live availability metrics
      expect(find.text('Live Availability'), findsOneWidget);
      expect(find.text('2 / 4 slots empty'), findsOneWidget);
      expect(find.text('Available capacity'), findsOneWidget);
      expect(find.text('Arrival readiness'), findsOneWidget);
      expect(find.text('Departure readiness'), findsOneWidget);

      // Status counter boxes
      expect(find.text('EMPTY'), findsWidgets);
      expect(find.text('OCCUPIED'), findsWidgets);
      expect(find.text('RESERVED'), findsWidgets);
      expect(find.text('ERROR'), findsWidgets);

      // Slots section
      expect(find.text('Landing & locker slots'), findsOneWidget);
      expect(find.text('Slot 1'), findsOneWidget);
      expect(find.text('Slot 2'), findsOneWidget);
      expect(find.text('Slot 3'), findsOneWidget);
      expect(find.text('Slot 4'), findsOneWidget);

      // Tapping a slot opens modal bottom sheet with slot details
      await tester.ensureVisible(find.text('Slot 3'));
      await tester.tap(find.text('Slot 3'));
      await tester.pumpAndSettle();

      expect(find.text('Slot #3'), findsOneWidget);
      expect(find.text('Current Order ID'), findsOneWidget);
      expect(find.text('order-123'), findsOneWidget);
      expect(find.text('Flight Mission ID'), findsOneWidget);
      expect(find.text('mission-456'), findsOneWidget);

      // Close modal
      await tester.tap(find.text('Close'));
      await tester.pumpAndSettle();
      expect(find.text('Slot #3'), findsNothing);
    });

    testWidgets('renders No station assigned screen when operator has no station', (tester) async {
      await userStorage.write(
        User(
          id: 'unassigned-op',
          email: 'unassigned@example.com',
          fullName: 'Unassigned Op',
          phoneNumber: '0909999999',
          status: 'ACTIVE',
          roleCode: 'STATION_OPERATOR',
          createdAt: DateTime(2026, 1, 1),
        ),
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            landingStationApiServiceProvider
                .overrideWithValue(buildDefaultFakeService()),
            userStorageProvider.overrideWithValue(userStorage),
          ],
          child: const MaterialApp(home: OperatorHomePage()),
        ),
      );

      await tester.pumpAndSettle();

      expect(find.text('No station assigned'), findsOneWidget);
      expect(find.text('Check again'), findsOneWidget);
    });
  });
}
