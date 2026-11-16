import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/paged_result.dart';
import 'package:smart_drone_delivery/data/models/station_operation_models.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/confirm_delivery_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/delivery_confirmation_controller.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/my_orders_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

const _arrivedOrderId = 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33';

const _arrivedOrder = DeliveryOrder(
  id: _arrivedOrderId,
  recipientName: 'Tran Quoc Bao',
  recipientPhone: '0912345678',
  originStationId: '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44',
  destinationStationId: 'a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21',
  status: 'ARRIVED',
  trackingNumber: 'DD-202611-0012',
);

DeliveryOrderDetailResponseDto _buildDetail({String status = 'ARRIVED'}) =>
    DeliveryOrderDetailResponseDto.fromJson({
      'id': _arrivedOrderId,
      'trackingNumber': 'DD-202611-0012',
      'recipientName': 'Tran Quoc Bao',
      'recipientPhone': '0912345678',
      'originStationId': '7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44',
      'destinationStationId': 'a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21',
      'status': status,
      'package': {'packageName': 'Lab sample kit', 'weightGram': 1200},
      'originStation': {'stationCode': 'HCM-01', 'name': 'District 1 Hub'},
      'destinationStation': {'stationCode': 'HCM-02', 'name': 'Thu Duc Hub'},
      'statusLogs': [
        {
          'fromStatus': 'IN_TRANSIT',
          'toStatus': status,
          'remarks': 'Landed at destination station',
          'createdAt': '2026-11-15T08:20:00Z',
        },
      ],
    });

final _confirmResponse = ConfirmPickupResponse.fromJson({
  'orderId': _arrivedOrderId,
  'trackingNumber': 'DD-202611-0012',
  'status': 'DELIVERED',
  'actualDeliveryTime': '2026-11-15T08:30:00Z',
  'slotId': 'b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10',
  'slotReleased': true,
});

class _FakeDeliveryOrderService extends DeliveryOrderApiService {
  _FakeDeliveryOrderService({
    DeliveryOrderDetailResponseDto? detail,
    ConfirmPickupResponse? confirmResponse,
  }) : detail = detail ?? _buildDetail(),
       confirmResponse = confirmResponse ?? _confirmResponse,
       super(Dio());

  DeliveryOrderDetailResponseDto detail;
  ConfirmPickupResponse confirmResponse;
  ApiException? detailError;
  final List<ApiException> detailErrorQueue = [];
  ApiException? confirmError;
  Completer<DeliveryOrderDetailResponseDto>? nextDetailCompleter;
  bool updateDetailToDeliveredOnConfirm = true;
  int detailLoadCount = 0;
  int confirmCallCount = 0;
  String? lastConfirmedOrderId;
  ConfirmDeliveryRequest? lastConfirmRequest;

  @override
  Future<PagedResult<DeliveryOrder>> getOrders(
    DeliveryOrderFilterParams params,
  ) async {
    return PagedResult(
      items: const [_arrivedOrder],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    );
  }

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async {
    detailLoadCount++;
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

  @override
  Future<ConfirmPickupResponse> confirmDelivery(
    String id,
    ConfirmDeliveryRequest request,
  ) async {
    confirmCallCount++;
    lastConfirmedOrderId = id;
    lastConfirmRequest = request;
    if (confirmError != null) {
      throw confirmError!;
    }
    if (updateDetailToDeliveredOnConfirm) {
      detail = _buildDetail(status: 'DELIVERED');
    }
    return confirmResponse;
  }
}

Future<void> _pumpWithRouter(
  WidgetTester tester,
  _FakeDeliveryOrderService service, {
  String initialLocation = '/customer/orders/$_arrivedOrderId',
}) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: initialLocation,
    routes: [
      GoRoute(
        path: RoutePaths.customerOrders,
        builder: (_, _) => const MyOrdersPage(),
      ),
      GoRoute(
        path: RoutePaths.customerOrderDetails,
        builder: (_, state) => OrderDetailPage(
          orderId: state.pathParameters['orderId'],
          order: state.extra is DeliveryOrder
              ? state.extra as DeliveryOrder
              : null,
        ),
      ),
      GoRoute(
        path: RoutePaths.customerOrderConfirmDelivery,
        builder: (_, state) => ConfirmDeliveryPage(
          orderId: state.pathParameters['orderId'] ?? '',
          initialOrder: state.extra is DeliveryOrder
              ? state.extra as DeliveryOrder
              : null,
        ),
      ),
    ],
  );
  addTearDown(router.dispose);

  await tester.pumpWidget(
    ProviderScope(
      overrides: [deliveryOrderApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('DeliveryConfirmationController', () {
    late _FakeDeliveryOrderService fakeService;

    setUp(() {
      fakeService = _FakeDeliveryOrderService();
    });

    test(
      'rejects malformed codes and non-ARRIVED orders without calling API',
      () async {
        final container = ProviderContainer(
          overrides: [
            deliveryOrderApiServiceProvider.overrideWithValue(fakeService),
          ],
        );
        addTearDown(container.dispose);

        final sub = container.listen(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
          (_, _) {},
        );
        addTearDown(sub.close);

        final notifier = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId).notifier,
        );

        final malformed = await notifier.confirmDelivery(
          secureCode: '12345',
          currentOrderStatus: 'ARRIVED',
        );
        expect(malformed, false);
        expect(
          container
              .read(deliveryConfirmationControllerProvider(_arrivedOrderId))
              .errorMessage,
          'Pickup code must be exactly 6 digits.',
        );
        expect(fakeService.lastConfirmRequest, isNull);

        final wrongStatus = await notifier.confirmDelivery(
          secureCode: '593284',
          currentOrderStatus: 'IN_TRANSIT',
        );
        expect(wrongStatus, false);
        expect(
          container
              .read(deliveryConfirmationControllerProvider(_arrivedOrderId))
              .errorMessage,
          'Only arrived orders can be confirmed for delivery.',
        );
        expect(fakeService.lastConfirmRequest, isNull);
      },
    );

    test(
      'surfaces 400 attemptsRemaining and 423 lockout with 0 attempts remaining',
      () async {
        final container = ProviderContainer(
          overrides: [
            deliveryOrderApiServiceProvider.overrideWithValue(fakeService),
          ],
        );
        addTearDown(container.dispose);

        final sub = container.listen(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
          (_, _) {},
        );
        addTearDown(sub.close);

        final notifier = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId).notifier,
        );

        fakeService.confirmError = const ApiException(
          message: 'Invalid pickup code.',
          statusCode: 400,
          attemptsRemaining: 2,
        );
        final res400 = await notifier.confirmDelivery(
          secureCode: '000000',
          currentOrderStatus: 'ARRIVED',
        );
        expect(res400, false);
        var state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.remainingAttempts, 2);
        expect(state.isLocked, false);

        final lockExpiry = DateTime.now().toUtc().add(
          const Duration(minutes: 10),
        );
        fakeService.confirmError = ApiException(
          message: 'Too many wrong codes. Try again later.',
          statusCode: 423,
          retryAfterSeconds: 600,
          lockoutExpiresAt: lockExpiry,
        );
        final res423 = await notifier.confirmDelivery(
          secureCode: '111111',
          currentOrderStatus: 'ARRIVED',
        );
        expect(res423, false);
        state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.isLocked, true);
        expect(state.remainingAttempts, 0);
        expect(state.lockoutSecondsRemaining, 600);
        expect(state.lockoutExpiresAt, lockExpiry);

        // Locked state prevents further API calls
        fakeService.lastConfirmRequest = null;
        final whileLocked = await notifier.confirmDelivery(
          secureCode: '593284',
          currentOrderStatus: 'ARRIVED',
        );
        expect(whileLocked, false);
        expect(fakeService.lastConfirmRequest, isNull);
      },
    );

    test(
      'marks requiresOrderRefresh on 409 Conflict and network errors, and accessLost on 403/404',
      () async {
        final container = ProviderContainer(
          overrides: [
            deliveryOrderApiServiceProvider.overrideWithValue(fakeService),
          ],
        );
        addTearDown(container.dispose);

        final sub = container.listen(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
          (_, _) {},
        );
        addTearDown(sub.close);

        final notifier = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId).notifier,
        );

        // Hold the refresh GET open so requiresOrderRefresh stays true until resolved
        fakeService.nextDetailCompleter =
            Completer<DeliveryOrderDetailResponseDto>();
        fakeService.confirmError = const ApiException(
          message: 'Order already confirmed.',
          statusCode: 409,
        );
        await notifier.confirmDelivery(
          secureCode: '593284',
          currentOrderStatus: 'ARRIVED',
        );
        var state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.requiresOrderRefresh, true);
        expect(state.errorMessage, 'Order already confirmed.');

        // Clear the refresh gate by completing a successful ARRIVED refresh
        await notifier.refreshOrderEligibility();
        state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.requiresOrderRefresh, false);

        fakeService.nextDetailCompleter =
            Completer<DeliveryOrderDetailResponseDto>();
        fakeService.confirmError = const ApiException(
          message: 'Network problem.',
          isNetwork: true,
        );
        await notifier.confirmDelivery(
          secureCode: '593284',
          currentOrderStatus: 'ARRIVED',
        );
        state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.requiresOrderRefresh, true);
        expect(state.errorMessage, contains('Connection lost'));

        await notifier.refreshOrderEligibility();
        fakeService.confirmError = const ApiException(
          message: 'Order not found or unavailable to your account.',
          statusCode: 404,
          code: 'MSG-P-CO-25',
        );
        await notifier.confirmDelivery(
          secureCode: '593284',
          currentOrderStatus: 'ARRIVED',
        );
        state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.accessLost, true);
      },
    );
  });

  group('Delivery Confirmation Screens (C08 -> C11/C12 -> C13)', () {
    testWidgets(
      'navigates from ARRIVED OrderDetailPage to ConfirmDeliveryPage, enters code via keypad, and completes delivery',
      (tester) async {
        final service = _FakeDeliveryOrderService();
        await _pumpWithRouter(tester, service);

        // C08: Arrived banner and Enter pickup code button on OrderDetailPage
        expect(find.textContaining('Arrived at Thu Duc Hub'), findsOneWidget);
        expect(
          find.byKey(const Key('enter-pickup-code-button')),
          findsOneWidget,
        );

        await tester.tap(find.byKey(const Key('enter-pickup-code-button')));
        await tester.pumpAndSettle();

        // C11: Confirm delivery screen
        expect(find.text('Enter the pickup code'), findsOneWidget);
        expect(
          find.textContaining('DD-202611-0012 · HCM-02 · Thu Duc Hub'),
          findsOneWidget,
        );

        // Tap digits 5-9-3-2-8-4 on keypad
        for (final digit in ['5', '9', '3', '2', '8', '4']) {
          await tester.tap(find.byKey(Key('keypad-digit-$digit')));
          await tester.pump();
        }

        final submitButton = tester.widget<FilledButton>(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        expect(submitButton.onPressed, isNotNull);

        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pumpAndSettle();

        expect(service.lastConfirmedOrderId, _arrivedOrderId);
        expect(service.lastConfirmRequest?.pickupSecureCode, '593284');

        // C13: Delivery Completed screen
        expect(find.text('Delivered'), findsOneWidget);
        expect(
          find.textContaining('Order DD-202611-0012 confirmed at'),
          findsOneWidget,
        );
        expect(
          find.text('Thank you for using SmartDroneDelivery.'),
          findsOneWidget,
        );
        expect(find.byKey(const Key('back-to-orders-button')), findsOneWidget);
        expect(
          find.byKey(const Key('view-order-history-button')),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'shows remaining attempts on 400 and disables confirmation on 423 lockout',
      (tester) async {
        final service = _FakeDeliveryOrderService();
        await _pumpWithRouter(
          tester,
          service,
          initialLocation:
              RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
        );

        // 400 wrong code with 2 tries left
        service.confirmError = const ApiException(
          message: 'Invalid pickup code.',
          statusCode: 400,
          attemptsRemaining: 2,
        );
        await tester.enterText(
          find.byKey(const Key('pickup-secure-code-input')),
          '000000',
        );
        await tester.pump();
        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pumpAndSettle();

        expect(find.text('Invalid pickup code.'), findsOneWidget);
        expect(
          find.textContaining('Wrong code. 2 tries left.'),
          findsOneWidget,
        );

        // 423 Locked with Retry-After = 600
        service.confirmError = ApiException(
          message: 'Too many wrong codes. Try again later.',
          statusCode: 423,
          retryAfterSeconds: 600,
          lockoutExpiresAt: DateTime.now().toUtc().add(
            const Duration(minutes: 10),
          ),
        );
        await tester.enterText(
          find.byKey(const Key('pickup-secure-code-input')),
          '111111',
        );
        await tester.pump();
        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pump();

        expect(find.textContaining('Attempts remaining: 0.'), findsOneWidget);
        expect(find.textContaining('Retry after 600s.'), findsOneWidget);

        final submitButton = tester.widget<FilledButton>(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        expect(submitButton.onPressed, isNull);
      },
    );

    testWidgets(
      'unlocks form and re-enables submission automatically when 423 lockout expires',
      (tester) async {
        final service = _FakeDeliveryOrderService();
        await _pumpWithRouter(
          tester,
          service,
          initialLocation:
              RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
        );

        // Return 423 Locked with a 2-second lockout
        service.confirmError = ApiException(
          message: 'Too many wrong codes. Try again later.',
          statusCode: 423,
          retryAfterSeconds: 2,
          lockoutExpiresAt: DateTime.now().toUtc().add(
            const Duration(seconds: 2),
          ),
        );

        await tester.enterText(
          find.byKey(const Key('pickup-secure-code-input')),
          '593284',
        );
        await tester.pump();
        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pump();

        // While locked, input and submit are disabled
        expect(
          tester
              .widget<TextField>(
                find.byKey(const Key('pickup-secure-code-input')),
              )
              .enabled,
          false,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-delivery-submit-button')),
              )
              .onPressed,
          isNull,
        );

        // Advance past the 2-second lockout without leaving the route
        service.confirmError = null;
        await tester.pump(const Duration(seconds: 3));

        expect(
          tester
              .widget<TextField>(
                find.byKey(const Key('pickup-secure-code-input')),
              )
              .enabled,
          true,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-delivery-submit-button')),
              )
              .onPressed,
          isNotNull,
        );

        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pumpAndSettle();

        expect(find.text('Delivered'), findsOneWidget);
      },
    );

    testWidgets(
      'gates submission after uncertain POST failure while refresh GET is pending or fails, and re-enables only after fresh ARRIVED read',
      (tester) async {
        final service = _FakeDeliveryOrderService();
        await _pumpWithRouter(
          tester,
          service,
          initialLocation:
              RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
        );

        await tester.enterText(
          find.byKey(const Key('pickup-secure-code-input')),
          '593284',
        );
        await tester.pump();

        // Hold the mandatory refresh GET open after a network failure on POST
        final pendingRefresh = Completer<DeliveryOrderDetailResponseDto>();
        service.nextDetailCompleter = pendingRefresh;
        service.confirmError = const ApiException(
          message: 'Network problem. Check your connection and try again.',
          isNetwork: true,
        );

        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pump();

        expect(service.confirmCallCount, 1);

        // Input and submit must remain disabled while the required GET is unresolved
        expect(
          tester
              .widget<TextField>(
                find.byKey(const Key('pickup-secure-code-input')),
              )
              .enabled,
          false,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-delivery-submit-button')),
              )
              .onPressed,
          isNull,
        );

        // If the refresh GET fails, preserve the block
        service.detailError = const ApiException(
          message: 'Temporary server error.',
          statusCode: 503,
        );
        pendingRefresh.completeError(service.detailError!);
        await tester.pumpAndSettle();

        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-delivery-submit-button')),
              )
              .onPressed,
          isNull,
        );

        // Now allow the refresh GET to succeed with ARRIVED
        service.detailError = null;
        service.confirmError = null;
        await tester.tap(find.byKey(const Key('refresh-order-status-button')));
        await tester.pumpAndSettle();

        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-delivery-submit-button')),
              )
              .onPressed,
          isNotNull,
        );

        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pumpAndSettle();

        expect(service.confirmCallCount, 2);
        expect(find.text('Delivered'), findsOneWidget);
      },
    );

    testWidgets(
      'clears completed confirmation and hides protected tracking data when refreshed GET returns 403',
      (tester) async {
        final service = _FakeDeliveryOrderService();
        await _pumpWithRouter(
          tester,
          service,
          initialLocation:
              RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
        );

        await tester.enterText(
          find.byKey(const Key('pickup-secure-code-input')),
          '593284',
        );
        await tester.pump();

        // POST succeeds, but the subsequent refreshed GET returns 403 Forbidden
        service.detailError = const ApiException(
          message: 'An active customer account is required.',
          statusCode: 403,
        );

        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pumpAndSettle();

        // Protected tracking number and slot ID must NOT be shown on screen
        expect(find.textContaining('DD-202611-0012'), findsNothing);
        expect(
          find.textContaining('b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10'),
          findsNothing,
        );
        expect(find.text('Delivered'), findsNothing);
        expect(
          find.text('An active customer account is required.'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'latches accessLost and clears completedConfirmation across 403-to-503 retry sequence without re-exposing tracking data',
      (tester) async {
        final service = _FakeDeliveryOrderService();
        await _pumpWithRouter(
          tester,
          service,
          initialLocation:
              RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
        );

        await tester.enterText(
          find.byKey(const Key('pickup-secure-code-input')),
          '593284',
        );
        await tester.pump();

        // POST succeeds, refreshed GET #1 returns 403, automatic retry GET #2+ returns 503
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

        await tester.tap(
          find.byKey(const Key('confirm-delivery-submit-button')),
        );
        await tester.pump();

        final container = ProviderScope.containerOf(
          tester.element(find.byType(ConfirmDeliveryPage)),
        );
        var state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.completedConfirmation, isNull);
        expect(state.accessLost, true);

        await tester.pumpAndSettle();

        state = container.read(
          deliveryConfirmationControllerProvider(_arrivedOrderId),
        );
        expect(state.completedConfirmation, isNull);
        expect(state.accessLost, true);
        expect(find.textContaining('DD-202611-0012'), findsNothing);
        expect(
          find.textContaining('b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10'),
          findsNothing,
        );
        expect(find.text('Delivered'), findsNothing);
        expect(
          find.text('An active customer account is required.'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      'clears completedConfirmation and latches accessLost immediately on 401, 403, and 404 refreshed GET',
      (tester) async {
        for (final statusCode in [401, 403, 404]) {
          final service = _FakeDeliveryOrderService();
          await _pumpWithRouter(
            tester,
            service,
            initialLocation:
                RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
          );

          await tester.enterText(
            find.byKey(const Key('pickup-secure-code-input')),
            '593284',
          );
          await tester.pump();

          service.detailError = ApiException(
            message: 'Access denied ($statusCode)',
            statusCode: statusCode,
            code: statusCode == 404 ? 'MSG-P-CO-25' : null,
          );

          await tester.tap(
            find.byKey(const Key('confirm-delivery-submit-button')),
          );
          await tester.pump();
          await tester.pump();

          final container = ProviderScope.containerOf(
            tester.element(find.byType(ConfirmDeliveryPage)),
          );
          final state = container.read(
            deliveryConfirmationControllerProvider(_arrivedOrderId),
          );
          expect(state.completedConfirmation, isNull);
          expect(state.accessLost, true);
          expect(find.textContaining('DD-202611-0012'), findsNothing);
          expect(
            find.textContaining('b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10'),
            findsNothing,
          );

          await tester.pumpAndSettle();
          await tester.pumpWidget(const SizedBox.shrink());
          await tester.pumpAndSettle();
        }
      },
    );

    testWidgets(
      'shows ineligible state when order is not ARRIVED',
      (tester) async {
        final service = _FakeDeliveryOrderService(
          detail: _buildDetail(status: 'IN_TRANSIT'),
        );
        await _pumpWithRouter(
          tester,
          service,
          initialLocation:
              RoutePaths.customerOrderConfirmDeliveryFor(_arrivedOrderId),
        );

        expect(
          find.text('Order not ready for confirmation'),
          findsOneWidget,
        );
        expect(find.textContaining('currently IN_TRANSIT'), findsOneWidget);
        expect(find.byKey(const Key('pickup-secure-code-input')), findsNothing);
      },
    );
  });
}
