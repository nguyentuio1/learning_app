import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/error/api_exception.dart';
import '../../../data/models/ai_assistant_models.dart';
import '../../../data/providers/ai_assistant_providers.dart';
import '../../../data/services/ai_assistant_api_service.dart';

class AiAssistantContext {
  const AiAssistantContext({
    this.orderId,
    this.trackingNumber,
    this.sessionId,
  });

  final String? orderId;
  final String? trackingNumber;
  final String? sessionId;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AiAssistantContext &&
          runtimeType == other.runtimeType &&
          orderId == other.orderId &&
          trackingNumber == other.trackingNumber &&
          sessionId == other.sessionId;

  @override
  int get hashCode => Object.hash(orderId, trackingNumber, sessionId);
}

class AiAssistantState {
  const AiAssistantState({
    this.sessions = const [],
    this.sessionsPage = 1,
    this.sessionsTotalCount = 0,
    this.hasMoreSessions = false,
    this.isLoadingSessions = false,
    this.activeSessionId,
    this.activeSessionTitle,
    this.selectedOrderId,
    this.selectedTrackingNumber,
    this.messages = const [],
    this.messagesPage = 1,
    this.messagesTotalCount = 0,
    this.hasMoreMessages = false,
    this.isLoadingMessages = false,
    this.isSending = false,
    this.errorMessage,
    this.uncertainSendPendingRecovery = false,
    this.accessLost = false,
  });

  final List<AiAssistantSession> sessions;
  final int sessionsPage;
  final int sessionsTotalCount;
  final bool hasMoreSessions;
  final bool isLoadingSessions;
  final String? activeSessionId;
  final String? activeSessionTitle;
  final String? selectedOrderId;
  final String? selectedTrackingNumber;
  final List<AiAssistantMessage> messages;
  final int messagesPage;
  final int messagesTotalCount;
  final bool hasMoreMessages;
  final bool isLoadingMessages;
  final bool isSending;
  final String? errorMessage;
  final bool uncertainSendPendingRecovery;
  final bool accessLost;

  AiAssistantState copyWith({
    List<AiAssistantSession>? sessions,
    int? sessionsPage,
    int? sessionsTotalCount,
    bool? hasMoreSessions,
    bool? isLoadingSessions,
    String? activeSessionId,
    bool clearActiveSession = false,
    String? activeSessionTitle,
    bool clearActiveSessionTitle = false,
    String? selectedOrderId,
    String? selectedTrackingNumber,
    bool clearSelectedOrder = false,
    List<AiAssistantMessage>? messages,
    int? messagesPage,
    int? messagesTotalCount,
    bool? hasMoreMessages,
    bool? isLoadingMessages,
    bool? isSending,
    String? errorMessage,
    bool clearError = false,
    bool? uncertainSendPendingRecovery,
    bool? accessLost,
  }) {
    return AiAssistantState(
      sessions: sessions ?? this.sessions,
      sessionsPage: sessionsPage ?? this.sessionsPage,
      sessionsTotalCount: sessionsTotalCount ?? this.sessionsTotalCount,
      hasMoreSessions: hasMoreSessions ?? this.hasMoreSessions,
      isLoadingSessions: isLoadingSessions ?? this.isLoadingSessions,
      activeSessionId: clearActiveSession
          ? null
          : (activeSessionId ?? this.activeSessionId),
      activeSessionTitle: (clearActiveSession || clearActiveSessionTitle)
          ? null
          : (activeSessionTitle ?? this.activeSessionTitle),
      selectedOrderId: clearSelectedOrder
          ? null
          : (selectedOrderId ?? this.selectedOrderId),
      selectedTrackingNumber: clearSelectedOrder
          ? null
          : (selectedTrackingNumber ?? this.selectedTrackingNumber),
      messages: messages ?? this.messages,
      messagesPage: messagesPage ?? this.messagesPage,
      messagesTotalCount: messagesTotalCount ?? this.messagesTotalCount,
      hasMoreMessages: hasMoreMessages ?? this.hasMoreMessages,
      isLoadingMessages: isLoadingMessages ?? this.isLoadingMessages,
      isSending: isSending ?? this.isSending,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      uncertainSendPendingRecovery:
          uncertainSendPendingRecovery ?? this.uncertainSendPendingRecovery,
      accessLost: accessLost ?? this.accessLost,
    );
  }
}

class AiAssistantController extends Notifier<AiAssistantState> {
  AiAssistantController(this.initialContext);

  final AiAssistantContext initialContext;
  int _sessionsGeneration = 0;
  int _messagesGeneration = 0;
  int _sendGeneration = 0;

  @override
  AiAssistantState build() {
    final initialState = AiAssistantState(
      selectedOrderId: _nullIfEmpty(initialContext.orderId),
      selectedTrackingNumber: _nullIfEmpty(initialContext.trackingNumber),
      activeSessionId: _nullIfEmpty(initialContext.sessionId),
    );
    Future.microtask(() async {
      if (!ref.mounted) return;
      await loadSessions();
      if (!ref.mounted) return;
      final sessionId = _nullIfEmpty(initialContext.sessionId);
      if (sessionId != null) {
        await loadMessages(sessionId);
      }
    });
    return initialState;
  }

  static String? _nullIfEmpty(String? value) {
    if (value == null) return null;
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  static bool isAccessDeniedError(Object error) {
    if (error is ApiException) {
      return error.statusCode == 401 ||
          error.statusCode == 403 ||
          error.statusCode == 404;
    }
    return false;
  }

  static String accessDeniedMessage(Object error) {
    if (error is ApiException) {
      if (error.statusCode == 404) {
        return 'Conversation or order not found or unavailable to your account.';
      }
      if (error.message.isNotEmpty) {
        return error.message;
      }
    }
    return 'You no longer have access to the AI assistant.';
  }

  void _handleAccessLost(Object error) {
    _sessionsGeneration++;
    _messagesGeneration++;
    _sendGeneration++;
    state = AiAssistantState(
      accessLost: true,
      errorMessage: accessDeniedMessage(error),
    );
  }

  Future<bool> loadSessions({int page = 1, bool append = false}) async {
    if (state.accessLost) return false;
    final generation = ++_sessionsGeneration;
    state = state.copyWith(isLoadingSessions: true, clearError: true);

    try {
      final result = await ref
          .read(aiAssistantApiServiceProvider)
          .getSessions(page: page);
      if (!ref.mounted || generation != _sessionsGeneration) return false;

      final merged = append
          ? _mergeSessions(state.sessions, result.items)
          : result.items;

      state = state.copyWith(
        sessions: List.unmodifiable(merged),
        sessionsPage: result.page,
        sessionsTotalCount: result.totalCount,
        hasMoreSessions: result.hasMore,
        isLoadingSessions: false,
      );
      return true;
    } catch (error) {
      if (!ref.mounted || generation != _sessionsGeneration) return false;
      if (isAccessDeniedError(error)) {
        _handleAccessLost(error);
        return false;
      }
      state = state.copyWith(
        isLoadingSessions: false,
        errorMessage: error is ApiException && error.message.isNotEmpty
            ? error.message
            : 'Unable to load conversations. Please try again.',
      );
      return false;
    }
  }

  Future<bool> selectSession(String sessionId, {String? title}) async {
    if (state.accessLost) return false;
    _sendGeneration++;
    state = state.copyWith(
      activeSessionId: sessionId,
      activeSessionTitle: title,
      clearActiveSessionTitle: title == null,
      messages: const [],
      messagesPage: 1,
      messagesTotalCount: 0,
      hasMoreMessages: false,
      isSending: false,
      clearError: true,
      uncertainSendPendingRecovery: false,
    );
    return loadMessages(sessionId);
  }

  Future<bool> loadMessages(
    String sessionId, {
    int? page,
    bool prependOlder = false,
  }) async {
    if (state.accessLost) return false;
    final generation = ++_messagesGeneration;
    state = state.copyWith(isLoadingMessages: true, clearError: true);

    try {
      final api = ref.read(aiAssistantApiServiceProvider);
      var result = await api.getSessionMessages(
        sessionId,
        page: page ?? 1,
      );
      if (!ref.mounted || generation != _messagesGeneration) return false;

      // On initial load (page == null), jump to the newest page if more than 1 page exists.
      if (page == null && result.totalCount > result.pageSize) {
        final lastPage = (result.totalCount / result.pageSize).ceil();
        if (lastPage > 1) {
          result = await api.getSessionMessages(sessionId, page: lastPage);
          if (!ref.mounted || generation != _messagesGeneration) return false;
        }
      }

      final combined = prependOlder
          ? _mergeMessages(result.items, state.messages)
          : _mergeMessages(
              // Preserve any transient local messages only if refreshing same session
              state.activeSessionId == sessionId
                  ? state.messages.where((m) => !m.isPersisted).toList()
                  : const [],
              result.items,
            );

      state = state.copyWith(
        activeSessionId: result.sessionId,
        messages: List.unmodifiable(combined),
        messagesPage: result.page,
        messagesTotalCount: result.totalCount,
        hasMoreMessages: prependOlder
            ? result.page > 1
            : (result.page > 1 || result.hasMore),
        isLoadingMessages: false,
        uncertainSendPendingRecovery: false,
      );
      return true;
    } catch (error) {
      if (!ref.mounted || generation != _messagesGeneration) return false;
      if (isAccessDeniedError(error)) {
        _handleAccessLost(error);
        return false;
      }
      state = state.copyWith(
        isLoadingMessages: false,
        errorMessage: error is ApiException && error.message.isNotEmpty
            ? error.message
            : 'Unable to load conversation messages. Please try again.',
      );
      return false;
    }
  }

  void startNewConversation() {
    if (state.accessLost) return;
    _messagesGeneration++;
    _sendGeneration++;
    state = state.copyWith(
      clearActiveSession: true,
      messages: const [],
      messagesPage: 1,
      messagesTotalCount: 0,
      hasMoreMessages: false,
      isLoadingMessages: false,
      isSending: false,
      clearError: true,
      uncertainSendPendingRecovery: false,
    );
  }

  void clearOrderContext() {
    if (state.accessLost) return;
    state = state.copyWith(clearSelectedOrder: true);
  }

  void setOrderContext({String? orderId, String? trackingNumber}) {
    if (state.accessLost) return;
    final cleanOrderId = _nullIfEmpty(orderId);
    if (cleanOrderId == null) {
      clearOrderContext();
      return;
    }
    state = state.copyWith(
      selectedOrderId: cleanOrderId,
      selectedTrackingNumber: _nullIfEmpty(trackingNumber),
    );
  }

  Future<bool> sendMessage(String rawText) async {
    if (state.accessLost ||
        state.isSending ||
        state.uncertainSendPendingRecovery) {
      return false;
    }

    final trimmed = rawText.trim();
    final runeCount = trimmed.runes.length;
    if (runeCount == 0) {
      state = state.copyWith(errorMessage: 'Please enter a message.');
      return false;
    }
    if (runeCount > AiAssistantApiService.maxMessageRunes) {
      state = state.copyWith(
        errorMessage: 'Message must be at most 2,000 characters.',
      );
      return false;
    }

    final sendGeneration = ++_sendGeneration;
    state = state.copyWith(
      isSending: true,
      clearError: true,
      uncertainSendPendingRecovery: false,
    );

    final request = AiAssistantSendRequest(
      sessionId: state.activeSessionId,
      orderId: state.selectedOrderId,
      message: trimmed,
    );

    try {
      final response = await ref
          .read(aiAssistantApiServiceProvider)
          .sendMessage(request);
      if (!ref.mounted || sendGeneration != _sendGeneration) return false;

      if (response.outcome == AiAssistantOutcome.answered) {
        final userMsg = AiAssistantMessage(
          id: response.userMessageId,
          senderType: 'USER',
          messageText: trimmed,
          createdAt: response.reply.createdAt,
        );
        final updatedMessages = _mergeMessages(
          state.messages,
          [userMsg, response.reply],
        );
        final isNewSession =
            state.activeSessionId == null && response.sessionId != null;

        state = state.copyWith(
          activeSessionId: response.sessionId,
          messages: List.unmodifiable(updatedMessages),
          isSending: false,
        );

        if (isNewSession) {
          // Refresh sessions list in background so the newly created owned session appears.
          loadSessions();
        }
        return true;
      }

      // CLARIFICATION_REQUIRED, HELP, or REFUSED: do NOT create or mutate sessionId.
      final transientUserMsg = AiAssistantMessage(
        senderType: 'USER',
        messageText: trimmed,
        createdAt: response.reply.createdAt,
      );
      final updatedMessages = [...state.messages, transientUserMsg, response.reply];
      state = state.copyWith(
        messages: List.unmodifiable(updatedMessages),
        isSending: false,
      );
      return true;
    } catch (error) {
      if (!ref.mounted || sendGeneration != _sendGeneration) return false;
      if (isAccessDeniedError(error)) {
        _handleAccessLost(error);
        return false;
      }

      final isUncertain = error is ApiException
          ? (error.isNetwork ||
              (error.statusCode != null && error.statusCode! >= 500))
          : true;

      state = state.copyWith(
        isSending: false,
        uncertainSendPendingRecovery: isUncertain,
        errorMessage: isUncertain
            ? 'Connection interrupted while waiting for reply. Reload history before retrying.'
            : (error is ApiException && error.message.isNotEmpty
                  ? error.message
                  : 'Unable to send message. Please try again.'),
      );
      return false;
    }
  }

  Future<bool> recoverAfterUncertainSend() async {
    if (state.accessLost) return false;
    final sessionId = state.activeSessionId;
    if (sessionId != null) {
      return loadMessages(sessionId);
    }
    final ok = await loadSessions(page: 1);
    if (ok && ref.mounted) {
      state = state.copyWith(uncertainSendPendingRecovery: false);
    }
    return ok;
  }

  void clearError() {
    if (state.errorMessage != null) {
      state = state.copyWith(clearError: true);
    }
  }

  static List<AiAssistantSession> _mergeSessions(
    List<AiAssistantSession> existing,
    List<AiAssistantSession> incoming,
  ) {
    final byId = <String, AiAssistantSession>{
      for (final s in existing) s.id.toLowerCase(): s,
    };
    for (final s in incoming) {
      byId[s.id.toLowerCase()] = s;
    }
    final list = byId.values.toList()
      ..sort((a, b) {
        final cmp = b.createdAt.compareTo(a.createdAt);
        return cmp != 0 ? cmp : b.id.compareTo(a.id);
      });
    return list;
  }

  static List<AiAssistantMessage> _mergeMessages(
    List<AiAssistantMessage> first,
    List<AiAssistantMessage> second,
  ) {
    final persistedById = <String, AiAssistantMessage>{};
    final transient = <AiAssistantMessage>[];

    for (final msg in [...first, ...second]) {
      if (msg.isPersisted) {
        persistedById[msg.id!] = msg;
      } else {
        transient.add(msg);
      }
    }

    final persisted = persistedById.values.toList()
      ..sort((a, b) {
        final cmp = a.createdAt.compareTo(b.createdAt);
        if (cmp != 0) return cmp;
        final aNum = BigInt.tryParse(a.id ?? '');
        final bNum = BigInt.tryParse(b.id ?? '');
        if (aNum != null && bNum != null) {
          return aNum.compareTo(bNum);
        }
        return (a.id ?? '').compareTo(b.id ?? '');
      });

    return [...persisted, ...transient];
  }
}

final aiAssistantControllerProvider = NotifierProvider.autoDispose
    .family<AiAssistantController, AiAssistantState, AiAssistantContext>(
      AiAssistantController.new,
      name: 'aiAssistantController',
    );
