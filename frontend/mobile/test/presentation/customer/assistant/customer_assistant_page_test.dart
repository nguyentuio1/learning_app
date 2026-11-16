import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:smart_drone_delivery/core/error/api_exception.dart';
import 'package:smart_drone_delivery/data/models/ai_assistant_models.dart';
import 'package:smart_drone_delivery/data/providers/ai_assistant_providers.dart';
import 'package:smart_drone_delivery/data/services/ai_assistant_api_service.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/ai_assistant_controller.dart';
import 'package:smart_drone_delivery/presentation/customer/assistant/customer_assistant_page.dart';

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
}) async {
  tester.view.physicalSize = const Size(800, 1600);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  await tester.pumpWidget(
    ProviderScope(
      overrides: [aiAssistantApiServiceProvider.overrideWithValue(service)],
      child: MaterialApp(
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
}
