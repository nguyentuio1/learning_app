import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/expired_parcel.dart';
import 'package:smart_drone_delivery/data/providers/expired_parcel_providers.dart';
import 'package:smart_drone_delivery/data/services/expired_parcel_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/expired_parcels/expired_parcel_screen.dart';
import 'package:smart_drone_delivery/presentation/operator/expired_parcels/pickup_timeout_alerts_card.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _stationId = '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44';
const _slotId = 'b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10';

final _parcel = ExpiredParcel(
  slotId: _slotId,
  slotNumber: 12,
  trackingNumber: 'DD-202611-0001',
  orderStatus: 'FAILED',
  slotStatus: 'OCCUPIED',
  cancelReason: ExpiredParcel.pickupTimeoutReason,
  arrivedAt: DateTime.utc(2026, 11, 14, 3),
  elapsedDuration: const Duration(days: 1, hours: 2, minutes: 15),
);

class _FakeExpiredParcelService extends ExpiredParcelApiService {
  _FakeExpiredParcelService(this.onClear) : super(Dio());

  final Future<ClearExpiredParcelResponse> Function(
    String stationId,
    ClearExpiredParcelRequest request,
  )
  onClear;

  @override
  Future<ClearExpiredParcelResponse> clearExpiredParcel({
    required String stationId,
    required ClearExpiredParcelRequest request,
  }) => onClear(stationId, request);
}

void main() {
  testWidgets('renders parcel details, status, and slot duration', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      ProviderScope(
        key: UniqueKey(),
        overrides: [
          expiredParcelsProvider.overrideWith(
            (ref, stationId) async => [_parcel],
          ),
        ],
        child: const MaterialApp(
          home: ExpiredParcelScreen(stationId: _stationId),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('DD-202611-0001'), findsOneWidget);
    expect(find.textContaining('Slot 12 · $_slotId'), findsOneWidget);
    expect(find.text('FAILED'), findsOneWidget);
    expect(find.text(ExpiredParcel.pickupTimeoutReason), findsOneWidget);
    expect(find.textContaining('Time in slot: 1d 2h 15m'), findsOneWidget);
    expect(find.textContaining('Arrived:'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('shows loading, empty, and error states', (tester) async {
    final response = Completer<List<ExpiredParcel>>();
    await tester.pumpWidget(
      ProviderScope(
        key: UniqueKey(),
        overrides: [
          expiredParcelsProvider.overrideWith(
            (ref, stationId) => response.future,
          ),
        ],
        child: const MaterialApp(
          home: ExpiredParcelScreen(stationId: _stationId),
        ),
      ),
    );
    await tester.pump();
    expect(find.byKey(const Key('expired-parcels-loading')), findsOneWidget);

    response.complete([]);
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('expired-parcels-empty')), findsOneWidget);

    await tester.pumpWidget(
      ProviderScope(
        key: UniqueKey(),
        overrides: [
          expiredParcelsProvider.overrideWith(
            (ref, stationId) => Future.error(
              const ApiException(message: 'Unable to load parcels.'),
            ),
          ),
        ],
        child: const MaterialApp(
          home: ExpiredParcelScreen(stationId: _stationId),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Unable to load parcels.'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });

  testWidgets('validates reason length and shows pending and success states', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final requestCompleter = Completer<ClearExpiredParcelResponse>();
    String? submittedReason;
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          expiredParcelsProvider.overrideWith(
            (ref, stationId) async => [_parcel],
          ),
          expiredParcelApiServiceProvider.overrideWithValue(
            _FakeExpiredParcelService((stationId, request) {
              submittedReason = request.reason;
              return requestCompleter.future;
            }),
          ),
        ],
        child: const MaterialApp(
          home: ExpiredParcelScreen(stationId: _stationId),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(Key('clear-expired-parcel-$_slotId')));
    await tester.pumpAndSettle();

    final reasonField = find.byKey(const Key('clear-expired-parcel-reason'));
    await tester.enterText(reasonField, '1234');
    await tester.pump();
    expect(find.text('Reason must be at least 5 characters.'), findsOneWidget);

    await tester.enterText(reasonField, List.filled(501, 'x').join());
    await tester.pump();
    expect(
      find.text('Reason must be no more than 500 characters.'),
      findsOneWidget,
    );

    await tester.enterText(reasonField, '12345');
    await tester.pump();
    expect(find.text('Reason must be at least 5 characters.'), findsNothing);
    await tester.tap(find.byKey(const Key('clear-expired-parcel-submit')));
    await tester.pump();

    expect(submittedReason, '12345');
    final submitButton = tester.widget<FilledButton>(
      find.byKey(const Key('clear-expired-parcel-submit')),
    );
    expect(submitButton.onPressed, isNull);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    requestCompleter.complete(
      ClearExpiredParcelResponse(
        orderId: 'c3b9a792-6e8d-4c12-8d37-2848f8464be2',
        trackingNumber: _parcel.trackingNumber,
        slotId: _slotId,
        slotStatus: 'EMPTY',
        clearedAt: DateTime.utc(2026, 11, 15, 4, 30),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Parcel cleared. Slot 12 is now EMPTY.'), findsOneWidget);
  });

  for (final (statusCode, message) in [
    (403, 'You are not authorized to clear parcels at this station.'),
    (
      409,
      'The parcel is no longer eligible to be cleared because its order or slot state changed.',
    ),
  ]) {
    testWidgets('shows a clear explanation for HTTP $statusCode', (
      tester,
    ) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            expiredParcelsProvider.overrideWith(
              (ref, stationId) async => [_parcel],
            ),
            expiredParcelApiServiceProvider.overrideWithValue(
              _FakeExpiredParcelService(
                (_, _) => Future.error(
                  ApiException(message: message, statusCode: statusCode),
                ),
              ),
            ),
          ],
          child: const MaterialApp(
            home: ExpiredParcelScreen(stationId: _stationId),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(Key('clear-expired-parcel-$_slotId')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('clear-expired-parcel-reason')),
        'Parcel removed',
      );
      await tester.tap(find.byKey(const Key('clear-expired-parcel-submit')));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('clear-expired-parcel-error')),
        findsOneWidget,
      );
      expect(find.text(message), findsOneWidget);
    });
  }

  testWidgets('shows pickup timeout alerts and routes directly to O10', (
    tester,
  ) async {
    final router = GoRouter(
      initialLocation: '/alerts',
      routes: [
        GoRoute(
          path: '/alerts',
          builder: (_, _) => const Scaffold(
            body: PickupTimeoutAlertsCard(stationId: _stationId),
          ),
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
        overrides: [
          pickupTimeoutAlertsProvider.overrideWith(
            (ref, stationId) async => [
              PickupTimeoutAlert.fromExpiredParcel(_parcel),
            ],
          ),
        ],
        child: MaterialApp.router(routerConfig: router),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Pickup timeout alerts'), findsOneWidget);
    expect(find.text('Pickup window expired'), findsOneWidget);
    expect(find.textContaining('DD-202611-0001'), findsOneWidget);
    await tester.tap(find.text('Pickup window expired'));
    await tester.pumpAndSettle();
    expect(find.text('O10 $_stationId'), findsOneWidget);
  });
}
