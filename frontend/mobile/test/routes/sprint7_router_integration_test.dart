import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/config/app_config.dart';
import 'package:smart_drone_delivery/core/session/auth_controller.dart';
import 'package:smart_drone_delivery/data/models/delivery_order.dart';
import 'package:smart_drone_delivery/data/providers/ai_assistant_providers.dart';
import 'package:smart_drone_delivery/data/services/ai_assistant_api_service.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/ai_assistant_page.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/customer_assistant_page.dart';
import 'package:smart_drone_delivery/presentation/operator/home/station_availability_providers.dart';
import 'package:smart_drone_delivery/presentation/operator/intake/package_intake_page.dart';
import 'package:smart_drone_delivery/presentation/operator/pickup/package_pickup_page.dart';
import 'package:smart_drone_delivery/routes/app_router.dart';
import 'package:smart_drone_delivery/routes/route_paths.dart';

import '../support/fake_http_adapter.dart';

const _orderId = 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33';
const _sessionId = '83ec4120-6388-426f-80a7-c8b627998d32';
const _trackingNumber = 'DD-202611-001';

class _SignedIn extends AuthController {
  @override
  AuthStatus build() => AuthStatus.authenticated;
}

class _CurrentUser extends CurrentUserController {
  _CurrentUser(this.role);
  final String role;

  @override
  User? build() => User(
    id: 'signed-in-user',
    email: 'test@example.com',
    fullName: 'Router Test',
    phoneNumber: '0900000001',
    roleCode: role,
    status: 'ACTIVE',
    createdAt: DateTime.utc(2026, 11, 17),
  );
}

class _UnassignedStation extends StationAvailabilityNotifier {
  @override
  Future<StationAvailabilityState> build() async =>
      const StationAvailabilityState(
        stations: [],
        selectedStationId: null,
        availability: null,
        detail: null,
        isUnassignedOperator: true,
      );
}

void main() {
  final cases = [
    (
      name: 'query order and persisted session',
      location: RoutePaths.customerAssistantFor(
        orderId: _orderId,
        trackingNumber: _trackingNumber,
        sessionId: _sessionId,
      ),
      extra: <String, String>{'sessionId': 'ignored-extra-session'},
      sessionId: _sessionId,
    ),
    (
      name: 'DeliveryOrder extra from customer screens',
      location: RoutePaths.customerAssistant,
      extra: const DeliveryOrder(
        id: _orderId,
        recipientName: 'Recipient',
        recipientPhone: '0900000002',
        originStationId: 'origin',
        destinationStationId: 'destination',
        status: 'APPROVED',
        trackingNumber: _trackingNumber,
      ),
      sessionId: null,
    ),
    (
      name: 'query overrides extra order and keeps extra session',
      location: RoutePaths.customerAssistantFor(
        orderId: _orderId,
        trackingNumber: _trackingNumber,
      ),
      extra: <String, String>{
        'orderId': 'ignored-extra-order',
        'trackingNumber': 'ignored-extra-tracking',
        'sessionId': _sessionId,
      },
      sessionId: _sessionId,
    ),
  ];

  for (final entry in cases) {
    testWidgets('customer router opens API assistant with ${entry.name}', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(800, 1600);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final requests = <RequestOptions>[];
      final dio = Dio(BaseOptions(baseUrl: 'https://test.invalid/api'));
      dio.httpClientAdapter = FakeHttpAdapter((request) {
        requests.add(request);
        if (request.method == 'POST') {
          return jsonBody({
            'outcome': 'HELP',
            'sessionId': null,
            'userMessageId': null,
            'reply': {
              'id': null,
              'senderType': 'BOT',
              'messageText': 'Response from the assistant API fixture.',
              'createdAt': '2026-11-17T04:00:00Z',
            },
          });
        }
        return jsonBody({
          if (request.path.contains('/messages')) 'sessionId': _sessionId,
          'items': [],
          'page': 1,
          'pageSize': 20,
          'totalCount': 0,
        });
      });
      addTearDown(() => dio.close(force: true));
      final container = ProviderContainer(
        overrides: [
          appConfigProvider.overrideWithValue(AppConfig.customer()),
          authControllerProvider.overrideWith(_SignedIn.new),
          currentUserProvider.overrideWith(() => _CurrentUser('CUSTOMER')),
          aiAssistantChatEnabledProvider.overrideWithValue(true),
          aiAssistantApiServiceProvider.overrideWithValue(
            AiAssistantApiService(dio),
          ),
        ],
      );
      addTearDown(container.dispose);
      final router = container.read(routerProvider);
      router.go(entry.location, extra: entry.extra);
      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: MaterialApp.router(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byType(AiAssistantPage), findsNothing);
      final page = tester.widget<CustomerAssistantPage>(
        find.byType(CustomerAssistantPage),
      );
      expect(page.orderId, _orderId);
      expect(page.trackingNumber, _trackingNumber);
      expect(page.sessionId, entry.sessionId);
      expect(requests.where((r) => r.path.endsWith('/sessions')), hasLength(1));
      expect(
        requests.where((r) => r.path.endsWith('/$_sessionId/messages')),
        hasLength(entry.sessionId == null ? 0 : 1),
      );

      await tester.enterText(
        find.byKey(const Key('assistant-message-input')),
        'How do I collect my delivery?',
      );
      await tester.pump();
      await tester.tap(find.byKey(const Key('assistant-send-button')));
      await tester.pumpAndSettle();
      final sent = requests.singleWhere((r) => r.method == 'POST');
      expect(sent.data, {
        'sessionId': entry.sessionId,
        'orderId': _orderId,
        'message': 'How do I collect my delivery?',
      });
      expect(
        find.text('Response from the assistant API fixture.'),
        findsOneWidget,
      );
    });
  }

  testWidgets(
    'operator router keeps intake and pickup destinations after assistant merge',
    (tester) async {
      final container = ProviderContainer(
        overrides: [
          appConfigProvider.overrideWithValue(AppConfig.operator()),
          authControllerProvider.overrideWith(_SignedIn.new),
          currentUserProvider.overrideWith(
            () => _CurrentUser('STATION_OPERATOR'),
          ),
          stationAvailabilityNotifierProvider.overrideWith(
            _UnassignedStation.new,
          ),
        ],
      );
      addTearDown(container.dispose);
      final router = container.read(routerProvider);
      router.go(RoutePaths.operatorIntake);
      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: MaterialApp.router(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byType(PackageIntakePage), findsOneWidget);
      router.go(RoutePaths.operatorPickup);
      await tester.pumpAndSettle();
      expect(find.byType(PackagePickupPage), findsOneWidget);
      expect(find.byType(CustomerAssistantPage), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
}
