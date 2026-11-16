import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/eta_estimation_model.dart';
import 'package:smart_drone_delivery/data/models/landing_station.dart';
import 'package:smart_drone_delivery/data/models/package_model.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/domain/entities/delivery_map_snapshot.dart';
import 'package:smart_drone_delivery/domain/repositories/delivery_map_repository.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/create_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/eta_estimation_controller.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/review_order_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/widgets/eta_estimation_card.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _orderId = '7b1c2d3e-4f5a-6b7c-8d9e-0f1a2b3c4d5e';
const _originStationId = '11111111-1111-1111-1111-111111111111';
const _destinationStationId = '22222222-2222-2222-2222-222222222222';

DeliveryOrderDetailResponseDto _buildDetail({
  String status = 'IN_TRANSIT',
  double? distanceKm = 3.4,
  int? durationMinutes = 14,
  String? weather = 'Light breeze',
  double? confidence = 0.92,
  DateTime? predictionCreatedAt,
  DateTime? estimatedDeliveryTime,
  DateTime? actualDeliveryTime,
  bool includePrediction = true,
}) {
  return DeliveryOrderDetailResponseDto(
    id: _orderId,
    trackingNumber: 'DD-202611-0401',
    recipientName: 'Tran Quoc Bao',
    recipientPhone: '0912345678',
    originStationId: _originStationId,
    destinationStationId: _destinationStationId,
    status: status,
    statusLogs: const [],
    package: const OrderPackageDto(
      id: 'pkg-1',
      packageName: 'Lab sample kit',
      weightGram: 1200,
      isFragile: true,
    ),
    originStation: const OrderStationDto(
      id: _originStationId,
      stationCode: 'QTSC-01',
      name: 'QTSC Campus Hub',
      latitude: 10.8538,
      longitude: 106.6278,
    ),
    destinationStation: const OrderStationDto(
      id: _destinationStationId,
      stationCode: 'SHTP-02',
      name: 'SHTP Lab Station',
      latitude: 10.8411,
      longitude: 106.8099,
    ),
    estimatedDeliveryTime: estimatedDeliveryTime,
    actualDeliveryTime: actualDeliveryTime,
    aiEtaPrediction: includePrediction
        ? OrderAiEtaPredictionDto(
            calculatedDistanceKm: distanceKm,
            predictedDurationMinutes: durationMinutes,
            weatherCondition: weather,
            confidenceScore: confidence,
            createdAt: predictionCreatedAt ?? DateTime.utc(2026, 11, 16, 1, 0),
          )
        : null,
  );
}

class _FakeEtaOrderService extends DeliveryOrderApiService {
  _FakeEtaOrderService({DeliveryOrderDetailResponseDto? initialDetail})
    : detail = initialDetail ?? _buildDetail(),
      super(Dio());

  DeliveryOrderDetailResponseDto detail;
  ApiException? detailError;
  final List<ApiException> detailErrorQueue = [];
  int detailCallCount = 0;

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async {
    detailCallCount++;
    if (detailErrorQueue.isNotEmpty) {
      throw detailErrorQueue.removeAt(0);
    }
    if (detailError != null) {
      throw detailError!;
    }
    return detail;
  }
}

class _FakeDeliveryMapRepository implements DeliveryMapRepository {
  final _controller = StreamController<DeliveryMapUpdate>.broadcast();

  void emitStatusChanged(String orderId) {
    _controller.add(DeliveryMapUpdate.statusChanged(orderId));
  }

  Future<void> close() => _controller.close();

  @override
  Future<DeliveryMapSnapshot> getOrderMap(String orderId) async =>
      const DeliveryMapSnapshot(
        orderId: _orderId,
        trackingNumber: 'DD-202611-0401',
        status: DeliveryMapOrderStatus.inTransit,
        originStation: MapStationPoint(
          id: _originStationId,
          name: 'QTSC Campus Hub',
          latitude: 10.8538,
          longitude: 106.6278,
        ),
        destinationStation: MapStationPoint(
          id: _destinationStationId,
          name: 'SHTP Lab Station',
          latitude: 10.8411,
          longitude: 106.8099,
        ),
        estimatedDeliveryTime: null,
        lastPosition: null,
        telemetryStale: false,
      );

  @override
  Stream<DeliveryMapUpdate> watchOrderMap(String orderId) => _controller.stream;
}

void main() {
  group('OrderEtaEstimate and RouteEtaPreview models (LA-40 / LA-160)', () {
    test(
      'validates positive distance/duration, [0,1] confidence, and never invents weather or timestamp',
      () {
        final now = DateTime.utc(2026, 11, 16, 1, 10);
        final detailWithoutTimestampOrWeather = _buildDetail(
          status: 'APPROVED',
          distanceKm: 4.25,
          durationMinutes: 18,
          weather: '   ',
          confidence: 0.88,
          estimatedDeliveryTime: null,
        );

        final estimate = OrderEtaEstimate.fromOrderDetail(
          detailWithoutTimestampOrWeather,
          now: now,
        );

        expect(estimate.calculatedDistanceKm, 4.25);
        expect(estimate.predictedDurationMinutes, 18);
        expect(estimate.weatherCondition, isNull);
        expect(estimate.confidenceScore, 0.88);
        expect(estimate.confidencePercentage, 88);
        // Must not fabricate an arrival timestamp when server estimatedDeliveryTime is null
        expect(estimate.estimatedDeliveryTime, isNull);
        expect(estimate.remainingMinutes, isNull);
        expect(estimate.freshnessStatus, EtaFreshnessStatus.current);

        // Reject negative/zero durations and out-of-bounds confidence scores
        final invalidDetail = _buildDetail(
          status: 'IN_TRANSIT',
          distanceKm: -2.0,
          durationMinutes: 0,
          confidence: 1.5,
          estimatedDeliveryTime: null,
        );
        final invalidEstimate = OrderEtaEstimate.fromOrderDetail(
          invalidDetail,
          now: now,
        );
        expect(invalidEstimate.hasPrediction, false);
        expect(invalidEstimate.confidenceScore, isNull);
        expect(
          invalidEstimate.freshnessStatus,
          EtaFreshnessStatus.unavailable,
        );
      },
    );

    test(
      'distinguishes PENDING, active, stale, ARRIVED, DELIVERED, and CANCELLED/FAILED states',
      () {
        final now = DateTime.utc(2026, 11, 16, 1, 30);

        final pending = OrderEtaEstimate.fromOrderDetail(
          _buildDetail(status: 'PENDING', includePrediction: false),
          now: now,
        );
        expect(pending.freshnessStatus, EtaFreshnessStatus.waitingForApproval);
        expect(pending.isLiveEstimateActive, false);

        final pastEstimate = OrderEtaEstimate.fromOrderDetail(
          _buildDetail(
            status: 'IN_TRANSIT',
            estimatedDeliveryTime: now.subtract(const Duration(minutes: 5)),
          ),
          now: now,
        );
        expect(pastEstimate.freshnessStatus, EtaFreshnessStatus.stale);
        expect(pastEstimate.remainingMinutes, isNull);

        final arrived = OrderEtaEstimate.fromOrderDetail(
          _buildDetail(
            status: 'ARRIVED',
            estimatedDeliveryTime: now.add(const Duration(minutes: 10)),
          ),
          now: now,
        );
        expect(
          arrived.freshnessStatus,
          EtaFreshnessStatus.arrivedAtDestination,
        );
        expect(arrived.remainingMinutes, isNull);

        final delivered = OrderEtaEstimate.fromOrderDetail(
          _buildDetail(
            status: 'DELIVERED',
            estimatedDeliveryTime: now.add(const Duration(minutes: 10)),
            actualDeliveryTime: now,
          ),
          now: now,
        );
        expect(delivered.freshnessStatus, EtaFreshnessStatus.completed);
        expect(delivered.remainingMinutes, isNull);
        expect(delivered.actualDeliveryTime, now);

        final cancelled = OrderEtaEstimate.fromOrderDetail(
          _buildDetail(
            status: 'CANCELLED',
            estimatedDeliveryTime: now.add(const Duration(minutes: 10)),
          ),
          now: now,
        );
        expect(cancelled.freshnessStatus, EtaFreshnessStatus.inactive);
        expect(cancelled.remainingMinutes, isNull);
      },
    );

    test(
      'RouteEtaPreview computes great-circle distance and payload-adjusted duration within limits',
      () {
        final lightPreview = RouteEtaPreview.compute(
          originLatitude: 10.8538,
          originLongitude: 106.6278,
          destinationLatitude: 10.8411,
          destinationLongitude: 106.8099,
          weightGram: 500,
          isFragile: false,
        );
        final heavyFragilePreview = RouteEtaPreview.compute(
          originLatitude: 10.8538,
          originLongitude: 106.6278,
          destinationLatitude: 10.8411,
          destinationLongitude: 106.8099,
          weightGram: 2800,
          isFragile: true,
        );

        expect(lightPreview, isNotNull);
        expect(heavyFragilePreview, isNotNull);
        expect(lightPreview!.distanceKm, greaterThan(15));
        expect(
          heavyFragilePreview!.estimatedDurationMinutes,
          greaterThanOrEqualTo(lightPreview.estimatedDurationMinutes),
        );

        // Over-weight (> 3000g) or identical stations return null
        expect(
          RouteEtaPreview.compute(
            originLatitude: 10.8538,
            originLongitude: 106.6278,
            destinationLatitude: 10.8411,
            destinationLongitude: 106.8099,
            weightGram: 3500,
          ),
          isNull,
        );
        expect(
          RouteEtaPreview.compute(
            originLatitude: 10.8538,
            originLongitude: 106.6278,
            destinationLatitude: 10.8538,
            destinationLongitude: 106.6278,
            weightGram: 1000,
          ),
          isNull,
        );
      },
    );
  });

  group('EtaEstimationController and Screens (LA-40 / LA-160)', () {
    testWidgets(
      'displays ETA estimate on OrderDetailPage and updates when order status changes',
      (tester) async {
        tester.view.physicalSize = const Size(900, 1900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final futureEta = DateTime.now().toUtc().add(
          const Duration(minutes: 15),
        );
        final service = _FakeEtaOrderService(
          initialDetail: _buildDetail(
            status: 'APPROVED',
            distanceKm: 3.4,
            durationMinutes: 14,
            weather: 'Clear sky',
            confidence: 0.92,
            estimatedDeliveryTime: futureEta,
          ),
        );
        final mapRepo = _FakeDeliveryMapRepository();
        addTearDown(mapRepo.close);

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              deliveryOrderApiServiceProvider.overrideWithValue(service),
              deliveryMapRepositoryProvider.overrideWithValue(mapRepo),
            ],
            child: const MaterialApp(
              home: OrderDetailPage(orderId: _orderId),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('eta-estimation-card')), findsOneWidget);
        expect(find.text('Live estimate'), findsOneWidget);
        expect(find.text('3.4 km'), findsOneWidget);
        expect(find.text('14 minutes'), findsOneWidget);
        expect(find.text('Clear sky'), findsOneWidget);
        expect(find.text('92% (0.92)'), findsOneWidget);

        // Simulate order status changing to IN_TRANSIT with updated AI ETA prediction
        final updatedEta = DateTime.now().toUtc().add(
          const Duration(minutes: 9),
        );
        service.detail = _buildDetail(
          status: 'IN_TRANSIT',
          distanceKm: 2.1,
          durationMinutes: 9,
          weather: 'Moderate wind',
          confidence: 0.87,
          estimatedDeliveryTime: updatedEta,
        );

        final container = ProviderScope.containerOf(
          tester.element(find.byType(OrderDetailPage)),
        );
        container
            .read(etaEstimationControllerProvider(_orderId).notifier)
            .onExternalStatusObserved('IN_TRANSIT');
        await tester.pumpAndSettle();

        expect(find.text('2.1 km'), findsOneWidget);
        expect(find.text('9 minutes'), findsOneWidget);
        expect(find.text('Moderate wind'), findsOneWidget);
        expect(find.text('87% (0.87)'), findsOneWidget);

        // Simulate order status changing to ARRIVED -> stops live countdown
        service.detail = _buildDetail(
          status: 'ARRIVED',
          distanceKm: 2.1,
          durationMinutes: 9,
          weather: 'Moderate wind',
          confidence: 0.87,
          estimatedDeliveryTime: updatedEta,
        );
        container
            .read(etaEstimationControllerProvider(_orderId).notifier)
            .onExternalStatusObserved('ARRIVED');
        await tester.pumpAndSettle();

        expect(find.text('Arrived at station'), findsOneWidget);
        expect(
          find.textContaining('awaiting recipient pickup confirmation'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'transitions live ETA to Estimate stale automatically when estimatedDeliveryTime expires',
      (tester) async {
        final shortFutureEta = DateTime.now().toUtc().add(
          const Duration(seconds: 2),
        );
        final service = _FakeEtaOrderService(
          initialDetail: _buildDetail(
            status: 'IN_TRANSIT',
            distanceKm: 1.5,
            durationMinutes: 5,
            estimatedDeliveryTime: shortFutureEta,
          ),
        );
        final mapRepo = _FakeDeliveryMapRepository();
        addTearDown(mapRepo.close);

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              deliveryOrderApiServiceProvider.overrideWithValue(service),
              deliveryMapRepositoryProvider.overrideWithValue(mapRepo),
            ],
            child: const MaterialApp(
              home: Scaffold(body: EtaEstimationCard(orderId: _orderId)),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Live estimate'), findsOneWidget);

        // Advance clock past the estimated arrival deadline
        await tester.pump(const Duration(seconds: 3));
        await tester.pump();

        expect(find.text('Estimate stale'), findsOneWidget);
        expect(
          find.textContaining('previous arrival window has passed'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'clears cached ETA and latches accessLost across 403-to-503 retry sequence',
      (tester) async {
        final service = _FakeEtaOrderService(
          initialDetail: _buildDetail(
            status: 'IN_TRANSIT',
            distanceKm: 3.4,
            durationMinutes: 14,
            weather: 'Clear sky',
            confidence: 0.92,
          ),
        );

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              deliveryOrderApiServiceProvider.overrideWithValue(service),
            ],
            child: const MaterialApp(
              home: Scaffold(body: EtaEstimationCard(orderId: _orderId)),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('3.4 km'), findsOneWidget);
        expect(find.text('14 minutes'), findsOneWidget);

        // Next refresh returns 403, followed by 503 on automatic retries
        service.detailErrorQueue.add(
          const ApiException(
            message: 'An active customer account is required.',
            statusCode: 403,
          ),
        );
        service.detailError = const ApiException(
          message: 'Temporary server error.',
          statusCode: 503,
        );

        await tester.tap(find.byKey(const Key('refresh-eta-button')));
        await tester.pump();
        await tester.pump();

        final container = ProviderScope.containerOf(
          tester.element(find.byType(EtaEstimationCard)),
        );
        var state = container.read(etaEstimationControllerProvider(_orderId));
        expect(state.estimate, isNull);
        expect(state.accessLost, true);

        await tester.pumpAndSettle();

        state = container.read(etaEstimationControllerProvider(_orderId));
        expect(state.estimate, isNull);
        expect(state.accessLost, true);
        expect(find.text('3.4 km'), findsNothing);
        expect(find.text('14 minutes'), findsNothing);
        expect(
          find.text('An active customer account is required.'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'displays RouteEtaPreviewCard on CreateOrderPage and ReviewOrderPage when stations have coordinates',
      (tester) async {
        const stationsWithCoords = [
          LandingStation(
            id: _originStationId,
            stationCode: 'QTSC-01',
            name: 'QTSC Campus Hub',
            address: 'District 12',
            latitude: 10.8538,
            longitude: 106.6278,
            status: 'OPERATIONAL',
          ),
          LandingStation(
            id: _destinationStationId,
            stationCode: 'SHTP-02',
            name: 'SHTP Lab Station',
            address: 'Thu Duc',
            latitude: 10.8411,
            longitude: 106.8099,
            status: 'OPERATIONAL',
          ),
        ];
        const pkg = PackageModel(
          id: 'pkg-1',
          packageName: 'Lab sample kit',
          weightGram: 1200,
          lengthCm: 20,
          widthCm: 15,
          heightCm: 10,
          isFragile: true,
        );

        final router = GoRouter(
          initialLocation: RoutePaths.customerOrdersCreate,
          routes: [
            GoRoute(
              path: RoutePaths.customerOrdersCreate,
              builder: (_, _) => const CreateOrderPage(),
            ),
            GoRoute(
              path: RoutePaths.customerOrdersReview,
              builder: (_, _) => const ReviewOrderPage(),
            ),
          ],
        );
        addTearDown(router.dispose);

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              deliveryOrderApiServiceProvider.overrideWithValue(
                _FakeEtaOrderService(),
              ),
              orderPackagesProvider.overrideWith((ref) async => [pkg]),
              orderStationsProvider.overrideWith(
                (ref) async => stationsWithCoords,
              ),
              orderAddressesProvider.overrideWith((ref) async => []),
            ],
            child: MaterialApp.router(routerConfig: router),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('route-eta-preview-card')), findsNothing);

        await tester.tap(find.byKey(const Key('order-package')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Lab sample kit').last);
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('order-origin')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('QTSC Campus Hub · QTSC-01').last);
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('order-destination')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('SHTP Lab Station · SHTP-02').last);
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('route-eta-preview-card')), findsOneWidget);
        expect(find.text('AI estimate'), findsOneWidget);
        expect(find.textContaining('fragile handling'), findsOneWidget);
      },
    );
  });
}
