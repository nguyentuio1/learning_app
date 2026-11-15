import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/core/storage/storage_providers.dart';
import 'package:smart_drone_delivery/data/models/expired_parcel.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/station_availability.dart';
import 'package:smart_drone_delivery/data/models/station_detail.dart';
import 'package:smart_drone_delivery/data/providers/expired_parcel_providers.dart';
import 'package:smart_drone_delivery/data/providers/landing_station_providers.dart';
import 'package:smart_drone_delivery/data/services/expired_parcel_api_service.dart';
import 'package:smart_drone_delivery/data/services/landing_station_api_service.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/presentation/operator/expired_parcels/pickup_timeout_alerts_card.dart';
import 'package:smart_drone_delivery/presentation/operator/home/operator_home_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

import '../../support/test_helpers.dart';

const _stationId = '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44';
const _operatorId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';

final _expiredParcel = ExpiredParcel(
  slotId: 'b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10',
  slotNumber: 12,
  trackingNumber: 'DD-202611-0001',
  orderStatus: 'FAILED',
  slotStatus: 'OCCUPIED',
  cancelReason: ExpiredParcel.pickupTimeoutReason,
  arrivedAt: DateTime.utc(2026, 11, 14, 3),
  elapsedDuration: const Duration(days: 1, hours: 2, minutes: 15),
);

const _station = LandingStation(
  id: _stationId,
  name: 'District 1 Hub',
  stationCode: 'HCM-01',
  address: '123 Le Loi',
  status: 'OPERATIONAL',
  operatorId: _operatorId,
  totalSlots: 2,
  availableSlots: 1,
);

final _availability = StationAvailability(
  stationId: _stationId,
  stationStatus: 'OPERATIONAL',
  totalSlots: 2,
  emptySlots: 1,
  occupiedSlots: 1,
  reservedSlots: 0,
  errorSlots: 0,
  isAvailableForArrival: true,
  isAvailableForDeparture: true,
  checkedAt: DateTime.utc(2026, 11, 15),
);

class _FakeStationApiService extends LandingStationApiService {
  _FakeStationApiService() : super(Dio());

  @override
  Future<List<LandingStation>> getStations({
    String? status,
    String? search,
  }) async => [_station];

  @override
  Future<StationAvailability> getStationAvailability(String stationId) async =>
      _availability;

  @override
  Future<StationDetail> getStationDetail(String stationId) async =>
      throw const ApiException(message: 'Station detail unavailable.');
}

class _FakeExpiredParcelApiService extends ExpiredParcelApiService {
  _FakeExpiredParcelApiService(this.onGetExpiredParcels) : super(Dio());

  final Future<List<ExpiredParcel>> Function(String stationId)
  onGetExpiredParcels;

  @override
  Future<List<ExpiredParcel>> getExpiredParcels(String stationId) =>
      onGetExpiredParcels(stationId);
}

Future<GoRouter> _pumpOperatorHome(
  WidgetTester tester, {
  required _FakeExpiredParcelApiService expiredParcelService,
}) async {
  final userStorage = InMemoryUserStorage();
  await userStorage.write(
    User(
      id: _operatorId,
      email: 'operator@example.com',
      fullName: 'Station Operator',
      phoneNumber: '0901234567',
      status: 'ACTIVE',
      roleCode: 'STATION_OPERATOR',
      createdAt: DateTime(2026, 1, 1),
    ),
  );
  final router = GoRouter(
    initialLocation: RoutePaths.operatorHome,
    routes: [
      GoRoute(
        path: RoutePaths.operatorHome,
        builder: (_, _) => const OperatorHomePage(),
      ),
      GoRoute(
        path: RoutePaths.operatorExpiredParcels,
        builder: (_, state) =>
            Scaffold(body: Text('O10 ${state.pathParameters['stationId']}')),
      ),
    ],
  );
  addTearDown(router.dispose);

  await tester.pumpWidget(
    ProviderScope(
      retry: (_, _) => null,
      overrides: [
        userStorageProvider.overrideWithValue(userStorage),
        landingStationApiServiceProvider.overrideWithValue(
          _FakeStationApiService(),
        ),
        expiredParcelApiServiceProvider.overrideWithValue(expiredParcelService),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  return router;
}

void main() {
  testWidgets('manual Home refresh updates alerts and navigates to O10', (
    tester,
  ) async {
    var getCount = 0;
    await _pumpOperatorHome(
      tester,
      expiredParcelService: _FakeExpiredParcelApiService((_) async {
        getCount++;
        return getCount == 1 ? [] : [_expiredParcel];
      }),
    );

    expect(find.text('No pickup timeout alerts.'), findsOneWidget);
    expect(getCount, 1);

    await tester.tap(find.byTooltip('Refresh'));
    await tester.pumpAndSettle();

    expect(getCount, 2);
    expect(find.text('No pickup timeout alerts.'), findsNothing);
    expect(find.text('Pickup window expired'), findsOneWidget);

    await tester.tap(find.text('Pickup window expired'));
    await tester.pumpAndSettle();
    expect(find.text('O10 $_stationId'), findsOneWidget);
  });

  testWidgets(
    '15-second station poll refreshes alerts without remounting Home',
    (tester) async {
      var getCount = 0;
      await _pumpOperatorHome(
        tester,
        expiredParcelService: _FakeExpiredParcelApiService((_) async {
          getCount++;
          return getCount == 1 ? [] : [_expiredParcel];
        }),
      );

      expect(find.text('No pickup timeout alerts.'), findsOneWidget);
      expect(getCount, 1);

      await tester.pump(const Duration(seconds: 15));
      await tester.pumpAndSettle();

      expect(getCount, 2);
      expect(find.text('No pickup timeout alerts.'), findsNothing);
      expect(find.text('Pickup window expired'), findsOneWidget);

      await tester.tap(find.text('Pickup window expired'));
      await tester.pumpAndSettle();
      expect(find.text('O10 $_stationId'), findsOneWidget);
    },
  );

  testWidgets('Retry fetches again through the expired-parcel provider chain', (
    tester,
  ) async {
    var getCount = 0;
    final service = _FakeExpiredParcelApiService((_) async {
      getCount++;
      if (getCount == 1) {
        throw const ApiException(message: 'Temporary API error.');
      }
      return [_expiredParcel];
    });

    await tester.pumpWidget(
      ProviderScope(
        retry: (_, _) => null,
        overrides: [expiredParcelApiServiceProvider.overrideWithValue(service)],
        child: const MaterialApp(
          home: Scaffold(body: PickupTimeoutAlertsCard(stationId: _stationId)),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(getCount, 1);
    expect(find.text('Temporary API error.'), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();

    expect(getCount, 2);
    expect(find.text('Temporary API error.'), findsNothing);
    expect(find.text('Pickup window expired'), findsOneWidget);
    expect(find.textContaining('DD-202611-0001'), findsOneWidget);
  });
}
