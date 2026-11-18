import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
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
  Completer<DeliveryOrderDetailResponseDto>? nextDetailCompleter;
  int detailCallCount = 0;

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async {
    detailCallCount++;
    if (nextDetailCompleter != null) {
      final completer = nextDetailCompleter!;
      nextDetailCompleter = null;
      return completer.future;
    }
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
  _FakeDeliveryMapRepository({
    DeliveryMapOrderStatus status = DeliveryMapOrderStatus.inTransit,
    DateTime? estimatedDeliveryTime,
    bool telemetryStale = false,
  }) : snapshot = _buildMapSnapshot(
         status: status,
         estimatedDeliveryTime: estimatedDeliveryTime,
         telemetryStale: telemetryStale,
       );

  final _controller = StreamController<DeliveryMapUpdate>.broadcast();
  DeliveryMapSnapshot snapshot;

  static DeliveryMapSnapshot _buildMapSnapshot({
    DeliveryMapOrderStatus status = DeliveryMapOrderStatus.inTransit,
    DateTime? estimatedDeliveryTime,
    bool telemetryStale = false,
  }) => DeliveryMapSnapshot(
    orderId: _orderId,
    trackingNumber: 'DD-202611-0401',
    status: status,
    originStation: const MapStationPoint(
      id: _originStationId,
      name: 'QTSC Campus Hub',
      latitude: 10.8538,
      longitude: 106.6278,
    ),
    destinationStation: const MapStationPoint(
      id: _destinationStationId,
      name: 'SHTP Lab Station',
      latitude: 10.8411,
      longitude: 106.8099,
    ),
    estimatedDeliveryTime: estimatedDeliveryTime,
    lastPosition: null,
    telemetryStale: telemetryStale,
  );

  void emitStatusChanged(String orderId) {
    _controller.add(
      DeliveryMapUpdate(
        type: DeliveryMapUpdateType.statusChanged,
        orderId: orderId,
      ),
    );
  }

  void emitConnected() {
    _controller.add(
      const DeliveryMapUpdate(type: DeliveryMapUpdateType.connected),
    );
  }

  Future<void> close() => _controller.close();

  @override
  Future<DeliveryMapSnapshot> getMapSnapshot(String orderId) async => snapshot;

  @override
  Stream<DeliveryMapUpdate> watchOrderMap(String orderId) => _controller.stream;
}

void main() {
  group('OrderEtaEstimate and RouteEtaPreview models (LA-40 / LA-160)', () {
    test('validates positive distance/duration, [0,1] confidence, and never invents weather or timestamp', () {
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
      expect(invalidEstimate.freshnessStatus, EtaFreshnessStatus.unavailable);
    });

    test('distinguishes PENDING, active, stale, ARRIVED, DELIVERED, and CANCELLED/FAILED states', () {
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
      expect(arrived.freshnessStatus, EtaFreshnessStatus.arrivedAtDestination);
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
    });

    test('RouteEtaPreview computes great-circle distance and payload-adjusted duration within limits', () {
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
    });
  });

  group('EtaEstimationController and Screens (LA-40 / LA-160)', () {
    testWidgets(
      'displays ETA estimate on OrderDetailPage and updates across PENDING -> APPROVED -> IN_TRANSIT -> ARRIVED -> DELIVERED via repository StatusChanged events',
      (tester) async {
        tester.view.physicalSize = const Size(900, 1900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final service = _FakeEtaOrderService(
          initialDetail: _buildDetail(
            status: 'PENDING',
            includePrediction: false,
            estimatedDeliveryTime: null,
          ),
        );
        final mapRepo = _FakeDeliveryMapRepository(
          status: DeliveryMapOrderStatus.pending,
        );
        addTearDown(mapRepo.close);

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              deliveryOrderApiServiceProvider.overrideWithValue(service),
              deliveryMapRepositoryProvider.overrideWithValue(mapRepo),
            ],
            child: const MaterialApp(home: OrderDetailPage(orderId: _orderId)),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('eta-estimation-card')), findsOneWidget);
        expect(find.text('Waiting for approval'), findsOneWidget);

        // Transition PENDING -> APPROVED via real repository StatusChanged event
        final approvedEta = DateTime.now().toUtc().add(
          const Duration(minutes: 15),
        );
        service.detail = _buildDetail(
          status: 'APPROVED',
          distanceKm: 3.4,
          durationMinutes: 14,
          weather: 'Clear sky',
          confidence: 0.92,
          estimatedDeliveryTime: approvedEta,
        );
        mapRepo.emitStatusChanged(_orderId);
        await tester.pumpAndSettle();

        expect(find.text('Live estimate'), findsOneWidget);
        expect(find.text('3.4 km'), findsOneWidget);
        expect(find.text('14 minutes'), findsOneWidget);
        expect(find.text('Clear sky'), findsOneWidget);
        expect(find.text('92% (0.92)'), findsOneWidget);

        // Transition APPROVED -> IN_TRANSIT via real repository StatusChanged event
        final inTransitEta = DateTime.now().toUtc().add(
          const Duration(minutes: 9),
        );
        service.detail = _buildDetail(
          status: 'IN_TRANSIT',
          distanceKm: 2.1,
          durationMinutes: 9,
          weather: 'Moderate wind',
          confidence: 0.87,
          estimatedDeliveryTime: inTransitEta,
        );
        mapRepo.emitStatusChanged(_orderId);
        await tester.pumpAndSettle();

        expect(find.text('2.1 km'), findsOneWidget);
        expect(find.text('9 minutes'), findsOneWidget);
        expect(find.text('Moderate wind'), findsOneWidget);
        expect(find.text('87% (0.87)'), findsOneWidget);

        // Transition IN_TRANSIT -> ARRIVED via real repository StatusChanged event
        service.detail = _buildDetail(
          status: 'ARRIVED',
          distanceKm: 2.1,
          durationMinutes: 9,
          weather: 'Moderate wind',
          confidence: 0.87,
          estimatedDeliveryTime: inTransitEta,
        );
        mapRepo.emitStatusChanged(_orderId);
        await tester.pumpAndSettle();

        expect(find.text('Arrived at station'), findsOneWidget);
        expect(
          find.textContaining('awaiting recipient pickup confirmation'),
          findsOneWidget,
        );

        // Transition ARRIVED -> DELIVERED via real repository StatusChanged event
        final deliveredTime = DateTime.now().toUtc();
        service.detail = _buildDetail(
          status: 'DELIVERED',
          distanceKm: 2.1,
          durationMinutes: 9,
          weather: 'Moderate wind',
          confidence: 0.87,
          estimatedDeliveryTime: inTransitEta,
          actualDeliveryTime: deliveredTime,
        );
        mapRepo.emitStatusChanged(_orderId);
        await tester.pumpAndSettle();

        expect(find.text('Completed'), findsOneWidget);
        expect(find.textContaining('Delivery completed'), findsOneWidget);
      },
    );

    testWidgets(
      'detects corrected estimatedDeliveryTime while remaining IN_TRANSIT and fresh via both screen map refresh and direct notifier call',
      (tester) async {
        tester.view.physicalSize = const Size(900, 1900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final etaPlus15 = DateTime.now().toUtc().add(
          const Duration(minutes: 15),
        );
        final service = _FakeEtaOrderService(
          initialDetail: _buildDetail(
            status: 'IN_TRANSIT',
            distanceKm: 3.4,
            durationMinutes: 15,
            weather: 'Clear sky',
            confidence: 0.92,
            estimatedDeliveryTime: etaPlus15,
          ),
        );
        final mapRepo = _FakeDeliveryMapRepository(
          status: DeliveryMapOrderStatus.inTransit,
          estimatedDeliveryTime: etaPlus15,
        );
        addTearDown(mapRepo.close);

        await tester.pumpWidget(
          ProviderScope(
            overrides: [
              deliveryOrderApiServiceProvider.overrideWithValue(service),
              deliveryMapRepositoryProvider.overrideWithValue(mapRepo),
            ],
            child: const MaterialApp(home: OrderDetailPage(orderId: _orderId)),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.textContaining('(in 15 min)'), findsOneWidget);

        // Canonical map refresh corrects ETA from +15 to +25 minutes while status remains IN_TRANSIT and fresh
        final etaPlus25 = DateTime.now().toUtc().add(
          const Duration(minutes: 25),
        );
        mapRepo.snapshot = _FakeDeliveryMapRepository._buildMapSnapshot(
          status: DeliveryMapOrderStatus.inTransit,
          estimatedDeliveryTime: etaPlus25,
        );
        service.detail = _buildDetail(
          status: 'IN_TRANSIT',
          distanceKm: 3.4,
          durationMinutes: 25,
          weather: 'Headwind',
          confidence: 0.85,
          estimatedDeliveryTime: etaPlus25,
        );
        mapRepo.emitConnected();
        await tester.pumpAndSettle();

        expect(find.textContaining('(in 25 min)'), findsOneWidget);
        expect(find.text('25 minutes'), findsOneWidget);

        // Direct notifier call correcting ETA to +35 minutes while status and telemetryStale remain unchanged
        final etaPlus35 = DateTime.now().toUtc().add(
          const Duration(minutes: 35),
        );
        final container = ProviderScope.containerOf(
          tester.element(find.byType(OrderDetailPage)),
        );
        container
            .read(etaEstimationControllerProvider(_orderId).notifier)
            .onExternalStatusObserved(
              'IN_TRANSIT',
              updatedEstimatedDeliveryTime: etaPlus35,
            );
        await tester.pumpAndSettle();

        final state = container.read(etaEstimationControllerProvider(_orderId));
        expect(state.estimate?.remainingMinutes, 35);
        expect(find.textContaining('(in 35 min)'), findsOneWidget);
      },
    );

    testWidgets(
      'updates relative countdown as time advances and transitions live ETA to Estimate stale when estimatedDeliveryTime expires',
      (tester) async {
        final futureEta15 = DateTime.now().toUtc().add(
          const Duration(minutes: 15),
        );
        final service = _FakeEtaOrderService(
          initialDetail: _buildDetail(
            status: 'IN_TRANSIT',
            distanceKm: 1.5,
            durationMinutes: 15,
            estimatedDeliveryTime: futureEta15,
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
        expect(find.textContaining('(in 15 min)'), findsOneWidget);

        // Advance clock by 5 minutes -> countdown updates from 15 min to 10 min
        await tester.pump(const Duration(minutes: 5));
        await tester.pump();
        expect(find.textContaining('(in 10 min)'), findsOneWidget);

        // Advance clock past the remaining 10 minutes -> transitions to Estimate stale
        await tester.pump(const Duration(minutes: 11));
        await tester.pump();

        expect(find.text('Estimate stale'), findsOneWidget);
        expect(
          find.textContaining('previous arrival window has passed'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'preserves newer ARRIVED observation when delayed IN_TRANSIT refresh completes and queues canonical reconciliation',
      (tester) async {
        final futureEta = DateTime.now().toUtc().add(
          const Duration(minutes: 15),
        );
        final staleInTransitDetail = _buildDetail(
          status: 'IN_TRANSIT',
          distanceKm: 3.4,
          durationMinutes: 15,
          weather: 'Clear sky',
          confidence: 0.92,
          estimatedDeliveryTime: futureEta,
        );
        final service = _FakeEtaOrderService(
          initialDetail: staleInTransitDetail,
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

        expect(find.text('Live estimate'), findsOneWidget);
        final initialCalls = service.detailCallCount;

        // Start refresh A whose response is delayed and will resolve with old IN_TRANSIT
        final delayedRefreshA = Completer<DeliveryOrderDetailResponseDto>();
        service.nextDetailCompleter = delayedRefreshA;

        final container = ProviderScope.containerOf(
          tester.element(find.byType(EtaEstimationCard)),
        );
        final notifier = container.read(
          etaEstimationControllerProvider(_orderId).notifier,
        );
        unawaited(notifier.refreshEta());
        await tester.pump();
        expect(service.detailCallCount, initialCalls + 1);

        // While A is pending, a newer ARRIVED observation arrives
        service.detail = _buildDetail(
          status: 'ARRIVED',
          distanceKm: 3.4,
          durationMinutes: 15,
          weather: 'Clear sky',
          confidence: 0.92,
          estimatedDeliveryTime: futureEta,
        );
        notifier.onExternalStatusObserved('ARRIVED', forceRefresh: true);
        await tester.pump();

        // State must immediately reflect ARRIVED with no live countdown
        var state = container.read(etaEstimationControllerProvider(_orderId));
        expect(
          state.estimate?.freshnessStatus,
          EtaFreshnessStatus.arrivedAtDestination,
        );
        expect(state.estimate?.remainingMinutes, isNull);
        expect(find.text('Arrived at station'), findsOneWidget);

        // Now complete delayed request A with the old IN_TRANSIT detail
        delayedRefreshA.complete(staleInTransitDetail);
        await tester.pumpAndSettle();

        // Stale request A must NOT revive IN_TRANSIT or live countdown, and queued canonical refresh B must run
        expect(service.detailCallCount, initialCalls + 2);
        state = container.read(etaEstimationControllerProvider(_orderId));
        expect(
          state.estimate?.freshnessStatus,
          EtaFreshnessStatus.arrivedAtDestination,
        );
        expect(state.estimate?.remainingMinutes, isNull);
        expect(find.text('Arrived at station'), findsOneWidget);
        expect(find.text('Live estimate'), findsNothing);
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
        await tester.tap(
          find.text('Lab sample kit · 1.2 kg · Fragile').last,
        );
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('order-origin')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('QTSC Campus Hub · QTSC-01').last);
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('order-destination')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('SHTP Lab Station · SHTP-02').last);
        await tester.pumpAndSettle();

        await tester.drag(find.byType(ListView), const Offset(0, -300));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('route-eta-preview-card')), findsOneWidget);
        expect(find.text('AI estimate'), findsOneWidget);
        expect(find.textContaining('fragile handling'), findsOneWidget);
      },
    );
  });
}
