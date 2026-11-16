import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/theme/theme_mode_provider.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_detail_response_dto.dart';
import 'package:smart_drone_delivery/data/models/delivery_order_filter_params.dart';
import 'package:smart_drone_delivery/data/models/notification_model.dart';
import 'package:smart_drone_delivery/data/models/paged_result.dart';
import 'package:smart_drone_delivery/data/providers/delivery_order_providers.dart';
import 'package:smart_drone_delivery/data/providers/notification_providers.dart';
import 'package:smart_drone_delivery/data/services/delivery_order_api_service.dart';
import 'package:smart_drone_delivery/data/services/notification_api_service.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/domain/repositories/profile_repository.dart';
import 'package:smart_drone_delivery/data/providers/repository_providers.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/ai_assistant_page.dart';
import 'package:smart_drone_delivery/presentation/customer/notifications/notifications_list_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_detail_page.dart';
import 'package:smart_drone_delivery/presentation/customer/orders/order_history_page.dart';
import 'package:smart_drone_delivery/presentation/customer/tracking/order_tracking_page.dart';
import 'package:smart_drone_delivery/presentation/profile/profile_page.dart';
import 'package:smart_drone_delivery/presentation/profile/theme_settings_page.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

import '../../support/fake_tracking_repository.dart';

const _orderId = 'order-1';
const _trackingNumber = 'DD-202611-0012';

DeliveryOrderDetailResponseDto _detail({
  String status = 'PENDING',
  List<Map<String, Object?>> logs = const [],
}) => DeliveryOrderDetailResponseDto.fromJson({
  'id': _orderId,
  'trackingNumber': _trackingNumber,
  'recipientName': 'Customer One',
  'recipientPhone': '0900000000',
  'originStationId': 'origin',
  'destinationStationId': 'destination',
  'status': status,
  'statusLogs': logs,
});

class _FakeDeliveryOrderService extends DeliveryOrderApiService {
  _FakeDeliveryOrderService({DeliveryOrderDetailResponseDto? detail})
    : detail = detail ?? _detail(),
      super(Dio());

  final DeliveryOrderDetailResponseDto detail;

  @override
  Future<PagedResult<DeliveryOrder>> getOrders(
    DeliveryOrderFilterParams params,
  ) async => const PagedResult(
    items: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  );

  @override
  Future<DeliveryOrderDetailResponseDto> getOrderDetail(String id) async =>
      detail;
}

class _FakeNotificationApiService extends NotificationApiService {
  _FakeNotificationApiService() : super(Dio());

  final notification = NotificationModel(
    id: 'notice-1',
    orderId: _orderId,
    type: 'ORDER_STATUS_CHANGED',
    title: 'Order updated',
    message: 'Your order status changed.',
    isRead: true,
    createdAt: DateTime.utc(2026, 11, 15),
  );

  @override
  Future<PagedResult<NotificationModel>> getNotifications({
    bool? isRead,
    int page = 1,
    int pageSize = 20,
  }) async => PagedResult(
    items: [notification],
    page: page,
    pageSize: pageSize,
    totalCount: 1,
    totalPages: 1,
  );
}

class _FakeProfileRepository implements ProfileRepository {
  @override
  Future<User> getProfile() async => User(
    id: 'customer-1',
    email: 'customer@example.com',
    fullName: 'Customer One',
    phoneNumber: '0900000000',
    status: 'ACTIVE',
    roleCode: 'CUSTOMER',
    createdAt: DateTime.utc(2025),
  );

  @override
  Future<User> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) => throw UnimplementedError();

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) => throw UnimplementedError();

  @override
  Future<String> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) => throw UnimplementedError();

  @override
  Future<String> uploadAvatarFromFile(String filePath) =>
      throw UnimplementedError();
}

GoRouter _router(String initialLocation) => GoRouter(
  initialLocation: initialLocation,
  routes: [
    GoRoute(
      path: RoutePaths.customerOrderDetails,
      builder: (_, state) =>
          OrderDetailPage(orderId: state.pathParameters['orderId']),
    ),
    GoRoute(
      path: RoutePaths.customerOrderHistory,
      builder: (_, state) =>
          OrderHistoryPage(orderId: state.pathParameters['orderId']!),
    ),
    GoRoute(
      path: RoutePaths.customerAssistant,
      builder: (_, state) => AiAssistantPage(
        orderId: state.uri.queryParameters['orderId'],
        trackingNumber: state.uri.queryParameters['trackingNumber'],
      ),
    ),
    GoRoute(
      path: RoutePaths.customerOrderTracking,
      builder: (_, state) =>
          OrderTrackingPage(orderId: state.pathParameters['orderId']!),
    ),
    GoRoute(
      path: RoutePaths.customerNotifications,
      builder: (_, _) => const NotificationsListPage(),
    ),
    GoRoute(path: RoutePaths.profile, builder: (_, _) => const ProfilePage()),
    GoRoute(
      path: RoutePaths.themeSettings,
      builder: (_, _) => const ThemeSettingsPage(),
    ),
  ],
);

Future<void> _pumpRouter(
  WidgetTester tester, {
  required String location,
  DeliveryOrderDetailResponseDto? detail,
  _FakeNotificationApiService? notifications,
  bool withProfile = false,
  FakeTrackingRepository? tracking,
}) async {
  tester.view.physicalSize = const Size(800, 1800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = _router(location);
  addTearDown(router.dispose);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        deliveryOrderApiServiceProvider.overrideWithValue(
          _FakeDeliveryOrderService(detail: detail),
        ),
        if (notifications != null)
          notificationApiServiceProvider.overrideWithValue(notifications),
        if (withProfile)
          profileRepositoryProvider.overrideWithValue(_FakeProfileRepository()),
        if (tracking != null)
          trackingRepositoryProvider.overrideWithValue(tracking),
      ],
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('C10 Order History', () {
    testWidgets('renders audit transitions, actor, UTC timestamp and remarks', (
      tester,
    ) async {
      final service = _FakeDeliveryOrderService(
        detail: _detail(
          logs: [
            {
              'fromStatus': 'PENDING',
              'toStatus': 'APPROVED',
              'changedBy': 'Dispatcher A',
              'remarks': 'Package verified',
              'createdAt': '2026-11-15T08:20:00Z',
            },
            {
              'fromStatus': 'APPROVED',
              'toStatus': 'IN_TRANSIT',
              'changedBy': 'Operator B',
              'remarks': 'Drone departed',
              'createdAt': '2026-11-15T09:00:00Z',
            },
          ],
        ),
      );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            deliveryOrderApiServiceProvider.overrideWithValue(service),
          ],
          child: const MaterialApp(home: OrderHistoryPage(orderId: _orderId)),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('PENDING → APPROVED'), findsOneWidget);
      expect(find.text('Changed by Dispatcher A'), findsOneWidget);
      expect(find.text('Package verified'), findsOneWidget);
      expect(find.text('2026-11-15T08:20:00.000Z'), findsOneWidget);
      expect(find.text('APPROVED → IN_TRANSIT'), findsOneWidget);
      expect(find.text('Changed by Operator B'), findsOneWidget);
    });

    testWidgets('shows the required empty history state', (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            deliveryOrderApiServiceProvider.overrideWithValue(
              _FakeDeliveryOrderService(),
            ),
          ],
          child: const MaterialApp(home: OrderHistoryPage(orderId: _orderId)),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('No order history found.'), findsOneWidget);
    });
  });

  group('C23 AI Assistant local simulation', () {
    testWidgets('shows quick prompts and returns local guidance', (
      tester,
    ) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: AiAssistantPage(
            orderId: _orderId,
            trackingNumber: _trackingNumber,
          ),
        ),
      );
      expect(find.byKey(const Key('assistant-prompt-0')), findsOneWidget);
      await tester.tap(find.text('Where is my order?'));
      await tester.pump();
      expect(
        find.textContaining('This local demo has your order context'),
        findsOneWidget,
      );
    });

    testWidgets('refuses another customer tracking codes', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: AiAssistantPage(
            orderId: _orderId,
            trackingNumber: _trackingNumber,
          ),
        ),
      );
      await tester.enterText(
        find.byKey(const Key('assistant-message-input')),
        'Where is DD-202611-9999?',
      );
      await tester.tap(find.byKey(const Key('assistant-send-button')));
      await tester.pump();
      expect(
        find.textContaining('I can’t access or discuss another customer’s'),
        findsOneWidget,
      );
    });
  });

  testWidgets('C27 applies System, Light, and Dark theme modes immediately', (
    tester,
  ) async {
    await tester.pumpWidget(
      const ProviderScope(child: MaterialApp(home: ThemeSettingsPage())),
    );
    final container = ProviderScope.containerOf(
      tester.element(find.byType(ThemeSettingsPage)),
    );

    expect(container.read(themeModeProvider), ThemeMode.system);
    await tester.tap(find.text('Light'));
    await tester.pump();
    expect(container.read(themeModeProvider), ThemeMode.light);
    await tester.tap(find.text('Dark'));
    await tester.pump();
    expect(container.read(themeModeProvider), ThemeMode.dark);
    await tester.tap(find.text('System Default'));
    await tester.pump();
    expect(container.read(themeModeProvider), ThemeMode.system);
  });

  for (final status in ['PENDING', 'ARRIVED']) {
    testWidgets(
      'C07/C08 order detail actions open history and assistant ($status)',
      (tester) async {
        await _pumpRouter(
          tester,
          location: '/customer/orders/$_orderId',
          detail: _detail(status: status),
        );
        await tester.ensureVisible(
          find.byKey(const Key('ask-ai-assistant-button')),
        );
        await tester.tap(find.byKey(const Key('ask-ai-assistant-button')));
        await tester.pumpAndSettle();
        expect(find.text('AI Assistant'), findsOneWidget);
        expect(find.text(_trackingNumber), findsOneWidget);

        await tester.pageBack();
        await tester.pumpAndSettle();
        await tester.ensureVisible(
          find.byKey(const Key('view-order-history-button')),
        );
        await tester.tap(find.byKey(const Key('view-order-history-button')));
        await tester.pumpAndSettle();
        expect(find.text('Order history'), findsOneWidget);
      },
    );
  }

  testWidgets('C06 tracking actions pass order context to assistant/history', (
    tester,
  ) async {
    final repository = FakeTrackingRepository()..connectOnSubscribe = null;
    addTearDown(repository.close);
    await _pumpRouter(
      tester,
      location: '/customer/orders/tracking/$_orderId',
      tracking: repository,
    );
    await tester.ensureVisible(find.byKey(const Key('ask-ai-assistant')));
    await tester.tap(find.byKey(const Key('ask-ai-assistant')));
    await tester.pumpAndSettle();
    expect(find.text('AI Assistant'), findsOneWidget);
    expect(find.text(_trackingNumber), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('view-order-history')));
    await tester.tap(find.byKey(const Key('view-order-history')));
    await tester.pumpAndSettle();
    expect(find.text('Order history'), findsOneWidget);
  });

  testWidgets('C22 order notification opens its order details', (tester) async {
    await _pumpRouter(
      tester,
      location: RoutePaths.customerNotifications,
      notifications: _FakeNotificationApiService(),
    );
    await tester.tap(find.text('View order'));
    await tester.pumpAndSettle();
    expect(find.text('Order details'), findsOneWidget);
  });

  testWidgets('C24 profile links to AI Assistant and Theme Settings', (
    tester,
  ) async {
    await _pumpRouter(tester, location: RoutePaths.profile, withProfile: true);
    await tester.ensureVisible(find.text('AI Assistant'));
    await tester.tap(find.text('AI Assistant'));
    await tester.pumpAndSettle();
    expect(find.text('AI Assistant'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Theme Settings'));
    await tester.tap(find.text('Theme Settings'));
    await tester.pumpAndSettle();
    expect(find.text('System Default'), findsOneWidget);
  });
}
