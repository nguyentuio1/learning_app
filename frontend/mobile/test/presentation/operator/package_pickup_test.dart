import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/constants/api_endpoints.dart';
import 'package:smart_drone_delivery/data/models/station_operation_models.dart';
import 'package:smart_drone_delivery/data/services/station_operations_api_service.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/package_pickup_page.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/pickup_controller.dart';

import '../../support/fake_http_adapter.dart';

void main() {
  const stationId = '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44';
  const trackingNumber = 'DD-202611-0001';

  final fakeOrderInfo = {
    'orderId': 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33',
    'trackingNumber': trackingNumber,
    'status': 'ARRIVED',
    'role': 'DESTINATION',
    'packageName': 'Electronic gadgets',
    'declaredWeightGram': 850,
    'declaredLengthCm': 20,
    'declaredWidthCm': 15,
    'declaredHeightCm': 10,
    'isFragile': true,
    'originStationId': 'a5c9e2b7-3d18-4e60-b4f2-8d1a7c0e9b55',
    'destinationStationId': stationId,
  };

  final fakePickupResponse = {
    'orderId': 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33',
    'trackingNumber': trackingNumber,
    'status': 'DELIVERED',
    'actualDeliveryTime': '2026-11-07T08:00:00.000Z',
    'slotId': 'slot-101',
    'slotReleased': true,
  };

  group('Station Operations Models', () {
    test('StationOrderInfo deserializes correctly', () {
      final info = StationOrderInfo.fromJson(fakeOrderInfo);
      expect(info.orderId, 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33');
      expect(info.trackingNumber, trackingNumber);
      expect(info.status, 'ARRIVED');
      expect(info.isDestination, true);
      expect(info.isOrigin, false);
      expect(info.declaredWeightGram, 850);
    });

    test('ConfirmPickupResponse deserializes correctly', () {
      final res = ConfirmPickupResponse.fromJson(fakePickupResponse);
      expect(res.trackingNumber, trackingNumber);
      expect(res.status, 'DELIVERED');
      expect(res.slotReleased, true);
    });
  });

  group('StationOperationsApiService', () {
    late Dio dio;
    late StationOperationsApiService service;

    setUp(() {
      dio = Dio(BaseOptions(baseUrl: 'http://test/api'));
      dio.httpClientAdapter = FakeHttpAdapter((options) {
        if (options.path ==
            ApiEndpoints.stationOrderLookup(stationId, trackingNumber)) {
          return jsonBody(fakeOrderInfo);
        }
        if (options.path == ApiEndpoints.stationPickup(stationId)) {
          return jsonBody(fakePickupResponse);
        }
        return jsonBody({'message': 'Not Found'}, 404);
      });
      service = StationOperationsApiService(dio);
    });

    test('lookupOrder returns order information', () async {
      final info = await service.lookupOrder(
        stationId: stationId,
        trackingNumber: trackingNumber,
      );
      expect(info.trackingNumber, trackingNumber);
      expect(info.packageName, 'Electronic gadgets');
    });

    test('confirmPickup sends request and parses response', () async {
      final res = await service.confirmPickup(
        stationId: stationId,
        request: const ConfirmPickupRequest(
          trackingNumber: trackingNumber,
          pickupSecureCode: '123456',
        ),
      );
      expect(res.status, 'DELIVERED');
      expect(res.slotReleased, true);
    });
  });

  group('PickupController', () {
    late Dio dio;
    late StationOperationsApiService service;

    setUp(() {
      dio = Dio(BaseOptions(baseUrl: 'http://test/api'));
      dio.httpClientAdapter = FakeHttpAdapter((options) {
        if (options.path ==
            ApiEndpoints.stationOrderLookup(stationId, trackingNumber)) {
          return jsonBody(fakeOrderInfo);
        }
        if (options.path == ApiEndpoints.stationPickup(stationId)) {
          return jsonBody(fakePickupResponse);
        }
        return jsonBody({'message': 'Not Found'}, 404);
      });
      service = StationOperationsApiService(dio);
    });

    test('lookupOrder updates state with order info', () async {
      final controller = PickupController(service);
      await controller.lookupOrder(
        stationId: stationId,
        trackingNumber: trackingNumber,
      );

      expect(controller.state.orderInfo?.trackingNumber, trackingNumber);
      expect(controller.state.errorMessage, isNull);
    });

    test('confirmPickup with invalid secure code sets error', () async {
      final controller = PickupController(service);
      final success = await controller.confirmPickup(
        stationId: stationId,
        trackingNumber: trackingNumber,
        secureCode: '12', // Less than 6 digits
      );

      expect(success, false);
      expect(controller.state.errorMessage,
          'Secure code must be exactly 6 digits.');
    });

    test('confirmPickup succeeds with 6-digit code', () async {
      final controller = PickupController(service);
      final success = await controller.confirmPickup(
        stationId: stationId,
        trackingNumber: trackingNumber,
        secureCode: '123456',
      );

      expect(success, true);
      expect(controller.state.completedPickup?.status, 'DELIVERED');
    });
  });

  group('PackagePickupPage Widget', () {
    late Dio dio;
    late StationOperationsApiService service;

    setUp(() {
      dio = Dio(BaseOptions(baseUrl: 'http://test/api'));
      dio.httpClientAdapter = FakeHttpAdapter((options) {
        if (options.path ==
            ApiEndpoints.stationOrderLookup(stationId, trackingNumber)) {
          return jsonBody(fakeOrderInfo);
        }
        if (options.path == ApiEndpoints.stationPickup(stationId)) {
          return jsonBody(fakePickupResponse);
        }
        return jsonBody({'message': 'Not Found'}, 404);
      });
      service = StationOperationsApiService(dio);
    });

    testWidgets('renders input fields, performs lookup and verifies pickup',
        (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            stationOperationsApiServiceProvider.overrideWithValue(service),
          ],
          child: const MaterialApp(
            home: PackagePickupPage(),
          ),
        ),
      );

      // Verify initial UI
      expect(find.text('Package pickup confirmation'), findsOneWidget);
      expect(find.text('1. Order Lookup'), findsOneWidget);
      expect(find.byType(TextField), findsOneWidget);

      // Enter tracking number and tap lookup
      await tester.enterText(find.byType(TextField).first, trackingNumber);
      await tester.tap(find.text('Look up parcel'));
      await tester.pumpAndSettle();

      // Verify order summary appears
      expect(find.text('2. Verify Secure Code'), findsOneWidget);
      expect(find.text('Electronic gadgets'), findsOneWidget);
      expect(find.text('ARRIVED'), findsOneWidget);

      // Enter 6-digit secure code and confirm
      await tester.enterText(find.byType(TextField).last, '123456');
      await tester.tap(find.text('Confirm Pickup & Release Slot'));
      await tester.pumpAndSettle();

      // Verify success card
      expect(find.text('Handover Completed'), findsOneWidget);
      expect(find.textContaining('is now marked DELIVERED'), findsOneWidget);
    });
  });
}
