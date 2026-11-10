import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/station_operation_models.dart';
import 'package:smart_drone_delivery/data/services/station_operations_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/home/station_availability_providers.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/package_pickup_page.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/pickup_controller.dart';

import '../../support/fake_http_adapter.dart';

class _FakeStationOperationsApiService extends StationOperationsApiService {
  _FakeStationOperationsApiService({
    required this.orderInfo,
    required this.pickupResponse,
  }) : super(Dio());

  StationOrderInfo orderInfo;
  ConfirmPickupResponse pickupResponse;
  ApiException? lookupError;
  ApiException? confirmError;
  String? lastStationId;
  String? lastLookupTracking;
  ConfirmPickupRequest? lastConfirmRequest;

  @override
  Future<StationOrderInfo> lookupOrder({
    required String stationId,
    required String trackingNumber,
  }) async {
    lastStationId = stationId;
    lastLookupTracking = trackingNumber;
    if (lookupError != null) {
      throw lookupError!;
    }
    return orderInfo;
  }

  @override
  Future<ConfirmPickupResponse> confirmPickup({
    required String stationId,
    required ConfirmPickupRequest request,
  }) async {
    lastStationId = stationId;
    lastConfirmRequest = request;
    if (confirmError != null) {
      throw confirmError!;
    }
    return pickupResponse;
  }
}

class _StubStationAvailabilityNotifier extends StationAvailabilityNotifier {
  _StubStationAvailabilityNotifier(this._initialState);

  final StationAvailabilityState _initialState;

  @override
  Future<StationAvailabilityState> build() async => _initialState;
}

void main() {
  const assignedStationId = 'station-assigned-001';
  const trackingNumber = 'DD-202611-0001';

  final decimalOrderJson = <String, dynamic>{
    'orderId': 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33',
    'trackingNumber': trackingNumber,
    'status': 'ARRIVED',
    'role': 'DESTINATION',
    'packageName': 'Electronic gadgets',
    'declaredWeightGram': 500.00,
    'declaredLengthCm': 20.50,
    'declaredWidthCm': 15.25,
    'declaredHeightCm': 10.00,
    'isFragile': true,
    'originStationId': 'a5c9e2b7-3d18-4e60-b4f2-8d1a7c0e9b55',
    'destinationStationId': assignedStationId,
  };

  final fakePickupResponse = <String, dynamic>{
    'orderId': 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33',
    'trackingNumber': trackingNumber,
    'status': 'DELIVERED',
    'actualDeliveryTime': '2026-11-07T08:00:00.000Z',
    'slotId': 'slot-101',
    'slotReleased': true,
  };

  const assignedStation = LandingStation(
    id: assignedStationId,
    name: 'District 1 Hub',
    stationCode: 'ST-D1-01',
    address: '123 Le Loi',
    latitude: 10.77,
    longitude: 106.70,
    status: 'OPERATIONAL',
    operatorId: 'op-1',
    totalSlots: 8,
    availableSlots: 4,
  );

  group('Station Operations Models', () {
    test('StationOrderInfo deserializes backend decimal JSON (500.00) correctly',
        () {
      final info = StationOrderInfo.fromJson(decimalOrderJson);
      expect(info.orderId, 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33');
      expect(info.trackingNumber, trackingNumber);
      expect(info.status, 'ARRIVED');
      expect(info.isDestination, true);
      expect(info.isOrigin, false);
      expect(info.isEligibleForPickup, true);
      expect(info.declaredWeightGram, 500.0);
      expect(info.declaredLengthCm, 20.5);
      expect(info.declaredWidthCm, 15.25);
      expect(info.declaredHeightCm, 10.0);
    });

    test('missing role defaults to UNKNOWN and is not eligible for pickup', () {
      final info = StationOrderInfo.fromJson({
        'orderId': 'ord-1',
        'trackingNumber': trackingNumber,
        'status': 'ARRIVED',
      });
      expect(info.role, 'UNKNOWN');
      expect(info.isOrigin, false);
      expect(info.isDestination, false);
      expect(info.isEligibleForPickup, false);
    });

    test('ConfirmPickupResponse deserializes correctly', () {
      final res = ConfirmPickupResponse.fromJson(fakePickupResponse);
      expect(res.trackingNumber, trackingNumber);
      expect(res.status, 'DELIVERED');
      expect(res.slotReleased, true);
    });
  });

  group('StationOperationsApiService', () {
    test('lookupOrder parses decimal JSON and confirmPickup parses 200 OK',
        () async {
      final dio = Dio(BaseOptions(baseUrl: 'http://test/api'));
      dio.httpClientAdapter = FakeHttpAdapter((options) {
        if (options.path ==
            ApiEndpoints.stationOrderLookup(assignedStationId, trackingNumber)) {
          return ResponseBody.fromString(
            '{"orderId":"e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",'
            '"trackingNumber":"$trackingNumber",'
            '"status":"ARRIVED",'
            '"role":"DESTINATION",'
            '"packageName":"Electronic gadgets",'
            '"declaredWeightGram":500.00,'
            '"declaredLengthCm":20.50,'
            '"declaredWidthCm":15.25,'
            '"declaredHeightCm":10.00,'
            '"isFragile":true,'
            '"originStationId":"a5c9e2b7-3d18-4e60-b4f2-8d1a7c0e9b55",'
            '"destinationStationId":"$assignedStationId"}',
            200,
            headers: {
              Headers.contentTypeHeader: [Headers.jsonContentType],
            },
          );
        }
        if (options.path == ApiEndpoints.stationPickup(assignedStationId)) {
          return jsonBody(fakePickupResponse);
        }
        return jsonBody({'message': 'Not Found'}, 404);
      });
      final service = StationOperationsApiService(dio);

      final info = await service.lookupOrder(
        stationId: assignedStationId,
        trackingNumber: trackingNumber,
      );
      expect(info.trackingNumber, trackingNumber);
      expect(info.declaredWeightGram, 500.0);
      expect(info.declaredLengthCm, 20.5);

      final res = await service.confirmPickup(
        stationId: assignedStationId,
        request: const ConfirmPickupRequest(
          trackingNumber: trackingNumber,
          pickupSecureCode: '123456',
        ),
      );
      expect(res.status, 'DELIVERED');
      expect(res.slotReleased, true);
    });

    test('confirmPickup surfaces attemptsRemaining on 400 and Retry-After on 423',
        () async {
      var return423 = false;
      final dio = Dio(BaseOptions(baseUrl: 'http://test/api'));
      dio.httpClientAdapter = FakeHttpAdapter((options) {
        if (!return423) {
          return jsonBody({
            'type': 'https://httpstatuses.com/400',
            'title': 'Bad Request',
            'status': 400,
            'detail': 'Invalid pickup secure code.',
            'attemptsRemaining': 3,
          }, 400);
        }
        return ResponseBody.fromString(
          '{"type":"https://httpstatuses.com/423",'
          '"title":"Locked",'
          '"status":423,'
          '"detail":"Too many failed attempts. Pickup is locked.",'
          '"lockoutExpiresAt":"2026-11-10T12:15:00.000Z"}',
          423,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType],
            'retry-after': ['900'],
          },
        );
      });
      final service = StationOperationsApiService(dio);

      await expectLater(
        () => service.confirmPickup(
          stationId: assignedStationId,
          request: const ConfirmPickupRequest(
            trackingNumber: trackingNumber,
            pickupSecureCode: '000000',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 400)
              .having((e) => e.attemptsRemaining, 'attemptsRemaining', 3),
        ),
      );

      return423 = true;
      await expectLater(
        () => service.confirmPickup(
          stationId: assignedStationId,
          request: const ConfirmPickupRequest(
            trackingNumber: trackingNumber,
            pickupSecureCode: '000000',
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 423)
              .having((e) => e.retryAfterSeconds, 'retryAfterSeconds', 900)
              .having(
                (e) => e.lockoutExpiresAt,
                'lockoutExpiresAt',
                DateTime.parse('2026-11-10T12:15:00.000Z'),
              ),
        ),
      );
    });
  });

  group('PickupController', () {
    late _FakeStationOperationsApiService fakeService;

    setUp(() {
      fakeService = _FakeStationOperationsApiService(
        orderInfo: StationOrderInfo.fromJson(decimalOrderJson),
        pickupResponse: ConfirmPickupResponse.fromJson(fakePickupResponse),
      );
    });

    test('lookupOrder normalizes trackingNumber and clears stale state',
        () async {
      final container = ProviderContainer(
        overrides: [
          stationOperationsApiServiceProvider.overrideWithValue(fakeService),
        ],
      );
      addTearDown(container.dispose);

      final sub = container.listen(pickupControllerProvider, (_, _) {});
      addTearDown(sub.close);

      // First lookup and complete pickup
      await container.read(pickupControllerProvider.notifier).lookupOrder(
            stationId: assignedStationId,
            trackingNumber: '  dd-202611-0001 ',
          );
      expect(fakeService.lastLookupTracking, trackingNumber);

      await container.read(pickupControllerProvider.notifier).confirmPickup(
            stationId: assignedStationId,
            trackingNumber: '  dd-202611-0001 ',
            secureCode: '123456',
          );
      expect(
        fakeService.lastConfirmRequest?.trackingNumber,
        trackingNumber,
      );
      expect(
        container.read(pickupControllerProvider).completedPickup,
        isNotNull,
      );

      // Now trigger a second lookup that fails -> stale orderInfo & completedPickup must be cleared
      fakeService.lookupError = const ApiException(
        message: 'Order not found.',
        statusCode: 404,
      );
      await container.read(pickupControllerProvider.notifier).lookupOrder(
            stationId: assignedStationId,
            trackingNumber: 'DD-202611-9999',
          );

      final state = container.read(pickupControllerProvider);
      expect(state.orderInfo, isNull);
      expect(state.completedPickup, isNull);
      expect(state.remainingAttempts, isNull);
      expect(state.isLocked, false);
      expect(state.errorMessage, 'Order not found.');
    });

    test('confirmPickup surfaces 400 attemptsRemaining and 423 lockout details',
        () async {
      final container = ProviderContainer(
        overrides: [
          stationOperationsApiServiceProvider.overrideWithValue(fakeService),
        ],
      );
      addTearDown(container.dispose);

      final sub = container.listen(pickupControllerProvider, (_, _) {});
      addTearDown(sub.close);

      await container.read(pickupControllerProvider.notifier).lookupOrder(
            stationId: assignedStationId,
            trackingNumber: trackingNumber,
          );

      // 400 with attemptsRemaining = 2
      fakeService.confirmError = const ApiException(
        message: 'Invalid pickup secure code.',
        statusCode: 400,
        attemptsRemaining: 2,
      );
      final res400 = await container
          .read(pickupControllerProvider.notifier)
          .confirmPickup(
            stationId: assignedStationId,
            trackingNumber: trackingNumber,
            secureCode: '111111',
          );
      expect(res400, false);
      var state = container.read(pickupControllerProvider);
      expect(state.remainingAttempts, 2);
      expect(state.isLocked, false);

      // 423 with Retry-After = 900 and lockoutExpiresAt
      final lockExpiry = DateTime.parse('2026-11-10T12:15:00.000Z');
      fakeService.confirmError = ApiException(
        message: 'Too many failed attempts. Pickup is locked.',
        statusCode: 423,
        retryAfterSeconds: 900,
        lockoutExpiresAt: lockExpiry,
      );
      final res423 = await container
          .read(pickupControllerProvider.notifier)
          .confirmPickup(
            stationId: assignedStationId,
            trackingNumber: trackingNumber,
            secureCode: '222222',
          );
      expect(res423, false);
      state = container.read(pickupControllerProvider);
      expect(state.isLocked, true);
      expect(state.remainingAttempts, 0);
      expect(state.lockoutSecondsRemaining, 900);
      expect(state.lockoutExpiresAt, lockExpiry);
    });

    test('confirmPickup rejects non-DESTINATION or non-ARRIVED orders',
        () async {
      fakeService.orderInfo = StationOrderInfo.fromJson({
        ...decimalOrderJson,
        'role': 'ORIGIN',
        'status': 'READY_FOR_TAKEOFF',
      });

      final container = ProviderContainer(
        overrides: [
          stationOperationsApiServiceProvider.overrideWithValue(fakeService),
        ],
      );
      addTearDown(container.dispose);

      final sub = container.listen(pickupControllerProvider, (_, _) {});
      addTearDown(sub.close);

      await container.read(pickupControllerProvider.notifier).lookupOrder(
            stationId: assignedStationId,
            trackingNumber: trackingNumber,
          );

      final success = await container
          .read(pickupControllerProvider.notifier)
          .confirmPickup(
            stationId: assignedStationId,
            trackingNumber: trackingNumber,
            secureCode: '123456',
          );
      expect(success, false);
      expect(
        container.read(pickupControllerProvider).errorMessage,
        contains('not at its destination station'),
      );
      expect(fakeService.lastConfirmRequest, isNull);
    });
  });

  group('PackagePickupPage Widget', () {
    late _FakeStationOperationsApiService fakeService;

    setUp(() {
      fakeService = _FakeStationOperationsApiService(
        orderInfo: StationOrderInfo.fromJson(decimalOrderJson),
        pickupResponse: ConfirmPickupResponse.fromJson(fakePickupResponse),
      );
    });

    testWidgets(
        'uses assigned station id, performs lookup and verifies pickup',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeService),
            stationAvailabilityNotifierProvider.overrideWith(
              () => _StubStationAvailabilityNotifier(
                const StationAvailabilityState(
                  stations: [assignedStation],
                  selectedStationId: assignedStationId,
                  availability: null,
                  detail: null,
                ),
              ),
            ),
          ],
          child: const MaterialApp(
            home: PackagePickupPage(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Verify initial UI
      expect(find.text('Package pickup confirmation'), findsOneWidget);
      expect(find.text('1. Order Lookup'), findsOneWidget);

      // Enter tracking number and tap lookup
      await tester.enterText(find.byType(TextField).first, trackingNumber);
      await tester.tap(find.text('Look up parcel'));
      await tester.pumpAndSettle();

      expect(fakeService.lastStationId, assignedStationId);
      expect(find.text('2. Verify Secure Code'), findsOneWidget);
      expect(find.text('Electronic gadgets'), findsOneWidget);
      expect(find.text('500 g'), findsOneWidget);
      expect(find.text('20.5 x 15.25 x 10 cm'), findsOneWidget);

      // Enter 6-digit secure code and confirm
      await tester.enterText(find.byType(TextField).last, '123456');
      await tester.tap(find.text('Confirm Pickup & Release Slot'));
      await tester.pumpAndSettle();

      expect(find.text('Handover Completed'), findsOneWidget);
      expect(find.textContaining('is now marked DELIVERED'), findsOneWidget);
    });

    testWidgets('shows No station assigned when operator has no assigned station',
        (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeService),
            stationAvailabilityNotifierProvider.overrideWith(
              () => _StubStationAvailabilityNotifier(
                const StationAvailabilityState(
                  stations: [],
                  selectedStationId: null,
                  availability: null,
                  detail: null,
                  isUnassignedOperator: true,
                ),
              ),
            ),
          ],
          child: const MaterialApp(
            home: PackagePickupPage(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('No station assigned'), findsOneWidget);
      expect(find.text('1. Order Lookup'), findsNothing);
    });

    testWidgets(
        'hides secure-code step and shows eligibility message for non-DESTINATION or non-ARRIVED order',
        (tester) async {
      fakeService.orderInfo = StationOrderInfo.fromJson({
        ...decimalOrderJson,
        'role': 'DESTINATION',
        'status': 'IN_TRANSIT',
      });

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeService),
            stationAvailabilityNotifierProvider.overrideWith(
              () => _StubStationAvailabilityNotifier(
                const StationAvailabilityState(
                  stations: [assignedStation],
                  selectedStationId: assignedStationId,
                  availability: null,
                  detail: null,
                ),
              ),
            ),
          ],
          child: const MaterialApp(
            home: PackagePickupPage(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField).first, trackingNumber);
      await tester.tap(find.text('Look up parcel'));
      await tester.pumpAndSettle();

      expect(find.text('Parcel not eligible for pickup'), findsOneWidget);
      expect(find.textContaining('currently IN_TRANSIT'), findsOneWidget);
      expect(find.text('2. Verify Secure Code'), findsNothing);
    });

    testWidgets(
        'surfaces remaining attempts on 400 and disables confirm button on 423 lockout',
        (tester) async {
      tester.view.physicalSize = const Size(800, 1400);
      tester.view.devicePixelRatio = 1;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(fakeService),
            stationAvailabilityNotifierProvider.overrideWith(
              () => _StubStationAvailabilityNotifier(
                const StationAvailabilityState(
                  stations: [assignedStation],
                  selectedStationId: assignedStationId,
                  availability: null,
                  detail: null,
                ),
              ),
            ),
          ],
          child: const MaterialApp(
            home: PackagePickupPage(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField).first, trackingNumber);
      await tester.tap(find.text('Look up parcel'));
      await tester.pumpAndSettle();

      // 400 Bad Request with 3 attempts remaining
      fakeService.confirmError = const ApiException(
        message: 'Invalid pickup secure code.',
        statusCode: 400,
        attemptsRemaining: 3,
      );
      await tester.enterText(find.byType(TextField).last, '000000');
      await tester.tap(find.text('Confirm Pickup & Release Slot'));
      await tester.pumpAndSettle();

      expect(find.text('Attempts remaining: 3'), findsOneWidget);

      // 423 Locked with Retry-After = 900
      fakeService.confirmError = ApiException(
        message: 'Too many failed attempts.',
        statusCode: 423,
        retryAfterSeconds: 900,
        lockoutExpiresAt: DateTime.parse('2026-11-10T12:15:00.000Z'),
      );
      await tester.tap(find.text('Confirm Pickup & Release Slot'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Retry after 900s.'), findsOneWidget);
      final confirmButton = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Confirm Pickup & Release Slot'),
      );
      expect(confirmButton.onPressed, isNull);
    });
  });
}
