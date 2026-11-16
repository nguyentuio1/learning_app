import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/session/auth_controller.dart';
import '../../../data/models/ai_assistant_models.dart';
import '../../../routes/route_paths.dart';
import 'ai_assistant_controller.dart';

class CustomerAssistantPage extends ConsumerStatefulWidget {
  const CustomerAssistantPage({
    this.orderId,
    this.trackingNumber,
    this.sessionId,
    super.key,
  });

  final String? orderId;
  final String? trackingNumber;
  final String? sessionId;

  @override
  ConsumerState<CustomerAssistantPage> createState() =>
      _CustomerAssistantPageState();
}

class _CustomerAssistantPageState extends ConsumerState<CustomerAssistantPage> {
  final _inputController = TextEditingController();
  final _scrollController = ScrollController();

  static const _suggestions = [
    'Where is my order?',
    'What is my delivery status?',
    'How do I cancel an order?',
  ];

  late final AiAssistantContext _contextKey;

  @override
  void initState() {
    super.initState();
    _contextKey = AiAssistantContext(
      orderId: widget.orderId,
      trackingNumber: widget.trackingNumber,
      sessionId: widget.sessionId,
    );
    _inputController.addListener(_onInputChanged);
  }

  void _onInputChanged() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _inputController
      ..removeListener(_onInputChanged)
      ..dispose();
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(aiAssistantControllerProvider(_contextKey));
    final notifier = ref.read(
      aiAssistantControllerProvider(_contextKey).notifier,
    );
    final currentUser = ref.watch(currentUserProvider);

    if (state.accessLost) {
      return Scaffold(
        appBar: AppBar(title: const Text('AI Assistant')),
        body: _AccessLostView(
          message:
              state.errorMessage ??
              'You no longer have access to the AI assistant.',
          onBack: _goBack,
        ),
      );
    }

    final theme = Theme.of(context);
    final trimmedInput = _inputController.text.trim();
    final canSend = trimmedInput.isNotEmpty && !state.isSending;

    return Scaffold(
      appBar: AppBar(
        title: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('AI Assistant'),
            Text(
              'How can I help with your deliveries?',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.normal),
            ),
          ],
        ),
        actions: [
          IconButton(
            key: const Key('assistant-sessions-button'),
            tooltip: 'Conversations',
            icon: const Icon(Icons.history),
            onPressed: () => _openSessionsSheet(context, notifier),
          ),
          IconButton(
            key: const Key('assistant-new-chat-button'),
            tooltip: 'New conversation',
            icon: const Icon(Icons.add_comment_outlined),
            onPressed: notifier.startNewConversation,
          ),
          IconButton(
            key: const Key('assistant-refresh-button'),
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh),
            onPressed: state.isLoadingMessages || state.isSending
                ? null
                : () {
                    if (state.activeSessionId != null) {
                      notifier.loadMessages(state.activeSessionId!);
                    } else {
                      notifier.loadSessions();
                    }
                  },
          ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView(
              controller: _scrollController,
              padding: const EdgeInsets.all(16),
              children: [
                _WelcomeBanner(
                  userName: currentUser?.fullName,
                  orderId: state.selectedOrderId,
                  trackingNumber: state.selectedTrackingNumber,
                  onClearOrder: state.selectedOrderId == null
                      ? null
                      : notifier.clearOrderContext,
                ),
                if (state.activeSessionId != null &&
                    state.messagesPage > 1) ...[
                  const SizedBox(height: 8),
                  Center(
                    child: TextButton(
                      key: const Key('assistant-load-older-button'),
                      onPressed: state.isLoadingMessages
                          ? null
                          : () => notifier.loadMessages(
                              state.activeSessionId!,
                              page: state.messagesPage - 1,
                              prependOlder: true,
                            ),
                      child: const Text('Load older messages'),
                    ),
                  ),
                ],
                if (state.isLoadingMessages && state.messages.isEmpty) ...[
                  const SizedBox(height: 24),
                  const Center(child: CircularProgressIndicator()),
                ],
                const SizedBox(height: 12),
                for (final msg in state.messages)
                  _MessageBubble(message: msg),
                if (state.isSending)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                        SizedBox(width: 8),
                        Text('Assistant is thinking…'),
                      ],
                    ),
                  ),
                const SizedBox(height: 16),
                Text(
                  'TRY ASKING',
                  style: theme.textTheme.labelSmall?.copyWith(
                    letterSpacing: 0.8,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final suggestion in _suggestions)
                      ActionChip(
                        label: Text(suggestion),
                        onPressed: state.isSending
                            ? null
                            : () => _submitText(notifier, suggestion),
                      ),
                  ],
                ),
              ],
            ),
          ),
          if (state.errorMessage != null)
            Container(
              key: const Key('assistant-error-banner'),
              width: double.infinity,
              color: theme.colorScheme.errorContainer,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Row(
                children: [
                  Icon(
                    Icons.error_outline,
                    size: 18,
                    color: theme.colorScheme.error,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      state.errorMessage!,
                      style: TextStyle(
                        color: theme.colorScheme.onErrorContainer,
                      ),
                    ),
                  ),
                  if (state.uncertainSendPendingRecovery)
                    TextButton(
                      key: const Key('assistant-recover-button'),
                      onPressed: state.isLoadingMessages ||
                              state.isLoadingSessions
                          ? null
                          : notifier.recoverAfterUncertainSend,
                      child: Text(
                        state.activeSessionId != null
                            ? 'Reload history'
                            : 'Check sessions',
                      ),
                    )
                  else
                    IconButton(
                      icon: const Icon(Icons.close, size: 18),
                      onPressed: notifier.clearError,
                    ),
                ],
              ),
            ),
          SafeArea(
            top: false,
            child: Container(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
              decoration: BoxDecoration(
                color: theme.colorScheme.surface,
                border: Border(
                  top: BorderSide(color: theme.colorScheme.outlineVariant),
                ),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          key: const Key('assistant-message-input'),
                          controller: _inputController,
                          enabled: !state.isSending,
                          minLines: 1,
                          maxLines: 4,
                          textInputAction: TextInputAction.send,
                          onSubmitted: (_) {
                            if (canSend) {
                              _submitText(notifier, _inputController.text);
                            }
                          },
                          decoration: const InputDecoration(
                            hintText: 'Ask about your deliveries...',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      FilledButton(
                        key: const Key('assistant-send-button'),
                        onPressed: canSend
                            ? () => _submitText(notifier, _inputController.text)
                            : null,
                        child: const Icon(Icons.send),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'Answers use your real order data.',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _submitText(
    AiAssistantController notifier,
    String text,
  ) async {
    final trimmed = text.trim();
    if (trimmed.isEmpty) return;
    final ok = await notifier.sendMessage(trimmed);
    if (ok && mounted && text == _inputController.text) {
      _inputController.clear();
    }
    if (mounted && _scrollController.hasClients) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_scrollController.hasClients) {
          _scrollController.animateTo(
            _scrollController.position.maxScrollExtent,
            duration: const Duration(milliseconds: 200),
            curve: Curves.easeOut,
          );
        }
      });
    }
  }

  void _openSessionsSheet(
    BuildContext context,
    AiAssistantController notifier,
  ) {
    notifier.loadSessions();
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => Consumer(
        builder: (_, sheetRef, _) {
          final state = sheetRef.watch(
            aiAssistantControllerProvider(_contextKey),
          );
          return SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Conversations',
                          style: Theme.of(sheetContext).textTheme.titleMedium,
                        ),
                      ),
                      TextButton.icon(
                        onPressed: () {
                          notifier.startNewConversation();
                          Navigator.of(sheetContext).pop();
                        },
                        icon: const Icon(Icons.add),
                        label: const Text('New chat'),
                      ),
                    ],
                  ),
                  const Divider(),
                  if (state.isLoadingSessions && state.sessions.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Center(child: CircularProgressIndicator()),
                    )
                  else if (state.sessions.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Text(
                        'No saved conversations yet.',
                        textAlign: TextAlign.center,
                      ),
                    )
                  else
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxHeight: 320),
                      child: ListView.builder(
                        shrinkWrap: true,
                        itemCount:
                            state.sessions.length +
                            (state.hasMoreSessions ? 1 : 0),
                        itemBuilder: (_, index) {
                          if (index == state.sessions.length) {
                            return TextButton(
                              onPressed: state.isLoadingSessions
                                  ? null
                                  : () => notifier.loadSessions(
                                      page: state.sessionsPage + 1,
                                      append: true,
                                    ),
                              child: const Text('Load more conversations'),
                            );
                          }
                          final session = state.sessions[index];
                          final isSelected =
                              session.id == state.activeSessionId;
                          return ListTile(
                            selected: isSelected,
                            leading: const Icon(Icons.chat_bubble_outline),
                            title: Text(
                              session.sessionTitle?.isNotEmpty == true
                                  ? session.sessionTitle!
                                  : 'Conversation ${session.id.substring(0, 8)}',
                            ),
                            subtitle: Text(
                              session.createdAt.toLocal().toString(),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                            onTap: () {
                              Navigator.of(sheetContext).pop();
                              notifier.selectSession(
                                session.id,
                                title: session.sessionTitle,
                              );
                            },
                          );
                        },
                      ),
                    ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  void _goBack() {
    final router = GoRouter.maybeOf(context);
    if (Navigator.of(context).canPop()) {
      Navigator.of(context).pop();
    } else if (router != null) {
      context.go(RoutePaths.customerHome);
    }
  }
}

class _WelcomeBanner extends StatelessWidget {
  const _WelcomeBanner({
    this.userName,
    this.orderId,
    this.trackingNumber,
    this.onClearOrder,
  });

  final String? userName;
  final String? orderId;
  final String? trackingNumber;
  final VoidCallback? onClearOrder;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final greetingName = userName?.trim();
    final greeting = (greetingName != null && greetingName.isNotEmpty)
        ? 'Hi $greetingName! '
        : 'Hi! ';
    final bodyText = orderId != null
        ? '${greeting}Ask me about this order or any of your deliveries.'
        : '${greeting}Ask me about your deliveries, tracking status, or ETA.';

    return Card(
      color: theme.colorScheme.primaryContainer.withValues(alpha: 0.45),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (orderId != null) ...[
              Chip(
                key: const Key('assistant-order-chip'),
                avatar: const Icon(Icons.local_shipping_outlined, size: 16),
                label: Text(
                  trackingNumber?.isNotEmpty == true
                      ? trackingNumber!
                      : orderId!,
                ),
                onDeleted: onClearOrder,
              ),
              const SizedBox(height: 6),
            ],
            Text(bodyText, style: theme.textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({required this.message});

  final AiAssistantMessage message;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isUser = message.isUser;
    final outcome = message.outcome;

    final outcomeLabel = switch (outcome) {
      AiAssistantOutcome.clarificationRequired => 'Clarification needed',
      AiAssistantOutcome.help => 'Help',
      AiAssistantOutcome.refused => 'Unavailable',
      _ => null,
    };

    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 6),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        constraints: const BoxConstraints(maxWidth: 320),
        decoration: BoxDecoration(
          color: isUser
              ? theme.colorScheme.primary
              : theme.colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (!isUser && outcomeLabel != null) ...[
              Text(
                outcomeLabel,
                style: theme.textTheme.labelSmall?.copyWith(
                  color: theme.colorScheme.primary,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 4),
            ],
            SelectableText(
              message.messageText,
              style: TextStyle(
                color: isUser
                    ? theme.colorScheme.onPrimary
                    : theme.colorScheme.onSurface,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AccessLostView extends StatelessWidget {
  const _AccessLostView({
    required this.message,
    required this.onBack,
  });

  final String message;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Center(
      key: const Key('assistant-access-lost-view'),
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.lock_person_outlined,
              size: 48,
              color: Theme.of(context).colorScheme.error,
            ),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: onBack,
              child: const Text('Back'),
            ),
          ],
        ),
      ),
    );
  }
}
