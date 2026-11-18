import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/core/network/logging_interceptor.dart';
import 'package:smart_drone_delivery/core/session/auth_controller.dart';
import 'package:smart_drone_delivery/domain/entities/user.dart';
import 'package:go_router/go_router.dart';
import 'package:smart_drone_delivery/core/config/app_config.dart';
import 'package:smart_drone_delivery/core/theme/app_theme.dart';
import 'package:smart_drone_delivery/data/models/ai_assistant_models.dart';
import 'package:smart_drone_delivery/data/providers/ai_assistant_providers.dart';
import 'package:smart_drone_delivery/data/services/ai_assistant_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/ai_assistant_controller.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/customer_assistant_page.dart';

import '../../../support/fake_http_adapter.dart';

const _sessionId1 = '83ec4120-6388-426f-80a7-c8b627998d32';
const _orderId1 = 'e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33';

class _FakeAiAssistantApiService extends AiAssistantApiService {
  _FakeAiAssistantApiService() : super(Dio());

  List<AiAssistantSession> sessions = [
    AiAssistantSession(
      id: _sessionId1,
      sessionTitle: 'Order DD-202610-0985',
      createdAt: DateTime.utc(2026, 11, 16, 8, 10),
    ),
  ];

  Map<String, List<AiAssistantMessage>> messagesBySession = {
    _sessionId1: [
      AiAssistantMessage(
        id: '9007199254740993',
        senderType: 'USER',
        messageText: 'When will it arrive?',
        createdAt: DateTime.utc(2026, 11, 16, 8, 14),
      ),
      AiAssistantMessage(
        id: '9007199254740994',
        senderType: 'BOT',
        messageText: 'DD-202610-0985 is in transit.',
        createdAt: DateTime.utc(2026, 11, 16, 8, 15),
      ),
    ],
  };

  Object? sessionsError;
  Object? messagesError;
  Object? sendError;
  AiAssistantSendResponse? nextSendResponse;
  Completer<AiAssistantSendResponse>? pendingSendCompleter;
  int sendCallCount = 0;
  int getSessionsCallCount = 0;
  int getMessagesCallCount = 0;
  AiAssistantSendRequest? lastSendRequest;

  @override
  Future<AiAssistantSessionsPage> getSessions({
    int page = 1,
    int pageSize = AiAssistantApiService.defaultPageSize,
  }) async {
    getSessionsCallCount++;
    if (sessionsError != null) throw sessionsError!;
    return AiAssistantSessionsPage(
      items: sessions,
      page: page,
      pageSize: pageSize,
      totalCount: sessions.length,
    );
  }

  @override
  Future<AiAssistantMessagesPage> getSessionMessages(
    String sessionId, {
    int page = 1,
    int pageSize = AiAssistantApiService.defaultPageSize,
  }) async {
    getMessagesCallCount++;
    if (messagesError != null) throw messagesError!;
    final items = messagesBySession[sessionId] ?? const [];
    return AiAssistantMessagesPage(
      sessionId: sessionId,
      items: items,
      page: page,
      pageSize: pageSize,
      totalCount: items.length,
    );
  }

  @override
  Future<AiAssistantSendResponse> sendMessage(
    AiAssistantSendRequest request,
  ) async {
    sendCallCount++;
    lastSendRequest = request;
    if (pendingSendCompleter != null) {
      return pendingSendCompleter!.future;
    }
    if (sendError != null) throw sendError!;
    return nextSendResponse ??
        AiAssistantSendResponse(
          outcome: AiAssistantOutcome.answered,
          sessionId: request.sessionId ?? _sessionId1,
          userMessageId: '1201',
          reply: AiAssistantMessage(
            id: '1202',
            senderType: 'BOT',
            messageText: 'Your order is in transit and expected at 14:48.',
            createdAt: DateTime.utc(2026, 11, 16, 8, 16),
            outcome: AiAssistantOutcome.answered,
          ),
        );
  }
}

Future<void> _pumpAssistantPage(
  WidgetTester tester,
  _FakeAiAssistantApiService service, {
  String? orderId = _orderId1,
  String? trackingNumber = 'DD-202610-0985',
  String? sessionId,
  bool? chatEnabled = true,
  bool productionTheme = false,
}) async {
  tester.view.physicalSize = const Size(800, 1600);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        aiAssistantApiServiceProvider.overrideWithValue(service),
        if (chatEnabled != null)
          aiAssistantChatEnabledProvider.overrideWithValue(chatEnabled),
      ],
      child: MaterialApp(
        theme: productionTheme ? AppTheme.light(AppFlavor.customer) : null,
        home: CustomerAssistantPage(
          orderId: orderId,
          trackingNumber: trackingNumber,
          sessionId: sessionId,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('AiAssistantModels and Service Contract (LA-42 / LA-167)', () {
    test('parses ANSWERED and transient outcomes strictly per contract', () {
      final answered = AiAssistantSendResponse.fromJson({
        'outcome': 'ANSWERED',
        'sessionId': _sessionId1,
        'userMessageId': '9007199254740995',
        'reply': {
          'id': '9007199254740996',
          'senderType': 'BOT',
          'messageText': 'The order is in transit.',
          'createdAt': '2026-11-16T08:15:00Z',
        },
      });
      expect(answered.outcome, AiAssistantOutcome.answered);
      expect(answered.sessionId, _sessionId1);
      expect(answered.userMessageId, '9007199254740995');
      expect(answered.reply.id, '9007199254740996');

      final refused = AiAssistantSendResponse.fromJson({
        'outcome': 'REFUSED',
        'sessionId': null,
        'userMessageId': null,
        'reply': {
          'id': null,
          'senderType': 'BOT',
          'messageText': 'I can only answer questions about your own orders.',
          'createdAt': '2026-11-16T08:16:00Z',
        },
      });
      expect(refused.outcome, AiAssistantOutcome.refused);
      expect(refused.sessionId, isNull);
      expect(refused.reply.id, isNull);

      expect(
        () => AiAssistantSendResponse.fromJson({
          'outcome': 'REFUSED',
          'sessionId': _sessionId1,
          'userMessageId': null,
          'reply': {
            'id': null,
            'senderType': 'BOT',
            'messageText': 'Refused',
            'createdAt': '2026-11-16T08:16:00Z',
          },
        }),
        throwsFormatException,
      );
    });

    test('AiAssistantApiService rejects empty or >2000 rune messages before API call', () async {
      final dio = Dio();
      final service = AiAssistantApiService(dio);

      expect(
        () => service.sendMessage(
          const AiAssistantSendRequest(message: '   '),
        ),
        throwsA(isA<ApiException>()),
      );

      final tooLong = 'a' * 2001;
      expect(
        () => service.sendMessage(
          AiAssistantSendRequest(message: tooLong),
        ),
        throwsA(isA<ApiException>()),
      );
    });
  });

  group('CustomerAssistantPage and AiAssistantController (LA-42 / LA-167)', () {
    testWidgets(
      'displays order context, sends message via suggestion chip, and updates active session on ANSWERED',
      (tester) async {
        final service = _FakeAiAssistantApiService();
        await _pumpAssistantPage(tester, service);

        expect(find.byKey(const Key('assistant-order-chip')), findsOneWidget);
        expect(find.text('DD-202610-0985'), findsOneWidget);
        expect(find.text('Where is my order?'), findsOneWidget);

        await tester.tap(find.text('Where is my order?'));
        await tester.pumpAndSettle();

        expect(service.sendCallCount, 1);
        expect(service.lastSendRequest?.orderId, _orderId1);
        expect(service.lastSendRequest?.message, 'Where is my order?');
        expect(
          find.text('Your order is in transit and expected at 14:48.'),
          findsOneWidget,
        );

        final container = ProviderScope.containerOf(
          tester.element(find.byType(CustomerAssistantPage)),
        );
        final state = container.read(
          aiAssistantControllerProvider(
            const AiAssistantContext(
              orderId: _orderId1,
              trackingNumber: 'DD-202610-0985',
            ),
          ),
        );
        expect(state.activeSessionId, _sessionId1);
      },
    );

    testWidgets(
      'handles REFUSED / CLARIFICATION_REQUIRED without mutating activeSessionId',
      (tester) async {
        final service = _FakeAiAssistantApiService()
          ..nextSendResponse = AiAssistantSendResponse(
            outcome: AiAssistantOutcome.refused,
            sessionId: null,
            userMessageId: null,
            reply: AiAssistantMessage(
              id: null,
              senderType: 'BOT',
              messageText:
                  'I can only answer questions about your own orders.',
              createdAt: DateTime.utc(2026, 11, 16, 8, 17),
              outcome: AiAssistantOutcome.refused,
            ),
          );
        await _pumpAssistantPage(tester, service);

        await tester.enterText(
          find.byKey(const Key('assistant-message-input')),
          'What about order DD-202610-0555?',
        );
        await tester.pump();
        await tester.tap(find.byKey(const Key('assistant-send-button')));
        await tester.pumpAndSettle();

        expect(
          find.text('I can only answer questions about your own orders.'),
          findsOneWidget,
        );
        expect(find.text('Unavailable'), findsOneWidget);

        final container = ProviderScope.containerOf(
          tester.element(find.byType(CustomerAssistantPage)),
        );
        final state = container.read(
          aiAssistantControllerProvider(
            const AiAssistantContext(
              orderId: _orderId1,
              trackingNumber: 'DD-202610-0985',
            ),
          ),
        );
        expect(state.activeSessionId, isNull);
      },
    );

    testWidgets(
      'does not auto-retry uncertain POST failure and offers history/session recovery',
      (tester) async {
        final service = _FakeAiAssistantApiService()
          ..sendError = const ApiException(
            message: 'Connection timed out.',
            isNetwork: true,
          );
        await _pumpAssistantPage(
          tester,
          service,
          sessionId: _sessionId1,
        );

        final initialMessageCalls = service.getMessagesCallCount;
        await tester.enterText(
          find.byKey(const Key('assistant-message-input')),
          'Any update?',
        );
        await tester.pump();
        await tester.tap(find.byKey(const Key('assistant-send-button')));
        await tester.pumpAndSettle();

        // Must NOT auto-retry POST
        expect(service.sendCallCount, 1);
        expect(find.byKey(const Key('assistant-error-banner')), findsOneWidget);
        expect(find.byKey(const Key('assistant-recover-button')), findsOneWidget);
        expect(find.text('Reload history'), findsOneWidget);

        await tester.tap(find.byKey(const Key('assistant-recover-button')));
        await tester.pumpAndSettle();

        expect(service.getMessagesCallCount, greaterThan(initialMessageCalls));
        expect(find.byKey(const Key('assistant-error-banner')), findsNothing);
      },
    );

    testWidgets(
      'deduplicates persisted messages by id on refresh and clears protected state on 403/404',
      (tester) async {
        final service = _FakeAiAssistantApiService();
        await _pumpAssistantPage(
          tester,
          service,
          sessionId: _sessionId1,
        );

        expect(find.text('When will it arrive?'), findsOneWidget);
        expect(find.text('DD-202610-0985 is in transit.'), findsOneWidget);

        // Refreshing the same session with overlapping IDs must deduplicate
        await tester.tap(find.byKey(const Key('assistant-refresh-button')));
        await tester.pumpAndSettle();
        expect(find.text('When will it arrive?'), findsOneWidget);
        expect(find.text('DD-202610-0985 is in transit.'), findsOneWidget);

        // Access loss (403) clears protected messages and shows access-lost view
        service.messagesError = const ApiException(
          message: 'An active customer account is required.',
          statusCode: 403,
        );
        await tester.tap(find.byKey(const Key('assistant-refresh-button')));
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('assistant-access-lost-view')),
          findsOneWidget,
        );
        expect(find.text('When will it arrive?'), findsNothing);
        expect(find.text('DD-202610-0985 is in transit.'), findsNothing);
        expect(
          find.text('An active customer account is required.'),
          findsOneWidget,
        );
      },
    );
  });

  group('LA-167 review regressions', () {
    testWidgets('composer fits beside Send under the production full-width button theme', (tester) async {
      await _pumpAssistantPage(tester, _FakeAiAssistantApiService(), productionTheme: true);
      expect(tester.takeException(), isNull);
      expect(tester.getSize(find.byKey(const Key('assistant-message-input'))).width,
          greaterThan(200));
      expect(tester.getSize(find.byKey(const Key('assistant-send-button'))).width,
          lessThanOrEqualTo(80));
    });

    testWidgets(
      'default-off chat disables input, suggestions and direct sends without losing history',
      (tester) async {
        final service = _FakeAiAssistantApiService();
        await _pumpAssistantPage(
          tester,
          service,
          chatEnabled: null,
          sessionId: _sessionId1,
        );
        expect(
          tester
              .widget<TextField>(
                find.byKey(const Key('assistant-message-input')),
              )
              .enabled,
          isFalse,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('assistant-send-button')),
              )
              .onPressed,
          isNull,
        );
        for (final chip in tester.widgetList<ActionChip>(
          find.byType(ActionChip),
        )) {
          expect(chip.onPressed, isNull);
        }
        expect(
          find.byKey(const Key('assistant-unavailable-notice')),
          findsOneWidget,
        );
        expect(find.text('When will it arrive?'), findsOneWidget);
        final container = ProviderScope.containerOf(
          tester.element(find.byType(CustomerAssistantPage)),
        );
        final notifier = container.read(
          aiAssistantControllerProvider(
            const AiAssistantContext(
              orderId: _orderId1,
              trackingNumber: 'DD-202610-0985',
              sessionId: _sessionId1,
            ),
          ).notifier,
        );
        expect(await notifier.sendMessage('Do not send'), isFalse);
        expect(service.sendCallCount, 0);
      },
    );

    testWidgets('disabled assistant provides a working tracking action', (
      tester,
    ) async {
      final router = GoRouter(
        initialLocation: '/assistant',
        routes: [
          GoRoute(
            path: '/assistant',
            builder: (_, _) => const CustomerAssistantPage(orderId: _orderId1),
          ),
          GoRoute(
            path: '/customer/orders/tracking/:orderId',
            builder: (_, state) => Scaffold(
              body: Text('Tracking ${state.pathParameters['orderId']}'),
            ),
          ),
        ],
      );
      addTearDown(router.dispose);
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            aiAssistantApiServiceProvider.overrideWithValue(
              _FakeAiAssistantApiService(),
            ),
            aiAssistantChatEnabledProvider.overrideWithValue(false),
          ],
          child: MaterialApp.router(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('assistant-track-order-button')));
      await tester.pumpAndSettle();
      expect(find.text('Tracking $_orderId1'), findsOneWidget);
    });

    testWidgets(
      '504 still requires history recovery and never retries automatically',
      (tester) async {
        final service = _FakeAiAssistantApiService()
          ..sendError = const ApiException(
            message: 'Gateway timeout',
            statusCode: 504,
          );
        await _pumpAssistantPage(tester, service);
        await tester.tap(find.text('Where is my order?'));
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('assistant-recover-button')),
          findsOneWidget,
        );
        expect(service.sendCallCount, 1);
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('assistant-send-button')),
              )
              .onPressed,
          isNull,
        );
      },
    );

    test(
      'sign-out clears protected history and discards an in-flight reply',
      () async {
        final service = _FakeAiAssistantApiService();
        final pending = Completer<AiAssistantSendResponse>();
        service.pendingSendCompleter = pending;
        final container = ProviderContainer(
          overrides: [
            aiAssistantApiServiceProvider.overrideWithValue(service),
            aiAssistantChatEnabledProvider.overrideWithValue(true),
          ],
        );
        addTearDown(container.dispose);
        container
            .read(currentUserProvider.notifier)
            .setUser(
              User(
                id: 'customer-a',
                email: 'test@example.invalid',
                fullName: 'Test',
                phoneNumber: '',
                status: 'ACTIVE',
                roleCode: 'CUSTOMER',
                createdAt: DateTime.utc(2026),
              ),
            );
        const ctx = AiAssistantContext(sessionId: _sessionId1);
        final sub = container.listen(
          aiAssistantControllerProvider(ctx),
          (_, _) {},
        );
        addTearDown(sub.close);
        final notifier = container.read(
          aiAssistantControllerProvider(ctx).notifier,
        );
        await Future<void>.delayed(Duration.zero);
        expect(
          container.read(aiAssistantControllerProvider(ctx)).messages,
          isNotEmpty,
        );
        final sending = notifier.sendMessage('Pending question');
        container.read(currentUserProvider.notifier).setUser(null);
        expect(
          container.read(aiAssistantControllerProvider(ctx)).messages,
          isEmpty,
        );
        pending.complete(
          AiAssistantSendResponse(
            outcome: AiAssistantOutcome.answered,
            sessionId: _sessionId1,
            userMessageId: 'u-late',
            reply: AiAssistantMessage(
              id: 'b-late',
              senderType: 'BOT',
              messageText: 'Late reply',
              createdAt: DateTime.utc(2026),
            ),
          ),
        );
        expect(await sending, isFalse);
        final state = container.read(aiAssistantControllerProvider(ctx));
        expect(state.accessLost, isTrue);
        expect(state.messages, isEmpty);
        expect(state.activeSessionId, isNull);
      },
    );

    testWidgets(
      '503 keeps the draft and permits a deliberate retry without history recovery',
      (tester) async {
        final service = _FakeAiAssistantApiService()
          ..sendError = const ApiException(
            message: 'The assistant is temporarily unavailable.',
            statusCode: 503,
          );
        await _pumpAssistantPage(tester, service);
        await tester.enterText(
          find.byKey(const Key('assistant-message-input')),
          'Any update?',
        );
        await tester.pump();
        await tester.tap(find.byKey(const Key('assistant-send-button')));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('assistant-recover-button')), findsNothing);
        expect(
          find.text('The assistant is temporarily unavailable.'),
          findsOneWidget,
        );
        expect(
          tester
              .widget<TextField>(
                find.byKey(const Key('assistant-message-input')),
              )
              .controller!
              .text,
          'Any update?',
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('assistant-send-button')),
              )
              .onPressed,
          isNotNull,
        );
        expect(service.sendCallCount, 1);
        service.sendError = null;
        await tester.tap(find.byKey(const Key('assistant-send-button')));
        await tester.pumpAndSettle();
        expect(service.sendCallCount, 2);
        expect(
          find.text('Your order is in transit and expected at 14:48.'),
          findsOneWidget,
        );
      },
    );

    const sessionId2 = '94fd5231-7499-437a-91b8-d9c738009e43';

    test('ignores stale POST completion after switching sessions', () async {
      final service = _FakeAiAssistantApiService();
      service.messagesBySession[sessionId2] = [
        AiAssistantMessage(
          id: 'm-b-1',
          senderType: 'BOT',
          messageText: 'Session B message',
          createdAt: DateTime.utc(2026, 11, 16, 9),
        ),
      ];
      final completer = Completer<AiAssistantSendResponse>();
      service.pendingSendCompleter = completer;

      final container = ProviderContainer(
        overrides: [
        aiAssistantApiServiceProvider.overrideWithValue(service),
        aiAssistantChatEnabledProvider.overrideWithValue(true),
      ],
      );
      addTearDown(container.dispose);

      const ctx = AiAssistantContext(sessionId: _sessionId1);
      final sub = container.listen(aiAssistantControllerProvider(ctx), (_, _) {});
      addTearDown(sub.close);
      final notifier = container.read(aiAssistantControllerProvider(ctx).notifier);
      await Future<void>.delayed(Duration.zero);

      final sendFuture = notifier.sendMessage('Question for A');
      await notifier.selectSession(sessionId2, title: 'Session B');

      completer.complete(
        AiAssistantSendResponse(
          outcome: AiAssistantOutcome.answered,
          sessionId: _sessionId1,
          userMessageId: 'm-a-u',
          reply: AiAssistantMessage(
            id: 'm-a-b',
            senderType: 'BOT',
            messageText: 'Late reply from A',
            createdAt: DateTime.utc(2026, 11, 16, 9, 5),
          ),
        ),
      );
      final result = await sendFuture;
      expect(result, isFalse);

      final state = container.read(aiAssistantControllerProvider(ctx));
      expect(state.activeSessionId, sessionId2);
      expect(state.messages.map((m) => m.messageText), ['Session B message']);
      expect(state.isSending, isFalse);
    });

    test('ignores stale POST completion after starting New chat', () async {
      final service = _FakeAiAssistantApiService();
      final completer = Completer<AiAssistantSendResponse>();
      service.pendingSendCompleter = completer;

      final container = ProviderContainer(
        overrides: [
        aiAssistantApiServiceProvider.overrideWithValue(service),
        aiAssistantChatEnabledProvider.overrideWithValue(true),
      ],
      );
      addTearDown(container.dispose);

      const ctx = AiAssistantContext(sessionId: _sessionId1);
      final sub = container.listen(aiAssistantControllerProvider(ctx), (_, _) {});
      addTearDown(sub.close);
      final notifier = container.read(aiAssistantControllerProvider(ctx).notifier);
      await Future<void>.delayed(Duration.zero);

      final sendFuture = notifier.sendMessage('Question for A');
      notifier.startNewConversation();

      completer.complete(
        AiAssistantSendResponse(
          outcome: AiAssistantOutcome.answered,
          sessionId: _sessionId1,
          userMessageId: 'm-a-u',
          reply: AiAssistantMessage(
            id: 'm-a-b',
            senderType: 'BOT',
            messageText: 'Late reply from A',
            createdAt: DateTime.utc(2026, 11, 16, 9, 5),
          ),
        ),
      );
      expect(await sendFuture, isFalse);

      final state = container.read(aiAssistantControllerProvider(ctx));
      expect(state.activeSessionId, isNull);
      expect(state.messages, isEmpty);
      expect(state.isSending, isFalse);
    });

    test('ignores stale POST completion after concurrent 403 accessLost',
        () async {
      final service = _FakeAiAssistantApiService();
      final completer = Completer<AiAssistantSendResponse>();
      service.pendingSendCompleter = completer;

      final container = ProviderContainer(
        overrides: [
        aiAssistantApiServiceProvider.overrideWithValue(service),
        aiAssistantChatEnabledProvider.overrideWithValue(true),
      ],
      );
      addTearDown(container.dispose);

      const ctx = AiAssistantContext(sessionId: _sessionId1);
      final sub = container.listen(aiAssistantControllerProvider(ctx), (_, _) {});
      addTearDown(sub.close);
      final notifier = container.read(aiAssistantControllerProvider(ctx).notifier);
      await Future<void>.delayed(Duration.zero);

      final sendFuture = notifier.sendMessage('Question before 403');
      service.messagesError = const ApiException(
        message: 'Forbidden',
        statusCode: 403,
      );
      await notifier.loadMessages(_sessionId1);
      expect(container.read(aiAssistantControllerProvider(ctx)).accessLost,
          isTrue);

      completer.complete(
        AiAssistantSendResponse(
          outcome: AiAssistantOutcome.answered,
          sessionId: _sessionId1,
          userMessageId: 'm-a-u',
          reply: AiAssistantMessage(
            id: 'm-a-b',
            senderType: 'BOT',
            messageText: 'Should be ignored',
            createdAt: DateTime.utc(2026, 11, 16, 9, 5),
          ),
        ),
      );
      expect(await sendFuture, isFalse);

      final state = container.read(aiAssistantControllerProvider(ctx));
      expect(state.accessLost, isTrue);
      expect(state.messages, isEmpty);
    });

    testWidgets(
        'marks malformed 2xx POST as uncertain and blocks sends until recovery',
        (tester) async {
      final service = _FakeAiAssistantApiService()
        ..sendError = const FormatException('Malformed 2xx body');

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
        aiAssistantApiServiceProvider.overrideWithValue(service),
        aiAssistantChatEnabledProvider.overrideWithValue(true),
      ],
          child: const MaterialApp(
            home: CustomerAssistantPage(sessionId: _sessionId1),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('assistant-message-input')),
        'First question',
      );
      await tester.pump();
      await tester.tap(find.byKey(const Key('assistant-send-button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('assistant-recover-button')), findsOneWidget);
      expect(service.sendCallCount, 1);

      // Send button & suggestion chips must be disabled while recovery is pending
      final sendBtn = tester.widget<FilledButton>(
        find.byKey(const Key('assistant-send-button')),
      );
      expect(sendBtn.onPressed, isNull);
      final chip = tester.widget<ActionChip>(find.byType(ActionChip).first);
      expect(chip.onPressed, isNull);

      // Controller must also reject a direct second send before recovery
      final element = tester.element(find.byType(CustomerAssistantPage));
      final container = ProviderScope.containerOf(element);
      final notifier = container.read(
        aiAssistantControllerProvider(
          const AiAssistantContext(sessionId: _sessionId1),
        ).notifier,
      );
      expect(await notifier.sendMessage('Second question'), isFalse);
      expect(service.sendCallCount, 1);

      // Recover via Reload history -> clears uncertain flag and re-enables chips
      await tester.tap(find.byKey(const Key('assistant-recover-button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('assistant-recover-button')), findsNothing);
      final chipAfter = tester.widget<ActionChip>(
        find.byType(ActionChip).first,
      );
      expect(chipAfter.onPressed, isNotNull);
    });

    test('suppresses request/response/error bodies in shared AppLogInterceptor',
        () async {
      final logs = <String>[];
      final adapter = FakeHttpAdapter((options) {
        final path = options.uri.path;
        if (options.method == 'POST' && path.endsWith('/ai-assistant/messages')) {
          return jsonBody({
            'outcome': 'ANSWERED',
            'sessionId': _sessionId1,
            'userMessageId': 'u1',
            'reply': {
              'id': 'b1',
              'senderType': 'BOT',
              'messageText': 'SECRET-REPLY-ADDRESS-999',
              'createdAt': '2026-11-16T09:00:00Z',
            },
          });
        }
        if (options.method == 'GET' && path.endsWith('/ai-assistant/sessions')) {
          return jsonBody({
            'items': [
              {
                'id': _sessionId1,
                'sessionTitle': 'SECRET-TITLE-888',
                'createdAt': '2026-11-16T08:00:00Z',
              },
            ],
            'page': 1,
            'pageSize': 20,
            'totalCount': 1,
          });
        }
        if (path.contains('/messages')) {
          return jsonBody({
            'sessionId': _sessionId1,
            'items': [
              {
                'id': 'u1',
                'senderType': 'USER',
                'messageText': 'SECRET-HISTORY-CODE-777',
                'createdAt': '2026-11-16T08:05:00Z',
              },
            ],
            'page': 1,
            'pageSize': 20,
            'totalCount': 1,
          });
        }
        return jsonBody({'detail': 'unexpected'}, 400);
      });

      final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api'))
        ..httpClientAdapter = adapter
        ..interceptors.add(AppLogInterceptor(logger: logs.add));

      final api = AiAssistantApiService(dio);
      await api.sendMessage(
        const AiAssistantSendRequest(
          sessionId: _sessionId1,
          message: 'My pickup code is SECRET-INPUT-123456',
        ),
      );
      await api.getSessions();
      await api.getSessionMessages(_sessionId1);

      final combined = logs.join('\n');
      expect(combined, isNotEmpty);
      expect(combined, isNot(contains('SECRET-INPUT-123456')));
      expect(combined, isNot(contains('SECRET-REPLY-ADDRESS-999')));
      expect(combined, isNot(contains('SECRET-TITLE-888')));
      expect(combined, isNot(contains('SECRET-HISTORY-CODE-777')));
    });
  });
}
